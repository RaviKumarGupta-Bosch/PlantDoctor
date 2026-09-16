using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceSimulator.Core.Com;
using DeviceSimulator.Core.Errors;
using DeviceSimulator.Core.Scanner;
using DeviceSimulator.Core.Sensors;
using DeviceSimulator.Core.Transports;
using PlantDoctor.Contracts;

namespace DeviceSimulator.UI.ViewModels;

/// <summary>
/// Drives the Device Simulator: three sensor devices plus a barcode scanner, each streaming
/// to the Plant Monitor main application over its own transport. Closing a device tears the
/// link down so the main application sees it drop.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private const int FeedCapacity = 300;

    private readonly ISensorSimulationService _sensors;
    private readonly IScannerSimulator _scanner;
    private readonly IComPortSimulator _com;
    private readonly IErrorInjector _errors;
    private readonly DeviceTransportSet _transports;
    private readonly Random _rng = new();

    public ObservableCollection<DeviceVm> Devices { get; } = new();
    public ObservableCollection<string> EmittedFeed { get; } = new();
    public ObservableCollection<ErrorEventDto> InjectedFaults { get; } = new();

    [ObservableProperty] private string _scannerPort = "COM3";
    [ObservableProperty] private double _scanProbability = 0.15;
    [ObservableProperty] private int _emitIntervalMs = 1500;

    public MainViewModel(ISensorSimulationService sensors, IScannerSimulator scanner,
        IComPortSimulator com, IErrorInjector errors, DeviceTransportSet transports)
    {
        _sensors = sensors;
        _scanner = scanner;
        _com = com;
        _errors = errors;
        _transports = transports;

        foreach (var def in _sensors.Sensors)
        {
            var channel = DefaultSensors.ChannelFor(def.Name);
            Devices.Add(new DeviceVm(def.Name, IconFor(def.Name), _transports[channel], def));
        }
        Devices.Add(new DeviceVm("Scanner", "🔍", _transports[TransportChannel.ComPort], sensor: null));
    }

    private DeviceVm ScannerDevice => Devices.First(d => d.IsScanner);
    private IEnumerable<DeviceVm> SensorDevices => Devices.Where(d => !d.IsScanner);

    private static string IconFor(string sensorName) => sensorName switch
    {
        "Temperature" => "🌡️",
        "Vibration" => "📳",
        "Pressure" => "💨",
        _ => "📟"
    };

    [RelayCommand]
    private async Task OpenDeviceAsync(DeviceVm device)
    {
        var connected = await device.Transport.ConnectAsync();
        device.IsOpen = connected;
        device.LastError = device.Transport.LastError;
        device.Status = connected ? ChannelStatus.Connected : ChannelStatus.Error;

        if (device.IsScanner)
        {
            if (connected) { _scanner.Connect(ScannerPort); _com.Connect(ScannerPort); }
            else { _scanner.Disconnect(); _com.Disconnect(); }
        }

        AddFeed(connected
            ? $"{device.Name} opened on {device.Endpoint}"
            : $"{device.Name} failed to open — {device.LastError}");
    }

    [RelayCommand]
    private void CloseDevice(DeviceVm device)
    {
        device.Transport.Disconnect();
        device.IsOpen = false;
        device.Status = ChannelStatus.Closed;
        device.LastError = null;

        if (device.IsScanner)
        {
            _scanner.Disconnect();
            _com.Disconnect();
        }

        AddFeed($"{device.Name} closed by operator");
    }

    [RelayCommand]
    private async Task OpenAllAsync()
    {
        foreach (var device in Devices) await OpenDeviceAsync(device);
    }

    [RelayCommand]
    private void CloseAll()
    {
        foreach (var device in Devices) CloseDevice(device);
    }

    [RelayCommand]
    private void CloseSensors()
    {
        foreach (var device in SensorDevices.ToList()) CloseDevice(device);
    }

    [RelayCommand]
    private void CloseScanner() => CloseDevice(ScannerDevice);

    [RelayCommand]
    private async Task InjectSensorTimeoutAsync()
    {
        var device = SensorDevices.First();
        _sensors.FreezeSensor(device.Name);
        await PublishFaultAsync(device, _errors.SensorTimeout(device.Name));
    }

    [RelayCommand]
    private async Task InjectComDisconnectAsync()
    {
        var device = ScannerDevice;
        _com.InjectDisconnect();
        await PublishFaultAsync(device, _errors.ComDisconnect(ScannerPort));
        CloseDevice(device);
    }

    [RelayCommand]
    private async Task InjectOverflowAsync()
    {
        var device = Devices.FirstOrDefault(d => d.IsOpen) ?? Devices.First();
        await PublishFaultAsync(device, _errors.OverflowException());
    }

    [RelayCommand]
    private async Task InjectOutOfRangeAsync()
    {
        var device = SensorDevices.First();
        var def = device.Sensor!;
        _sensors.InjectOutOfRange(def.Name);
        await PublishFaultAsync(device, _errors.OutOfRangeValue(def.Name, def.Max * 10, def.Unit));
    }

    /// <summary>Called by a DispatcherTimer from the view; emits one sample from every open device.</summary>
    public async Task TickAsync()
    {
        foreach (var device in SensorDevices.Where(d => d.IsOpen).ToList())
        {
            var reading = _sensors.Tick(device.Sensor!);
            var sent = await device.Transport.SendAsync(DeviceMessage.ForReading(device.Name, reading));
            if (sent)
            {
                device.SentCount++;
                device.Status = ChannelStatus.Connected;
                device.LastValue = $"{reading.Value:0.##} {reading.Unit} @ {reading.TimestampUtc:HH:mm:ss}";
                AddFeed($"[{reading.TimestampUtc:HH:mm:ss}] {device.Name} → {reading.Value:0.##}{reading.Unit} ({reading.Status})");
            }
            else
            {
                MarkLinkLost(device);
            }
        }

        var scanner = ScannerDevice;
        if (scanner.IsOpen && _rng.NextDouble() < ScanProbability)
        {
            var scan = _scanner.NextScan();
            var sent = await scanner.Transport.SendAsync(DeviceMessage.ForScan(scanner.Name, scan));
            if (sent)
            {
                scanner.SentCount++;
                scanner.Status = ChannelStatus.Connected;
                scanner.LastValue = $"{scan.CodeType} {scan.Code}";
                AddFeed($"[{scan.TimestampUtc:HH:mm:ss}] Scanner → {scan.CodeType} {scan.Code} ({scan.Port}) {_com.NextFrame()}");
            }
            else
            {
                MarkLinkLost(scanner);
            }
        }
    }

    private async Task PublishFaultAsync(DeviceVm device, ErrorEventDto fault)
    {
        InjectedFaults.Insert(0, fault);
        while (InjectedFaults.Count > FeedCapacity) InjectedFaults.RemoveAt(InjectedFaults.Count - 1);

        if (device.IsOpen && !await device.Transport.SendAsync(DeviceMessage.ForError(device.Name, fault)))
            MarkLinkLost(device);

        AddFeed($"[{fault.TimestampUtc:HH:mm:ss}] FAULT {fault.ErrorCode} via {device.Name}");
    }

    private void MarkLinkLost(DeviceVm device)
    {
        device.IsOpen = false;
        device.Status = ChannelStatus.Error;
        device.LastError = device.Transport.LastError;
        if (device.IsScanner) { _scanner.Disconnect(); _com.Disconnect(); }
        AddFeed($"{device.Name} link lost — {device.LastError}");
    }

    private void AddFeed(string line)
    {
        EmittedFeed.Insert(0, line);
        while (EmittedFeed.Count > FeedCapacity) EmittedFeed.RemoveAt(EmittedFeed.Count - 1);
    }
}

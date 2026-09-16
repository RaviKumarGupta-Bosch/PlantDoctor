using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlantSimulator.Contracts;
using PlantSimulator.Core.Com;
using PlantSimulator.Core.Errors;
using PlantSimulator.Core.Logging;
using PlantSimulator.Core.Scanner;
using PlantSimulator.Core.Sensors;
using PlantSimulator.Core.Transports;
using PlantSimulator.UI.Views;

namespace PlantSimulator.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISensorSimulationService _sensors;
    private readonly IComPortSimulator _com;
    private readonly IErrorInjector _errors;
    private readonly IPlantLogger _log;
    private readonly ICommunicationHub _hub;
    private readonly IScannerSimulator _scanner;
    private readonly NamedPipeSensorClient _tempClient;
    private readonly TcpSensorClient _vibrationClient;
    private readonly BluetoothSimSensorClient _pressureClient;
    private readonly Random _rng = new();

    private readonly Dictionary<TransportChannel, DeviceBlockVm> _blocksByChannel;

    public ObservableCollection<SensorReadingDto> Readings { get; } = new();
    public ObservableCollection<ErrorEventDto> Events { get; } = new();
    public ObservableCollection<string> ScanFeed { get; } = new();
    public ObservableCollection<DeviceBlockVm> Blocks { get; } = new();

    [ObservableProperty] private string _scannerPort = "COM3";
    [ObservableProperty] private string _hubStatus = "Starting…";

    public MainViewModel(ISensorSimulationService sensors, IComPortSimulator com,
        IErrorInjector errors, IPlantLogger log, ICommunicationHub hub, IScannerSimulator scanner,
        NamedPipeSensorClient tempClient, TcpSensorClient vibrationClient, BluetoothSimSensorClient pressureClient)
    {
        _sensors = sensors; _com = com; _errors = errors; _log = log;
        _hub = hub; _scanner = scanner;
        _tempClient = tempClient; _vibrationClient = vibrationClient; _pressureClient = pressureClient;

        _com.Event += (_, e) => _log.Log(e);
        _scanner.ScanReceived += (_, scan) => _hub.SubmitScan(scan);

        var temperature = new DeviceBlockVm("Temperature", "🌡️", "Named Pipe");
        var vibration = new DeviceBlockVm("Vibration", "📳", "TCP Socket");
        var pressure = new DeviceBlockVm("Pressure", "💨", "Bluetooth (Simulated)");
        var scannerBlock = new DeviceBlockVm("Scanner", "🔍", "COM Port");
        Blocks.Add(temperature);
        Blocks.Add(vibration);
        Blocks.Add(pressure);
        Blocks.Add(scannerBlock);

        _blocksByChannel = new Dictionary<TransportChannel, DeviceBlockVm>
        {
            [TransportChannel.NamedPipe] = temperature,
            [TransportChannel.Tcp] = vibration,
            [TransportChannel.Bluetooth] = pressure,
            [TransportChannel.ComPort] = scannerBlock
        };

        _hub.ChannelStatusChanged += OnChannelStatusChanged;
        _hub.SensorDataReceived += OnSensorDataReceived;
        _hub.ScanDataReceived += OnScanDataReceived;
        _hub.Start();
        HubStatus = "Running — listening on Named Pipe, TCP, and Bluetooth(sim) channels";
    }

    private void OnChannelStatusChanged(object? _, ChannelStatusEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_blocksByChannel.TryGetValue(e.Channel, out var block))
                block.Status = e.Status;
        });
    }

    private void OnSensorDataReceived(object? _, SensorTransportEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_blocksByChannel.TryGetValue(e.Channel, out var block))
                block.LastValue = $"{e.Reading.Value:0.##} {e.Reading.Unit} @ {e.Reading.TimestampUtc:HH:mm:ss}";
        });
    }

    private void OnScanDataReceived(object? _, ScanEventDto scan)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_blocksByChannel.TryGetValue(TransportChannel.ComPort, out var block))
                block.LastValue = $"{scan.CodeType} {scan.Code}";
            ScanFeed.Insert(0, $"[{scan.TimestampUtc:HH:mm:ss}] {scan.CodeType} {scan.Code} ({scan.Port})");
            while (ScanFeed.Count > 200) ScanFeed.RemoveAt(ScanFeed.Count - 1);
            _log.Log(scan);
        });
    }

    [RelayCommand]
    private void ConnectScanner()
    {
        _scanner.Connect(ScannerPort);
        _blocksByChannel[TransportChannel.ComPort].Status = _scanner.Status;
    }

    [RelayCommand]
    private void DisconnectScanner()
    {
        _scanner.Disconnect();
        _blocksByChannel[TransportChannel.ComPort].Status = _scanner.Status;
    }

    [RelayCommand]
    private void InjectSensorTimeout()
    {
        var s = _sensors.Sensors.First();
        _sensors.FreezeSensor(s.Name);
        Publish(_errors.SensorTimeout(s.Name));
    }

    [RelayCommand]
    private void InjectComDisconnect()
    {
        _com.InjectDisconnect();
        Publish(_errors.ComDisconnect(ScannerPort));
    }

    [RelayCommand] private void InjectOverflow() => Publish(_errors.OverflowException());

    [RelayCommand]
    private void InjectOutOfRange()
    {
        var s = _sensors.Sensors.First();
        _sensors.InjectOutOfRange(s.Name);
        Publish(_errors.OutOfRangeValue(s.Name, s.Max * 10, s.Unit));
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var window = Application.Current.MainWindow;
        var dlg = new SettingsDialog { Owner = window };
        dlg.ShowDialog();
    }

    private void Publish(ErrorEventDto e)
    {
        Events.Insert(0, e);
        _log.Log(e);
    }

    /// <summary>Called by a DispatcherTimer from the view.</summary>
    public void Tick()
    {
        foreach (var def in _sensors.Sensors)
        {
            var r = _sensors.Tick(def);
            var existing = Readings.FirstOrDefault(x => x.Name == r.Name);
            if (existing != null) Readings.Remove(existing);
            Readings.Add(r);
            _log.Log(r);

            _ = SendOverTransportAsync(def.Name, r);
        }

        if (_scanner.Status == "Connected" && _rng.NextDouble() < 0.15)
            _scanner.NextScan();
    }

    private Task SendOverTransportAsync(string sensorName, SensorReadingDto reading) => sensorName switch
    {
        "Temperature" => _tempClient.SendAsync(reading),
        "Vibration" => _vibrationClient.SendAsync(reading),
        "Pressure" => _pressureClient.SendAsync(reading),
        _ => Task.CompletedTask
    };
}


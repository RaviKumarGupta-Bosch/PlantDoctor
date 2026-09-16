using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PlantDoctor.Contracts;
using PlantMonitor.Core.Artifacts;
using PlantMonitor.Core.Diagnostics;
using PlantMonitor.Core.Ingest;
using PlantMonitor.Core.Logging;
using PlantMonitor.ServiceHost;

namespace PlantMonitor.UI.ViewModels;

/// <summary>
/// Main application view model: shows every device connection, the data each one emits,
/// and lets the operator disconnect sensors or the scanner by stopping their listener.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private const int FeedCapacity = 300;

    private readonly ICommunicationHub _hub;
    private readonly PlantMonitorService _monitor;
    private readonly IPlantLogger _log;
    private readonly IArtifactWriter _artifactWriter;

    public ObservableCollection<ChannelVm> Channels { get; } = new();
    public ObservableCollection<SensorReadingDto> Readings { get; } = new();
    public ObservableCollection<string> ScanFeed { get; } = new();
    public ObservableCollection<ErrorEventDto> Events { get; } = new();

    [ObservableProperty] private string _hubStatus = "Stopped";

    public string LogFilePath => _log.CurrentLogFilePath;

    public MainViewModel(ICommunicationHub hub, PlantMonitorService monitor, IPlantLogger log, IArtifactWriter artifactWriter)
    {
        _hub = hub;
        _monitor = monitor;
        _log = log;
        _artifactWriter = artifactWriter;

        Channels.Add(new ChannelVm(TransportChannel.NamedPipe, "Temperature", "🌡️"));
        Channels.Add(new ChannelVm(TransportChannel.Tcp, "Vibration", "📳"));
        Channels.Add(new ChannelVm(TransportChannel.Bluetooth, "Pressure", "💨"));
        Channels.Add(new ChannelVm(TransportChannel.ComPort, "Scanner", "🔍"));

        _hub.ChannelStatusChanged += OnChannelStatusChanged;
        _hub.MessageReceived += OnMessageReceived;
        _hub.ProcessingErrorRaised += (_, error) => OnUiThread(() => ApplyError(error));
        _monitor.ErrorReported += OnMonitorErrorReported;
    }

    /// <summary>Log-write failures (E907) surface here; they are shown but deliberately not re-logged.</summary>
    private void OnMonitorErrorReported(object? sender, ErrorEventDto error) =>
        OnUiThread(() =>
        {
            if (error.ErrorCode != DiagnosticCodes.ForApplication(ProcessingFault.LogWriteFailed)) return;
            Events.Insert(0, error);
            while (Events.Count > FeedCapacity) Events.RemoveAt(Events.Count - 1);
        });

    private IEnumerable<ChannelVm> SensorChannels => Channels.Where(c => !c.IsScanner);
    private ChannelVm ScannerChannel => Channels.First(c => c.IsScanner);

    [RelayCommand]
    private void StartAll()
    {
        _hub.Start();
        HubStatus = "Listening on all device channels";
    }

    [RelayCommand]
    private void DisconnectAll()
    {
        _hub.Stop();
        HubStatus = "All device channels disconnected";
    }

    [RelayCommand]
    private void ConnectChannel(ChannelVm channel) => _hub.StartChannel(channel.Channel);

    [RelayCommand]
    private void DisconnectChannel(ChannelVm channel) => _hub.StopChannel(channel.Channel);

    [RelayCommand]
    private void DisconnectSensors()
    {
        foreach (var channel in SensorChannels) _hub.StopChannel(channel.Channel);
        HubStatus = "Sensor channels disconnected — scanner unaffected";
    }

    [RelayCommand]
    private void DisconnectScanner()
    {
        _hub.StopChannel(ScannerChannel.Channel);
        HubStatus = "Scanner channel disconnected — sensors unaffected";
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        var folder = Path.GetDirectoryName(_log.CurrentLogFilePath);
        if (folder is not null && Directory.Exists(folder))
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task ExportZip()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Zip Archive|*.zip",
            DefaultExt = ".zip",
            FileName = $"plant-monitor-artifact-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _artifactWriter.WriteZipAsync(dialog.FileName, _log.CurrentLogFilePath, SnapshotChannels());
                MessageBox.Show($"Artifact exported to:\n{dialog.FileName}",
                    "Plant Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export artifact:\n{ex.Message}",
                    "Plant Monitor", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private List<ChannelStatusSummary> SnapshotChannels() => Channels
        .Select(c => new ChannelStatusSummary
        {
            Channel = c.TransportLabel,
            DeviceName = c.DeviceName,
            Endpoint = c.Endpoint,
            Status = c.Status,
            Detail = c.Detail,
            LastValue = c.LastValue,
            MessageCount = c.MessageCount,
            LastMessageUtc = c.LastMessageUtc
        })
        .ToList();

    private void OnChannelStatusChanged(object? sender, ChannelStatusEventArgs e) =>
        OnUiThread(() =>
        {
            var vm = Channels.FirstOrDefault(c => c.Channel == e.Channel);
            if (vm is null) return;
            vm.Status = e.Status;
            vm.Detail = e.Detail;
        });

    private void OnMessageReceived(object? sender, DeviceMessageEventArgs e) =>
        OnUiThread(() =>
        {
            var vm = Channels.FirstOrDefault(c => c.Channel == e.Channel);
            if (vm is not null)
            {
                vm.MessageCount++;
                vm.LastMessageUtc = DateTime.UtcNow;
                vm.Status = ChannelStatus.Connected;
            }

            try
            {
                switch (e.Message.Type)
                {
                    case DeviceMessageTypes.Sensor when e.Message.Reading is { } reading:
                        ApplyReading(vm, reading);
                        break;
                    case DeviceMessageTypes.Scan when e.Message.Scan is { } scan:
                        ApplyScan(vm, scan);
                        break;
                    case DeviceMessageTypes.Error when e.Message.Error is { } error:
                        ApplyError(DiagnosticEvents.FromDevice(e.Channel, error));
                        break;
                }
            }
            catch (Exception ex)
            {
                ApplyError(DiagnosticEvents.FromApplication(
                    ProcessingFault.UnhandledProcessingError, ex.Message, e.Channel, ex));
            }
        });

    private void ApplyReading(ChannelVm? vm, SensorReadingDto reading)
    {
        var existing = Readings.FirstOrDefault(r => r.Name == reading.Name);
        if (existing is not null) Readings.Remove(existing);
        Readings.Add(reading);

        if (vm is not null)
            vm.LastValue = $"{reading.Value:0.##} {reading.Unit} @ {reading.TimestampUtc:HH:mm:ss}";

        _monitor.ReportSensorReading(reading);
    }

    private void ApplyScan(ChannelVm? vm, ScanEventDto scan)
    {
        if (vm is not null) vm.LastValue = $"{scan.CodeType} {scan.Code}";

        ScanFeed.Insert(0, $"[{scan.TimestampUtc:HH:mm:ss}] {scan.CodeType} {scan.Code} ({scan.Port})");
        while (ScanFeed.Count > FeedCapacity) ScanFeed.RemoveAt(ScanFeed.Count - 1);

        _monitor.ReportScanEvent(scan);
    }

    private void ApplyError(ErrorEventDto error)
    {
        Events.Insert(0, error);
        while (Events.Count > FeedCapacity) Events.RemoveAt(Events.Count - 1);

        _monitor.ReportError(error);
    }

    private static void OnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }
}

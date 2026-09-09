using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlantSimulator.Contracts;
using PlantSimulator.Core.Com;
using PlantSimulator.Core.Errors;
using PlantSimulator.Core.Logging;
using PlantSimulator.Core.Sensors;

namespace PlantSimulator.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISensorSimulationService _sensors;
    private readonly IComPortSimulator _com;
    private readonly IErrorInjector _errors;
    private readonly IPlantLogger _log;

    public ObservableCollection<SensorReadingDto> Readings { get; } = new();
    public ObservableCollection<string> ComTraffic { get; } = new();
    public ObservableCollection<ErrorEventDto> Events { get; } = new();

    [ObservableProperty] private string _comStatus = "Disconnected";
    [ObservableProperty] private string _selectedPort = "COM1";

    public MainViewModel(ISensorSimulationService sensors, IComPortSimulator com,
        IErrorInjector errors, IPlantLogger log)
    {
        _sensors = sensors; _com = com; _errors = errors; _log = log;
        _com.Event += (_, e) => { ComStatus = e.Status; _log.Log(e); };
    }

    [RelayCommand] private void Connect() => _com.Connect(SelectedPort);
    [RelayCommand] private void Disconnect() => _com.Disconnect();

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
        Publish(_errors.ComDisconnect(SelectedPort));
    }

    [RelayCommand] private void InjectOverflow() => Publish(_errors.OverflowException());

    [RelayCommand]
    private void InjectOutOfRange()
    {
        var s = _sensors.Sensors.First();
        _sensors.InjectOutOfRange(s.Name);
        Publish(_errors.OutOfRangeValue(s.Name, s.Max * 10, s.Unit));
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
        }
        if (ComStatus == "Connected")
        {
            ComTraffic.Insert(0, _com.NextFrame());
            while (ComTraffic.Count > 200) ComTraffic.RemoveAt(ComTraffic.Count - 1);
        }
    }
}

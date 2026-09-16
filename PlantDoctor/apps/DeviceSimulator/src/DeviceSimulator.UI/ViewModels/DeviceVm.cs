using CommunityToolkit.Mvvm.ComponentModel;
using DeviceSimulator.Core.Sensors;
using DeviceSimulator.Core.Transports;
using PlantDoctor.Contracts;

namespace DeviceSimulator.UI.ViewModels;

/// <summary>One simulated device block: a sensor, or the barcode scanner when <see cref="Sensor"/> is null.</summary>
public partial class DeviceVm : ObservableObject
{
    public IDeviceTransportClient Transport { get; }
    public SensorDefinition? Sensor { get; }

    public string Name { get; }
    public string Icon { get; }
    public TransportChannel Channel => Transport.Channel;
    public string TransportLabel => Transport.Label;
    public string Endpoint => Transport.Endpoint;
    public bool IsScanner => Sensor is null;

    [ObservableProperty] private string _status = ChannelStatus.Closed;
    [ObservableProperty] private string _lastValue = "—";
    [ObservableProperty] private string? _lastError;
    [ObservableProperty] private int _sentCount;
    [ObservableProperty] private bool _isOpen;

    public DeviceVm(string name, string icon, IDeviceTransportClient transport, SensorDefinition? sensor)
    {
        Name = name;
        Icon = icon;
        Transport = transport;
        Sensor = sensor;
    }
}

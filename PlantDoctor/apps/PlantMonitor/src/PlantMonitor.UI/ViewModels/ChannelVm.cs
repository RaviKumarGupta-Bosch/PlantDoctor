using CommunityToolkit.Mvvm.ComponentModel;
using PlantDoctor.Contracts;

namespace PlantMonitor.UI.ViewModels;

/// <summary>One inbound device connection as seen by the main application.</summary>
public partial class ChannelVm : ObservableObject
{
    public TransportChannel Channel { get; }
    public string DeviceName { get; }
    public string Icon { get; }
    public string TransportLabel => TransportEndpoints.LabelFor(Channel);
    public string Endpoint => TransportEndpoints.DescribeFor(Channel);
    public bool IsScanner => Channel == TransportChannel.ComPort;

    [ObservableProperty] private string _status = ChannelStatus.Stopped;
    [ObservableProperty] private string _lastValue = "—";
    [ObservableProperty] private string? _detail;
    [ObservableProperty] private int _messageCount;
    [ObservableProperty] private DateTime? _lastMessageUtc;

    public ChannelVm(TransportChannel channel, string deviceName, string icon)
    {
        Channel = channel;
        DeviceName = deviceName;
        Icon = icon;
    }
}

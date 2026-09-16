using CommunityToolkit.Mvvm.ComponentModel;

namespace PlantSimulator.UI.ViewModels;

/// <summary>Represents one device block in the block-diagram UI (a sensor simulator or the scanner).</summary>
public partial class DeviceBlockVm : ObservableObject
{
    public string Name { get; }
    public string Icon { get; }
    public string TransportLabel { get; }

    [ObservableProperty] private string _status = "Disconnected";
    [ObservableProperty] private string _lastValue = "—";

    public DeviceBlockVm(string name, string icon, string transportLabel)
    {
        Name = name;
        Icon = icon;
        TransportLabel = transportLabel;
    }
}

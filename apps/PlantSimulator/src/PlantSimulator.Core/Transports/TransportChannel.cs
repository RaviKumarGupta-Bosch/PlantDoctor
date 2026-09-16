namespace PlantSimulator.Core.Transports;

/// <summary>Identifies which physical/simulated transport a device block communicates over.</summary>
public enum TransportChannel
{
    NamedPipe,
    Tcp,
    Bluetooth,
    ComPort
}

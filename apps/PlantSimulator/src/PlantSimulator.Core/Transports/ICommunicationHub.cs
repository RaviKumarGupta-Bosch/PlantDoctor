using PlantSimulator.Contracts;

namespace PlantSimulator.Core.Transports;

/// <summary>
/// The "main application" side ingestion point. Listens across every device transport
/// (named pipe, TCP, simulated Bluetooth) and exposes a single event stream so the UI
/// can show each block going Listening → Connected as data arrives.
/// </summary>
public interface ICommunicationHub
{
    event EventHandler<SensorTransportEventArgs>? SensorDataReceived;
    event EventHandler<ChannelStatusEventArgs>? ChannelStatusChanged;
    event EventHandler<ScanEventDto>? ScanDataReceived;

    void Start();
    void Stop();

    /// <summary>Ingests a scan from the COM-port scanner device (direct call, no socket hop).</summary>
    void SubmitScan(ScanEventDto scan);
}

public sealed class SensorTransportEventArgs : EventArgs
{
    public required TransportChannel Channel { get; init; }
    public required SensorReadingDto Reading { get; init; }
}

public sealed class ChannelStatusEventArgs : EventArgs
{
    public required TransportChannel Channel { get; init; }
    public required string Status { get; init; }
}

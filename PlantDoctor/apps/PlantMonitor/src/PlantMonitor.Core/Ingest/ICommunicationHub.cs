using PlantDoctor.Contracts;

namespace PlantMonitor.Core.Ingest;

/// <summary>
/// The main application's ingestion layer. Owns one listener per device transport
/// (named pipe, TCP, simulated Bluetooth, bridged COM) and can start or stop each one
/// independently — stopping a channel drops the device's link, which is how the
/// "Disconnect sensor / Disconnect scanner" buttons work.
/// </summary>
public interface ICommunicationHub
{
    event EventHandler<DeviceMessageEventArgs>? MessageReceived;
    event EventHandler<ChannelStatusEventArgs>? ChannelStatusChanged;

    /// <summary>Raised when the main application itself fails to ingest data; carries an E9xx code.</summary>
    event EventHandler<ErrorEventDto>? ProcessingErrorRaised;

    bool IsListening(TransportChannel channel);

    void StartChannel(TransportChannel channel);
    void StopChannel(TransportChannel channel);

    void Start();
    void Stop();
}

public sealed class DeviceMessageEventArgs : EventArgs
{
    public required TransportChannel Channel { get; init; }
    public required DeviceMessage Message { get; init; }
}

public sealed class ChannelStatusEventArgs : EventArgs
{
    public required TransportChannel Channel { get; init; }
    public required string Status { get; init; }
    public string? Detail { get; init; }
}

using PlantDoctor.Contracts;

namespace DeviceSimulator.Core.Transports;

/// <summary>
/// A device's outbound link to the Plant Monitor main application. The connection is held
/// open so that <see cref="Disconnect"/> ("Close device") is observable on the other side.
/// </summary>
public interface IDeviceTransportClient : IDisposable
{
    TransportChannel Channel { get; }

    /// <summary>Human-readable transport name, e.g. "Named Pipe".</summary>
    string Label { get; }

    /// <summary>Wire address the main application listens on.</summary>
    string Endpoint { get; }

    bool IsConnected { get; }
    string? LastError { get; }

    Task<bool> ConnectAsync(CancellationToken ct = default);

    /// <summary>Sends one framed message; returns false and drops the link if the main app is gone.</summary>
    Task<bool> SendAsync(DeviceMessage message, CancellationToken ct = default);

    void Disconnect();
}

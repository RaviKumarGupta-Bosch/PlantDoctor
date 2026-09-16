using PlantSimulator.Contracts;

namespace PlantSimulator.Core.Transports;

/// <summary>A sensor-side transport used to deliver a reading to the main application.</summary>
public interface ISensorTransportClient
{
    string ChannelLabel { get; }
    string? LastError { get; }
    Task<bool> SendAsync(SensorReadingDto reading, CancellationToken ct = default);
}

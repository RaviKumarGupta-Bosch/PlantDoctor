using PlantDoctor.Contracts;

namespace DeviceSimulator.Core.Transports;

/// <summary>The four device links owned by this simulator, one per <see cref="TransportChannel"/>.</summary>
public sealed class DeviceTransportSet : IDisposable
{
    private readonly Dictionary<TransportChannel, IDeviceTransportClient> _clients;

    public DeviceTransportSet()
    {
        _clients = new Dictionary<TransportChannel, IDeviceTransportClient>
        {
            [TransportChannel.NamedPipe] = new NamedPipeDeviceClient(),
            [TransportChannel.Tcp] = new TcpDeviceClient(TransportChannel.Tcp),
            [TransportChannel.Bluetooth] = new TcpDeviceClient(TransportChannel.Bluetooth, latencyMinMs: 10, latencyMaxMs: 40),
            [TransportChannel.ComPort] = new TcpDeviceClient(TransportChannel.ComPort)
        };
    }

    public IDeviceTransportClient this[TransportChannel channel] => _clients[channel];

    public IEnumerable<IDeviceTransportClient> All => _clients.Values;

    public void Dispose()
    {
        foreach (var client in _clients.Values) client.Dispose();
        _clients.Clear();
    }
}

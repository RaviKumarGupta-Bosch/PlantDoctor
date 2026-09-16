using System.Net;
using System.Net.Sockets;
using System.Text;
using PlantDoctor.Contracts;

namespace DeviceSimulator.Core.Transports;

/// <summary>
/// Loopback TCP device link, used for three devices:
///   Vibration  — plain TCP socket
///   Pressure   — simulated Bluetooth RFCOMM (real pairing needs radios, so air latency is emulated)
///   Scanner    — simulated COM port bridged over a socket so it can cross the process boundary
/// The connection is held open so closing the device is visible to the main application.
/// </summary>
public sealed class TcpDeviceClient : IDeviceTransportClient
{
    private readonly int _port;
    private readonly int _latencyMinMs;
    private readonly int _latencyMaxMs;
    private readonly Random _rng = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TcpClient? _client;
    private StreamWriter? _writer;

    public TransportChannel Channel { get; }
    public string Label => TransportEndpoints.LabelFor(Channel);
    public string Endpoint => TransportEndpoints.DescribeFor(Channel);
    public string? LastError { get; private set; }

    /// <summary>Synthetic RSSI, only meaningful for the simulated Bluetooth channel.</summary>
    public int LastRssi { get; private set; } = -60;

    public TcpDeviceClient(TransportChannel channel, int latencyMinMs = 0, int latencyMaxMs = 0)
    {
        Channel = channel;
        _port = TransportEndpoints.PortFor(channel);
        _latencyMinMs = latencyMinMs;
        _latencyMaxMs = latencyMaxMs;
    }

    public bool IsConnected
    {
        get
        {
            var socket = _client?.Client;
            if (socket is null || !socket.Connected) return false;
            try
            {
                // Poll reports readable with zero bytes available once the peer closes its end.
                return !(socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch (SocketException) { return false; }
            catch (ObjectDisposedException) { return false; }
        }
    }

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (IsConnected) return true;
            Close();
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, _port, ct);
            _client = client;
            _writer = new StreamWriter(client.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Close();
            return false;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> SendAsync(DeviceMessage message, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_writer is null || !IsConnected)
            {
                LastError = "Not connected.";
                Close();
                return false;
            }

            if (_latencyMaxMs > 0)
                await Task.Delay(_rng.Next(_latencyMinMs, _latencyMaxMs), ct);

            await _writer.WriteLineAsync(WireFormat.Serialize(message).AsMemory(), ct);
            LastRssi = -_rng.Next(40, 90);
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Close();
            return false;
        }
        finally { _gate.Release(); }
    }

    public void Disconnect()
    {
        _gate.Wait();
        try { Close(); }
        finally { _gate.Release(); }
    }

    private void Close()
    {
        _writer?.Dispose();
        _writer = null;
        _client?.Dispose();
        _client = null;
    }

    public void Dispose()
    {
        Disconnect();
        _gate.Dispose();
    }
}

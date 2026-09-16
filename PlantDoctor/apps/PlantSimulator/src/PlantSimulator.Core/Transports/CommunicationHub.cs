using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using PlantSimulator.Contracts;

namespace PlantSimulator.Core.Transports;

/// <summary>
/// Simulates the "main application" ingestion layer. Listens for sensor data over three
/// distinct transports:
///   - Temperature over a Named Pipe (in-process/local IPC)
///   - Vibration over a TCP socket (loopback network)
///   - Pressure over a simulated Bluetooth RFCOMM channel (real BT pairing needs physical
///     radios, so this emulates the same publish semantics over a dedicated loopback TCP
///     port with an added pairing/air latency)
/// The Scanner (COM port) is ingested directly via <see cref="SubmitScan"/> since serial
/// hardware isn't available in this simulator.
/// </summary>
public sealed class CommunicationHub : ICommunicationHub, IDisposable
{
    public const string PipeName = "PlantDoctor.Temperature";
    public const int VibrationTcpPort = 51001;
    public const int BluetoothSimPort = 51002;

    private CancellationTokenSource? _cts;

    public event EventHandler<SensorTransportEventArgs>? SensorDataReceived;
    public event EventHandler<ChannelStatusEventArgs>? ChannelStatusChanged;
    public event EventHandler<ScanEventDto>? ScanDataReceived;

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _ = Task.Run(() => RunNamedPipeServerAsync(ct), ct);
        _ = Task.Run(() => RunTcpServerAsync(TransportChannel.Tcp, VibrationTcpPort, ct), ct);
        _ = Task.Run(() => RunTcpServerAsync(TransportChannel.Bluetooth, BluetoothSimPort, ct), ct);
        RaiseStatus(TransportChannel.ComPort, "Disconnected");
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    public void SubmitScan(ScanEventDto scan) => ScanDataReceived?.Invoke(this, scan);

    private async Task RunNamedPipeServerAsync(CancellationToken ct)
    {
        RaiseStatus(TransportChannel.NamedPipe, "Listening");
        while (!ct.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try
            {
                await server.WaitForConnectionAsync(ct);
                RaiseStatus(TransportChannel.NamedPipe, "Connected");
                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                string? line;
                while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync()) != null)
                    Dispatch(TransportChannel.NamedPipe, line);
            }
            catch (OperationCanceledException) { break; }
            catch (IOException) { /* client disconnected mid-stream */ }
            finally
            {
                if (!ct.IsCancellationRequested)
                    RaiseStatus(TransportChannel.NamedPipe, "Listening");
            }
        }
    }

    private async Task RunTcpServerAsync(TransportChannel channel, int port, CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        RaiseStatus(channel, "Listening");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(ct);
                RaiseStatus(channel, "Connected");
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string? line;
                while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync()) != null)
                    Dispatch(channel, line);
                if (!ct.IsCancellationRequested)
                    RaiseStatus(channel, "Listening");
            }
        }
        catch (OperationCanceledException) { }
        finally { listener.Stop(); }
    }

    private void Dispatch(TransportChannel channel, string json)
    {
        try
        {
            var reading = JsonSerializer.Deserialize<SensorReadingDto>(json);
            if (reading is not null)
                SensorDataReceived?.Invoke(this, new SensorTransportEventArgs { Channel = channel, Reading = reading });
        }
        catch (JsonException) { /* ignore malformed frame */ }
    }

    private void RaiseStatus(TransportChannel channel, string status) =>
        ChannelStatusChanged?.Invoke(this, new ChannelStatusEventArgs { Channel = channel, Status = status });

    public void Dispose() => Stop();
}

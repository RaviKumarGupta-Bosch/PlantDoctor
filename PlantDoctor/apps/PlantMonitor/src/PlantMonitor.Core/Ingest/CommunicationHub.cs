using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PlantDoctor.Contracts;
using PlantMonitor.Core.Diagnostics;

namespace PlantMonitor.Core.Ingest;

/// <inheritdoc cref="ICommunicationHub"/>
public sealed class CommunicationHub : ICommunicationHub, IDisposable
{
    private readonly object _lock = new();
    private readonly Dictionary<TransportChannel, CancellationTokenSource> _running = new();

    public event EventHandler<DeviceMessageEventArgs>? MessageReceived;
    public event EventHandler<ChannelStatusEventArgs>? ChannelStatusChanged;
    public event EventHandler<ErrorEventDto>? ProcessingErrorRaised;

    public bool IsListening(TransportChannel channel)
    {
        lock (_lock) return _running.ContainsKey(channel);
    }

    public void Start()
    {
        foreach (var channel in TransportEndpoints.All) StartChannel(channel);
    }

    public void Stop()
    {
        foreach (var channel in TransportEndpoints.All) StopChannel(channel);
    }

    public void StartChannel(TransportChannel channel)
    {
        CancellationTokenSource cts;
        lock (_lock)
        {
            if (_running.ContainsKey(channel)) return;
            cts = new CancellationTokenSource();
            _running[channel] = cts;
        }
        _ = Task.Run(() => RunAsync(channel, cts.Token));
    }

    public void StopChannel(TransportChannel channel)
    {
        CancellationTokenSource? cts;
        lock (_lock)
        {
            if (!_running.Remove(channel, out cts)) return;
        }
        cts.Cancel();
        cts.Dispose();
        RaiseStatus(channel, ChannelStatus.Stopped);
    }

    private async Task RunAsync(TransportChannel channel, CancellationToken ct)
    {
        try
        {
            if (channel == TransportChannel.NamedPipe)
                await RunNamedPipeAsync(ct);
            else
                await RunTcpAsync(channel, TransportEndpoints.PortFor(channel), ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            lock (_lock) _running.Remove(channel);
            RaiseStatus(channel, ChannelStatus.Error, ex.Message);
            RaiseProcessingError(ProcessingFault.ListenerFailed, ex.Message, channel, ex);
        }
    }

    private async Task RunNamedPipeAsync(CancellationToken ct)
    {
        const TransportChannel channel = TransportChannel.NamedPipe;
        while (!ct.IsCancellationRequested)
        {
            RaiseStatus(channel, ChannelStatus.Listening);
            using var server = new NamedPipeServerStream(TransportEndpoints.TemperaturePipeName,
                PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try
            {
                await server.WaitForConnectionAsync(ct);
                RaiseStatus(channel, ChannelStatus.Connected);
                await ReadLinesAsync(channel, server, ct);
                if (!ct.IsCancellationRequested)
                    RaiseProcessingError(ProcessingFault.UnexpectedDisconnect, "Device closed the named pipe.", channel);
            }
            catch (OperationCanceledException) { break; }
            catch (IOException ex)
            {
                if (!ct.IsCancellationRequested)
                    RaiseProcessingError(ProcessingFault.UnexpectedDisconnect, ex.Message, channel);
            }
        }
    }

    private async Task RunTcpAsync(TransportChannel channel, int port, CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        // Disposing the listener/socket on cancel is what makes the device see the link drop immediately.
        using var listenerReg = ct.Register(listener.Stop);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                RaiseStatus(channel, ChannelStatus.Listening);
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(ct);
                }
                catch (Exception) when (ct.IsCancellationRequested) { break; }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }

                using (client)
                using (ct.Register(client.Dispose))
                {
                    RaiseStatus(channel, ChannelStatus.Connected);
                    try
                    {
                        await ReadLinesAsync(channel, client.GetStream(), ct);
                        if (!ct.IsCancellationRequested)
                            RaiseProcessingError(ProcessingFault.UnexpectedDisconnect, "Device closed the socket.", channel);
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        RaiseProcessingError(ProcessingFault.UnexpectedDisconnect, ex.Message, channel);
                    }
                }
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task ReadLinesAsync(TransportChannel channel, Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;

            if (!WireFormat.TryParse(line, out var message))
            {
                RaiseProcessingError(ProcessingFault.MalformedFrame, Truncate(line), channel);
                continue;
            }

            if (!IsPayloadPresent(message))
            {
                RaiseProcessingError(ProcessingFault.MissingPayload, $"type='{message.Type}'", channel);
                continue;
            }

            if (!IsKnownType(message))
            {
                RaiseProcessingError(ProcessingFault.UnknownMessageType, $"type='{message.Type}'", channel);
                continue;
            }

            var expectedDevice = DiagnosticCodes.DeviceNameFor(channel);
            if (!string.IsNullOrEmpty(message.Device) &&
                !message.Device.Equals(expectedDevice, StringComparison.OrdinalIgnoreCase))
            {
                RaiseProcessingError(ProcessingFault.UnknownDevice,
                    $"'{message.Device}' reported on the {expectedDevice} channel.", channel);
                continue;
            }

            MessageReceived?.Invoke(this, new DeviceMessageEventArgs { Channel = channel, Message = message });
        }
    }

    private static bool IsKnownType(DeviceMessage message) => message.Type is
        DeviceMessageTypes.Sensor or DeviceMessageTypes.Scan or DeviceMessageTypes.Error;

    private static bool IsPayloadPresent(DeviceMessage message) => message.Type switch
    {
        DeviceMessageTypes.Sensor => message.Reading is not null,
        DeviceMessageTypes.Scan => message.Scan is not null,
        DeviceMessageTypes.Error => message.Error is not null,
        _ => true
    };

    private static string Truncate(string line) =>
        line.Length <= 120 ? line : line[..120] + "…";

    private void RaiseProcessingError(ProcessingFault fault, string? detail,
        TransportChannel? channel = null, Exception? exception = null) =>
        ProcessingErrorRaised?.Invoke(this,
            DiagnosticEvents.FromApplication(fault, detail, channel, exception));

    private void RaiseStatus(TransportChannel channel, string status, string? detail = null) =>
        ChannelStatusChanged?.Invoke(this, new ChannelStatusEventArgs
        {
            Channel = channel,
            Status = status,
            Detail = detail
        });

    public void Dispose() => Stop();
}

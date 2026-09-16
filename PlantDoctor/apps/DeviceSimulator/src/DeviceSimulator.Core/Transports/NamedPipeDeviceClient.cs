using System.IO.Pipes;
using System.Text;
using PlantDoctor.Contracts;

namespace DeviceSimulator.Core.Transports;

/// <summary>Temperature device link: a persistent named-pipe stream to the main application.</summary>
public sealed class NamedPipeDeviceClient : IDeviceTransportClient
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private NamedPipeClientStream? _pipe;
    private StreamWriter? _writer;

    public TransportChannel Channel => TransportChannel.NamedPipe;
    public string Label => TransportEndpoints.LabelFor(Channel);
    public string Endpoint => TransportEndpoints.DescribeFor(Channel);
    public bool IsConnected => _pipe?.IsConnected == true;
    public string? LastError { get; private set; }

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (IsConnected) return true;
            Close();
            var pipe = new NamedPipeClientStream(".", TransportEndpoints.TemperaturePipeName,
                PipeDirection.Out, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(1000, ct);
            _pipe = pipe;
            _writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
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
                return false;
            }
            await _writer.WriteLineAsync(WireFormat.Serialize(message).AsMemory(), ct);
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
        try
        {
            _writer?.Dispose();
        }
        catch (IOException) when (_pipe?.IsConnected == false)
        {
            // Pipe is already broken - ignore flush error
        }
        _writer = null;
        try
        {
            _pipe?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }
        _pipe = null;
    }

    public void Dispose()
    {
        Disconnect();
        _gate.Dispose();
    }
}

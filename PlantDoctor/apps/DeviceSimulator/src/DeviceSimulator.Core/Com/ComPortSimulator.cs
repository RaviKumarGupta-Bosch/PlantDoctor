using PlantDoctor.Contracts;

namespace DeviceSimulator.Core.Com;

public sealed class ComPortSimulator : IComPortSimulator
{
    private readonly Random _rng = new();
    public string? CurrentPort { get; private set; }
    public string Status { get; private set; } = ChannelStatus.Closed;
    public event EventHandler<ComEventDto>? Event;

    public void Connect(string port)
    {
        CurrentPort = port;
        Status = ChannelStatus.Connected;
        Raise(null);
    }

    public void Disconnect()
    {
        Status = ChannelStatus.Closed;
        Raise(null);
    }

    public void InjectDisconnect()
    {
        Status = ChannelStatus.Error;
        Raise("dropped mid-transmission");
    }

    public string NextFrame()
    {
        var bytes = new byte[8];
        _rng.NextBytes(bytes);
        var frame = string.Join(' ', bytes.Select(b => b.ToString("X2")));
        return (_rng.Next(2) == 0 ? "TX: " : "RX: ") + frame;
    }

    private void Raise(string? rawFrame) => Event?.Invoke(this, new ComEventDto
    {
        TimestampUtc = DateTime.UtcNow,
        Port = CurrentPort ?? "-",
        Status = Status,
        RawFrame = rawFrame
    });
}

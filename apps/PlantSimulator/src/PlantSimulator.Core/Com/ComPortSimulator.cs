using PlantSimulator.Contracts;

namespace PlantSimulator.Core.Com;

public sealed class ComPortSimulator : IComPortSimulator
{
    private readonly Random _rng = new();
    public string? CurrentPort { get; private set; }
    public string Status { get; private set; } = "Disconnected";
    public event EventHandler<ComEventDto>? Event;

    public void Connect(string port)
    {
        CurrentPort = port;
        Status = "Connected";
        Raise(null);
    }

    public void Disconnect()
    {
        Status = "Disconnected";
        Raise(null);
    }

    public void InjectDisconnect()
    {
        Status = "Error";
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

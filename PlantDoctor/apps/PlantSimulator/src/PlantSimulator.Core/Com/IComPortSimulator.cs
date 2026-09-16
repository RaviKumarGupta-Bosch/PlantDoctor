using PlantSimulator.Contracts;

namespace PlantSimulator.Core.Com;

public interface IComPortSimulator
{
    string? CurrentPort { get; }
    string Status { get; }
    event EventHandler<ComEventDto>? Event;

    void Connect(string port);
    void Disconnect();
    void InjectDisconnect();
    string NextFrame();
}

# Device Simulator

Simulates the plant's field devices — three sensors and a barcode scanner — in **one** application.
Each device streams to the **Plant Monitor** main application over its own transport, and each one
can be closed independently from the screen.

## Run

Start `PlantMonitor` first (it owns the listeners), then:

```powershell
dotnet build .\DeviceSimulator.sln
dotnet run --project .\src\DeviceSimulator.UI\DeviceSimulator.UI.csproj
```

## Devices

| Device | Transport | Endpoint |
| ------ | --------- | -------- |
| 🌡️ Temperature | Named Pipe | `\\.\pipe\PlantDoctor.Temperature` |
| 📳 Vibration | TCP Socket | `tcp://127.0.0.1:51001` |
| 💨 Pressure | Bluetooth (simulated RFCOMM over loopback + air latency) | `bt-sim://127.0.0.1:51002` |
| 🔍 Scanner | COM port bridged over loopback | `com-bridge://127.0.0.1:51003` |

## Controls

| Control | Behaviour |
| ------- | --------- |
| **Open** / **Close** (per device card) | Opens or tears down that one device's link. Closing is visible to Plant Monitor as a dropped connection. |
| **Close Sensors** | Closes Temperature, Vibration and Pressure; the scanner keeps running. |
| **Close Scanner** | Closes only the scanner. |
| **Close All** / **Open All** | Master switches for all four devices. |
| **Emit interval (ms)** | How often every open device publishes a sample. |
| **Scanner Port** | COM port reported in the scan payload. Applied on the next Open. |

Fault-injection buttons (Sensor Timeout, COM Disconnect, Overflow, Out of Range) live here because
faults originate at the device. Each injected fault is sent to Plant Monitor over the affected
device's own channel, where it lands in the error log and the JSONL file.

## Wire format

Every channel carries newline-delimited JSON `DeviceMessage` envelopes:

```json
{"type":"sensor","device":"Temperature","reading":{"Name":"Temperature","Value":74.4,"Unit":"°C","Status":"OK","TimestampUtc":"..."}}
{"type":"scan","device":"Scanner","scan":{"TimestampUtc":"...","Port":"COM3","Code":"SKU-12345","CodeType":"QR"}}
{"type":"error","device":"Scanner","error":{"ErrorCode":"COM_DROPPED","Level":"Critical","Message":"..."}}
```

`type` is `sensor`, `scan` or `error`. The envelope and endpoint addresses are defined in
`..\Shared\PlantDoctor.Contracts` and shared with Plant Monitor — both apps must agree on them.

## Projects

| Project | Role |
| ------- | ---- |
| `DeviceSimulator.Core` | Sensor random-walk simulation, scanner, COM simulation, fault injection, transport clients |
| `DeviceSimulator.UI` | WPF/MVVM shell: device blocks, emitted-data feed, fault injection |
| `DeviceSimulator.Tests` | xUnit tests for the simulators, transports and wire format |

## Offline guarantee

No `HttpClient`, no cloud SDKs. Everything is loopback IPC. This app writes **no** log file —
the JSONL integration contract belongs to Plant Monitor alone.

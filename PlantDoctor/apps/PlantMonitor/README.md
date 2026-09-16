# Plant Monitor (main application)

The plant-floor main application. It listens for the **Device Simulator**'s sensors and scanner,
shows each connection's live state and emitted data, and writes the JSONL log that
`PlantDoctor.Agent` consumes.

## Run

Start this app **first**, then launch Device Simulator.

```powershell
dotnet build .\PlantMonitor.sln
dotnet run --project .\src\PlantMonitor.UI\PlantMonitor.UI.csproj
```

## Screen

| Area | Shows |
| ---- | ----- |
| **Connection cards** (top) | One card per inbound device channel: transport, endpoint, status light (Stopped / Listening / Connected / Error), last value and received-message count. |
| **Sensor Data** | Live grid of the latest reading per sensor, as emitted by the devices. |
| **Scanner Data** | Rolling feed of barcode/QR scans. |
| **Error & Event Log** | Faults reported by the devices over their own channels. |

## Disconnect controls

Disconnecting stops that channel's listener, which drops the device's socket — the Device
Simulator sees the link die and flags the device red.

| Control | Behaviour |
| ------- | --------- |
| **Connect** / **Disconnect** (per card) | Starts or stops one channel. |
| **Disconnect Sensors** | Stops Temperature, Vibration and Pressure; the scanner keeps streaming. |
| **Disconnect Scanner** | Stops only the scanner channel. |
| **Disconnect All** / **Reconnect All** | Master switches. |

## Listening endpoints

| Device | Transport | Endpoint |
| ------ | --------- | -------- |
| Temperature | Named Pipe | `\\.\pipe\PlantDoctor.Temperature` |
| Vibration | TCP Socket | `tcp://127.0.0.1:51001` |
| Pressure | Bluetooth (simulated) | `bt-sim://127.0.0.1:51002` |
| Scanner | COM port bridged | `com-bridge://127.0.0.1:51003` |

## JSONL log — the integration contract

`%LOCALAPPDATA%\PlantDoctor\logs\plant-YYYYMMDD.jsonl`, one JSON object per line:

```json
{
  "timestamp": "2026-01-01T12:00:00Z",
  "level": "Info|Warning|Error|Critical",
  "source": "Sensor|COM|Scanner|Application",
  "sensorName": "optional",
  "value": "optional",
  "errorCode": "E101",
  "deviceErrorCode": "SENSOR_TIMEOUT",
  "message": "human readable",
  "stackTrace": "optional, full .NET stack trace if exception"
}
```

`errorCode` is this application's catalog code (below). `deviceErrorCode` preserves the semantic
code the device itself reported, so nothing is lost in translation.

`PlantDoctor.Agent` (App 2) tails this file with a `FileSystemWatcher`. **This app is the only
writer** — the Device Simulator writes nothing.

## Diagnostic code catalog

Codes are `E` + a device block digit + a two-digit fault number. The block identifies **which
device**; the fault number means the same thing on every device.

| Block | Source |
| ----- | ------ |
| `E1nn` | Temperature sensor |
| `E2nn` | Vibration sensor |
| `E3nn` | Pressure sensor |
| `E4nn` | Scanner |
| `E9nn` | Main application processing |

### Device faults (`nn` is identical across all four devices)

| `nn` | Fault | Temperature | Vibration | Pressure | Scanner |
| ---- | ----- | ----------- | --------- | -------- | ------- |
| 01 | Did not report within the expected interval | `E101` | `E201` | `E301` | `E401` |
| 02 | Reported a value outside its valid bounds | `E102` | `E202` | `E302` | `E402` |
| 03 | Dropped its communication link mid-transmission | `E103` | `E203` | `E303` | `E403` |
| 04 | Raised an unhandled exception | `E104` | `E204` | `E304` | `E404` |
| 05 | Lost its link to the main application | `E105` | `E205` | `E305` | `E405` |
| 99 | Unclassified device fault | `E199` | `E299` | `E399` | `E499` |

Device codes are mapped from what the device reports: `SENSOR_TIMEOUT` → 01, `OUT_OF_RANGE` → 02,
`COM_DROPPED` → 03, `UNHANDLED_EXCEPTION` → 04. Anything unrecognised becomes `99`, so a new device
fault is still logged with a usable code.

### Main application processing faults

| Code | Meaning |
| ---- | ------- |
| `E901` | Received a frame that is not valid JSON |
| `E902` | Received a frame with an unrecognised message type |
| `E903` | Received a frame whose payload was missing for its declared type |
| `E904` | Received data on a channel with no mapped device (device/channel mismatch) |
| `E905` | A device channel listener failed and stopped accepting connections |
| `E906` | A connected device dropped without being disconnected by the operator |
| `E907` | Failed to write an entry to the diagnostic log file |
| `E999` | Unhandled error while processing device data |

The catalog lives in `PlantMonitor.Core/Diagnostics/DiagnosticCodes.cs`; `DiagnosticCodes.All()`
enumerates every code with its source and description.

## Projects

| Project | Role |
| ------- | ---- |
| `PlantMonitor.Core` | `CommunicationHub` ingestion (one independently startable listener per channel), the diagnostic code catalog, and the JSONL logger |
| `PlantMonitor.ServiceHost` | CoreWCF net.pipe host of `IPlantMonitorService`; single writer of the JSONL log |
| `PlantMonitor.UI` | WPF/MVVM shell: connection cards, sensor/scan/error panels |
| `PlantMonitor.Tests` | xUnit tests for the hub, logger and monitor service |

## WCF contract

`IPlantMonitorService` is hosted at `net.pipe://localhost/PlantDoctor/monitor` using CoreWCF, so the
UI stays decoupled from device communication the way real plant software is. Future extension: expose
`net.tcp` so `PlantDoctor.Agent` can subscribe cross-process (see `ServiceHostBuilder.cs`).

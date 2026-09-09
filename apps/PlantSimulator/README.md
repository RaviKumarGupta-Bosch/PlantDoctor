# PlantSimulator (App 1)

Offline WPF + WCF factory simulator. Emits JSONL logs to `%LOCALAPPDATA%\PlantDoctor\logs\plant-YYYYMMDD.jsonl` — this file is the **integration contract** with `PlantDoctor.Agent` (App 2).

## Run

```powershell
dotnet build .\PlantSimulator.sln
dotnet run --project .\src\PlantSimulator.UI\PlantSimulator.UI.csproj
```

## Projects

| Project                       | Role                                        |
| ----------------------------- | ------------------------------------------- |
| `PlantSimulator.Contracts`    | WCF `[ServiceContract]` + DTOs              |
| `PlantSimulator.Core`         | Sensor sim, COM sim, error injection, JSONL logger |
| `PlantSimulator.ServiceHost`  | CoreWCF net.pipe self-host of `IPlantMonitorService` |
| `PlantSimulator.UI`           | WPF/MVVM shell (three-panel layout)         |
| `PlantSimulator.Tests`        | xUnit tests for Core                        |

## Log line schema (JSONL)

```json
{"timestamp":"ISO-8601","level":"Info|Warning|Error|Critical","source":"Sensor|COM|Application","sensorName":"...","value":"...","errorCode":"...","message":"...","stackTrace":"..."}
```

## WCF contract

`IPlantMonitorService` is hosted at `net.pipe://localhost/PlantDoctor/monitor`. Currently in-process; can be re-exposed over `net.tcp` if App 2 needs to subscribe cross-process (see `ServiceHostBuilder.cs`).

## Offline guarantee

No `HttpClient`, no `WebRequest`, no cloud SDKs. Serilog uses file sink only.

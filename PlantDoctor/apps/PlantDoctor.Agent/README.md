# PlantDoctor.Agent (App 2)

Offline WPF agent that tails PlantSimulator's JSONL logs, reasons about them via a local Ollama LLM, and exports a **DiagnosticArtifact** JSON for handoff to the online Portal.

## Prerequisites

1. Install [Ollama](https://ollama.com/) and start it (`http://localhost:11434`).
2. Pull a model:
   ```powershell
   ollama pull llama3.1:8b
   ```
3. Run PlantSimulator so logs exist at `%LOCALAPPDATA%\PlantDoctor\logs\`.

## Run

```powershell
dotnet build .\PlantDoctor.Agent.sln
dotnet run --project .\src\PlantDoctor.Agent.UI\PlantDoctor.Agent.UI.csproj
```

## Projects

| Project                     | Role                                         |
| --------------------------- | -------------------------------------------- |
| `PlantDoctor.Agent.Core`    | LogEntry model, JSONL parser, tailer, PromptBuilder, OllamaClient, ArtifactWriter |
| `PlantDoctor.Agent.UI`      | WPF/MVVM shell (live log • chat • incidents) |
| `PlantDoctor.Agent.Tests`   | xUnit tests                                  |

## DiagnosticArtifact schema

See [`../../docs/contracts/diagnostic-artifact.schema.json`](../../docs/contracts/diagnostic-artifact.schema.json). This file is the single source of truth linking App 2 and App 3.

## Offline guarantee

The only outbound HTTP is loopback to `http://localhost:11434` (Ollama). No cloud SDKs. The exported artifact file is the sole bridge to the online world.

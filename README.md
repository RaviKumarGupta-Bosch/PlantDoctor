# PlantDoctor

PlantDoctor is an AI-powered industrial diagnostics platform that monitors plant logs, automatically detects issues, explains root causes using AI, and delivers actionable diagnostics from edge systems to enterprise insights.

## Repository layout

```
PlantDoctor/
├── apps/
│   ├── PlantSimulator/          # App 1 — WPF + WCF (.NET 8), fully offline
│   ├── PlantDoctor.Agent/       # App 2 — WPF + Ollama (.NET 8), offline AI
│   └── PlantDoctor.Portal/      # App 3 — React + Node/Express, online (Gemini + SAP BTP)
├── docs/
│   ├── architecture.md
│   └── contracts/
│       └── diagnostic-artifact.schema.json   # Single source of truth for App 2 ↔ App 3
└── README.md
```

## Data flow

```
[PlantSimulator] --JSONL logs--> [PlantDoctor.Agent] --artifact.json--> [PlantDoctor.Portal] --> Gemini --> SAP BTP
     offline                          offline (Ollama)        file transfer         online
```

## Conventions (all apps)

- Enterprise layering: UI → Application/Services → Domain/Core → Infrastructure.
- DI: `Microsoft.Extensions.DependencyInjection` (.NET); explicit composition root (Node).
- MVVM in WPF via `CommunityToolkit.Mvvm`.
- Contracts first: `docs/contracts/diagnostic-artifact.schema.json` is the shared contract.
- No secrets in source. `.env` (Portal), `appsettings.json` (.NET apps).
- Tests per app: `dotnet test` / `npm test`.

See each app's `README.md` for run instructions.


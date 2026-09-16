# 🤖 Coding Agent Prompts — PlantDoctor 3-App System
## Ready to paste into GitHub Copilot / Copilot Chat / Copilot Workspace

---

## 📦 System Overview (for your own reference)

```
APP 1: Plant Simulator (.NET, WPF + WCF)
  → Simulates a factory app: sensor readings, COM port connection, errors
  → Runs 100% OFFLINE

APP 2: AI Agent (Offline, Ollama)
  → Watches App 1's logs/connections
  → Local LLM reasons about errors, chats with operator
  → Exports a "diagnostic artifact" (JSON) — bridge to the online world

APP 3: Web Portal (Online, Gemini + SAP BTP)
  → Developer uploads the artifact file
  → Gemini analyzes it → returns structured JSON (root cause, fix, etc.)
  → That JSON is pushed to SAP BTP (HANA/CAP endpoint)
```

Each prompt below is self-contained — copy the whole block for one app into Copilot Chat (or Copilot Workspace / Copilot Agent mode) as your task description. They're written the way Copilot responds best to: context → requirements → structure → constraints → acceptance criteria.

---

## 🏭 PROMPT 1 — Plant Simulator (WPF + WCF, .NET)

```
ROLE
You are a senior .NET engineer. Build a Windows desktop application that
simulates an industrial plant's control software for a hackathon demo
called "PlantDoctor". It must run completely offline.

PROJECT NAME
PlantDoctor.PlantSimulator

TECH STACK
- .NET 8 (or .NET Framework 4.8 if targeting older industrial PCs — ask me
  which, default to .NET 8)
- WPF for the UI (single desktop app)
- WCF (self-hosted, net.pipe or net.tcp binding) as the internal service
  layer between the UI and the "hardware" simulation layer — this mimics
  how real plant software separates the UI from device communication
- No external NuGet packages that require internet access at runtime
- No cloud calls anywhere in this app — must work fully air-gapped

SOLUTION STRUCTURE
Create a solution with these projects:
1. PlantSimulator.UI          (WPF, .exe, startup project)
2. PlantSimulator.ServiceHost (WCF self-host, class library referenced by UI,
   hosted in-process via ServiceHost)
3. PlantSimulator.Contracts   (WCF [ServiceContract]/[DataContract] interfaces,
   shared DTOs)
4. PlantSimulator.Core        (business logic: sensor simulation, COM port
   simulation, error injection, logging)
5. PlantSimulator.Tests       (xUnit tests for Core)

FUNCTIONAL REQUIREMENTS

1. MAIN WINDOW (WPF)
   Three-panel layout using a Grid:
   - LEFT panel: "Sensor Readings" — a live list of 5-8 simulated sensors
     (e.g. Temperature, Pressure, VibrationX, VibrationY, RPM, Voltage).
     Each row shows: sensor name, current value, unit, status indicator
     (green/amber/red dot bound via IValueConverter based on threshold).
     Values update every 1-2 seconds via a DispatcherTimer, with small
     randomized fluctuation (use a simple random-walk, not pure random,
     so it looks realistic).
   - CENTER panel: "COM Port Connection" — shows a simulated serial/COM
     connection status panel:
       - Dropdown to pick a virtual COM port (COM1–COM8, just strings, no
         real hardware needed)
       - Connect / Disconnect buttons
       - Connection status light (Connected / Disconnected / Error)
       - A scrolling raw log window showing simulated bytes/telegrams
         being sent/received (e.g. "TX: 01 03 00 00 00 02 C4 0B",
         "RX: 01 03 04 00 96 00 00 FA F3") on a timer, to look like real
         Modbus/serial traffic
   - RIGHT panel: "Error & Event Log" — a DataGrid bound to an
     ObservableCollection<ErrorEvent> showing: Timestamp, Severity
     (Info/Warning/Error/Critical), Source (Sensor/COM/Application),
     Message, ErrorCode. New rows appear when errors are injected.

2. ERROR INJECTION (for demo purposes)
   Add a "Simulate Error" menu/toolbar with buttons to inject specific
   fault scenarios on demand, e.g.:
     - "Sensor Timeout"      → a sensor stops updating, status goes red,
                                error event logged
     - "COM Port Disconnect" → simulate a dropped serial connection mid-
                                transmission, error event logged
     - "Overflow Exception"  → simulate an unhandled exception in a
                                background worker (catch it, log full
                                stack trace to the error log AND to a
                                rolling file log)
     - "Out of Range Value"  → a sensor reports a value outside valid
                                bounds (e.g. Temperature = 9999°C)
   Each injected error must:
     a) Be pushed to the WCF service layer via a one-way operation
     b) Be persisted to a local rolling log file (see Logging below)
     c) Appear immediately in the UI's error DataGrid

3. WCF SERVICE LAYER
   Define a contract, e.g.:
     [ServiceContract]
     public interface IPlantMonitorService
     {
         [OperationContract] void ReportSensorReading(SensorReadingDto dto);
         [OperationContract] void ReportError(ErrorEventDto dto);
         [OperationContract] void ReportComEvent(ComEventDto dto);
         [OperationContract] PlantSnapshotDto GetCurrentSnapshot();
     }
   Self-host this with a ServiceHost inside the WPF app (net.pipe binding
   is fine — no real network needed). The point is to demonstrate a
   realistic UI/service separation pattern that mirrors real plant
   software architecture, and to give the future "AI Agent" app (App 2)
   a well-defined service boundary to hook into later (either via reading
   the same log files, or optionally exposing net.tcp for future
   cross-process access — add a comment noting this extension point).

4. LOGGING (critical — this is what App 2 will read)
   Write structured JSON-lines logs to a local folder, e.g.
   %LOCALAPPDATA%\PlantDoctor\logs\plant-YYYYMMDD.jsonl
   Each line is one JSON object with fields:
     {
       "timestamp": "ISO-8601",
       "level": "Info|Warning|Error|Critical",
       "source": "Sensor|COM|Application",
       "sensorName": "optional",
       "value": "optional",
       "errorCode": "optional",
       "message": "human readable",
       "stackTrace": "optional, full .NET stack trace if exception"
     }
   Use Serilog (offline file sink only, no cloud sinks) configured for
   rolling daily files. This log file is the PRIMARY integration point
   with the AI Agent app — document this clearly in a README.

5. SETTINGS / ABOUT
   Simple settings panel to adjust:
     - Sensor update interval (ms)
     - Log folder path
     - Number of active sensors

NON-FUNCTIONAL REQUIREMENTS
- MVVM pattern throughout (ICommand via RelayCommand/DelegateCommand,
  INotifyPropertyChanged, no code-behind logic beyond view wiring)
- Use CommunityToolkit.Mvvm for MVVM boilerplate (offline NuGet package,
  fine to reference at build time)
- Dependency injection via Microsoft.Extensions.DependencyInjection
- Clean separation: ViewModels never reference WCF contracts directly —
  go through a service abstraction (IPlantMonitorClient)
- All async work must not block the UI thread
- Include XML doc comments on public members
- Add a README.md explaining:
    - How to run
    - Where logs are written (this is the handoff point to App 2)
    - What each panel demonstrates
    - The WCF contract, and why it mirrors real industrial software

DELIVERABLES
- Full solution buildable with `dotnet build`
- README.md with run instructions and architecture diagram (mermaid or
  ASCII)
- At least 5 unit tests in PlantSimulator.Tests covering the error
  injection and sensor simulation logic

ACCEPTANCE CRITERIA
- App launches, shows live sensor updates within 2 seconds
- Clicking each "Simulate Error" button produces a new row in the error
  log grid AND a new line in the JSONL log file within 1 second
- Disconnecting/reconnecting COM updates the status light correctly
- No internet/network calls anywhere (verify by code review — flag any
  HttpClient/WebRequest usage)

Please scaffold the full solution now, file by file, starting with the
solution file and project structure, then Contracts, then Core, then
ServiceHost, then the WPF UI (App.xaml, MainWindow.xaml + .cs, and each
ViewModel). Ask me before assuming which .NET version if unsure.
```

---

## 🤖 PROMPT 2 — Offline AI Agent (Ollama)

```
ROLE
You are a senior .NET/AI engineer. Build a Windows desktop AI agent
application called "PlantDoctor.Agent" that runs completely offline,
using a local LLM (via Ollama) to monitor and reason about the plant
simulator app's logs and connection state (from the app called
PlantSimulator, which writes JSONL logs to
%LOCALAPPDATA%\PlantDoctor\logs\).

GOAL
This agent watches the plant's logs in real time, uses a local LLM to
detect and explain problems, lets a plant operator chat with it in plain
language, and — when asked — exports a single "diagnostic artifact" JSON
file that fully describes an incident. That artifact is later uploaded
to a separate online web portal (a different app, out of scope here) for
deeper cloud-based analysis. Your app's only bridge to the outside world
is that exported file — this app itself must have ZERO internet/cloud
calls.

TECH STACK
- .NET 8
- WPF for UI (same look-and-feel family as PlantSimulator: clean,
  industrial, dark-on-light or light-on-dark, your call — keep it
  professional)
- Ollama running locally (assume it's already installed and running on
  http://localhost:11434 — call its REST API directly via HttpClient;
  this is a LOCAL loopback call, not internet, so it's allowed)
- Use a small, fast local model by default, e.g. "llama3.1:8b" or
  "phi3:mini" — make the model name configurable in settings
- FileSystemWatcher to tail the plant's JSONL log folder in real time
- System.Text.Json for all serialization
- CommunityToolkit.Mvvm + Microsoft.Extensions.DependencyInjection,
  matching the architecture style of the PlantSimulator app for
  consistency

SOLUTION STRUCTURE
1. PlantDoctor.Agent.UI       (WPF, startup project)
2. PlantDoctor.Agent.Core     (log tailing, context building, artifact
                                model, Ollama client)
3. PlantDoctor.Agent.Tests    (xUnit)

FUNCTIONAL REQUIREMENTS

1. LOG MONITORING
   - On startup, locate and tail the newest .jsonl file in
     %LOCALAPPDATA%\PlantDoctor\logs\ using FileSystemWatcher +
     incremental read (handle file rollover at midnight).
   - Parse each JSON line into a LogEntry model (mirror the schema from
     PlantSimulator: timestamp, level, source, sensorName, value,
     errorCode, message, stackTrace).
   - Maintain an in-memory rolling buffer of the last N (e.g. 500) log
     entries for context, plus a separate "flagged incidents" list for
     anything level >= Warning.

2. MAIN WINDOW — THREE AREAS
   a) LIVE LOG STREAM (left/top) — auto-scrolling list of incoming log
      entries, color-coded by severity, exactly like a real log tailer.
   b) AI CHAT PANEL (center) — a chat interface (bubbles, like a
      messenger) where the operator can type free-text questions, e.g.
      "why did sensor 3 go red?" or "what happened at 10:42?". Send the
      relevant recent log context + the user's message to the local
      Ollama model and stream the response back into the chat.
   c) INCIDENT PANEL (right) — whenever the agent auto-detects a
      Warning/Error/Critical entry, it should:
        - Proactively summarize what happened in plain English (call
          Ollama automatically, no need to wait for the operator to ask)
        - Show a card: severity, source, AI's one-paragraph explanation,
          and a "Export Diagnostic Artifact" button

3. AI REASONING LOGIC (Core)
   - Build a PromptBuilder that assembles a system prompt + relevant log
     context (last N entries around the incident, sensor readings, COM
     state) into a well-structured prompt for Ollama.
   - System prompt should instruct the model to act as an "industrial
     diagnostics assistant" — explain likely root causes in plain
     language, suggest what a developer should look at, and be concise.
   - Call Ollama's /api/chat or /api/generate endpoint (use streaming if
     straightforward, otherwise a single completion is fine for MVP).
   - Wrap all Ollama calls in a resilience layer (retry once, graceful
     error message in UI if Ollama isn't running — detect this and show
     a clear "Ollama not detected — please start Ollama" banner rather
     than crashing).

4. DIAGNOSTIC ARTIFACT EXPORT (the critical hand-off feature)
   When the operator clicks "Export Diagnostic Artifact", generate a
   single JSON file with this shape (this exact shape will be consumed
   later by a separate web app, so keep field names stable and clear):

   {
     "artifactId": "guid",
     "generatedAtUtc": "ISO-8601",
     "plantId": "string, from settings, default 'DEMO-PLANT-01'",
     "incident": {
       "detectedAtUtc": "ISO-8601",
       "severity": "Warning|Error|Critical",
       "source": "Sensor|COM|Application",
       "errorCode": "string or null",
       "primaryMessage": "string"
     },
     "recentLogEntries": [ /* array of LogEntry, last ~50 around the incident */ ],
     "sensorSnapshot": [ /* last known reading per sensor, name/value/unit/status */ ],
     "comConnectionState": {
       "port": "string",
       "status": "Connected|Disconnected|Error",
       "lastEventUtc": "ISO-8601"
     },
     "aiAnalysis": {
       "modelUsed": "e.g. llama3.1:8b",
       "summary": "the AI's plain-English explanation",
       "suspectedRootCause": "string",
       "confidence": "Low|Medium|High"
     },
     "operatorChatTranscript": [
       { "role": "operator|assistant", "message": "string", "timestampUtc": "ISO-8601" }
     ]
   }

   - Save this as a formatted (indented) .json file via a SaveFileDialog,
     default filename pattern: "diagnostic-artifact-{plantId}-{timestamp}.json"
   - After saving, show a confirmation with the file path and a note:
     "Transfer this file via USB, email, or file share to upload it to
     the PlantDoctor web portal for deeper analysis."
   - Also produce a .zip of the same artifact (System.IO.Compression) as
     an alternative export option, for consistency with the docs that
     describe USB/email transfer of a compressed artifact.

5. SETTINGS
   - Ollama base URL (default http://localhost:11434)
   - Model name (default configurable, e.g. "llama3.1:8b")
   - Plant ID string
   - Log folder path (default matches PlantSimulator's path)

NON-FUNCTIONAL REQUIREMENTS
- MVVM throughout, same conventions as PlantSimulator app
- NO internet calls anywhere except http://localhost:11434 (loopback to
  Ollama) — flag this explicitly in code comments and README
- Handle Ollama being offline/not installed gracefully (don't crash)
- Include XML doc comments on public members
- README.md explaining:
    - Prerequisite: Ollama installed + a model pulled (e.g.
      `ollama pull llama3.1:8b`)
    - How the app discovers PlantSimulator's logs
    - The exact JSON schema of the exported artifact (copy the schema
      above into the README verbatim so it's a contract document)
    - How to run

DELIVERABLES
- Full solution buildable with `dotnet build`
- README.md with the artifact JSON schema as a first-class documented
  contract (this is what the next app depends on)
- At least 5 unit tests covering: log parsing, artifact JSON generation
  (schema correctness), and PromptBuilder context assembly (mock the
  Ollama HTTP call, don't hit a real Ollama instance in tests)

ACCEPTANCE CRITERIA
- Starting the app with PlantSimulator already running and generating
  logs shows live entries appearing within 2 seconds
- Triggering an error in PlantSimulator surfaces an incident card with
  an AI-generated summary within ~10 seconds (allow for local LLM
  latency)
- Clicking Export produces a valid JSON file matching the schema exactly
- If Ollama is not running, the app shows a clear non-crashing error
  state

Please scaffold the full solution now, starting with the solution file
and Core project (LogEntry, Artifact model, PromptBuilder, OllamaClient),
then the UI (MainWindow.xaml + ViewModels + chat control). Ask me before
assuming anything about the Ollama API shape if you're not certain of
current endpoint behavior — describe your assumption clearly.
```

---

## ☁️ PROMPT 3 — Web Portal (Upload → Gemini → SAP BTP)

```
ROLE
You are a senior full-stack engineer. Build a web application called
"PlantDoctor.Portal" that a developer uses to upload a diagnostic
artifact file (JSON, produced by a separate offline desktop agent app —
schema given below), have it analyzed by an online AI model (Google
Gemini), receive a structured JSON analysis back, and then push that
analysis JSON onward to an SAP BTP endpoint for storage/dashboarding.

GOAL / FLOW
  1. Developer opens the web app, drags & drops (or browses) a
     diagnostic-artifact-*.json file (or .zip containing one)
  2. Backend parses/validates the artifact against the known schema
  3. Backend sends the artifact content to the Gemini API with a
     structured prompt, requesting a JSON response containing root
     cause, fix location, code recommendations, severity classification
  4. Backend returns that structured JSON to the frontend, which
     displays it nicely (cards, syntax-highlighted code suggestions,
     etc.)
  5. User clicks "Send to SAP BTP" — backend POSTs the combined result
     (original artifact metadata + Gemini's analysis JSON) to a
     configured SAP BTP HTTP endpoint (e.g. a SAP CAP OData/REST service
     or SAP Cloud Integration endpoint), with OAuth2 client-credentials
     auth
  6. UI shows success/failure and the resulting record ID from SAP BTP

TECH STACK (pick ONE stack, ask me if unsure — default to option A)
  OPTION A (recommended default):
    - Frontend: React + TypeScript + Vite, Tailwind CSS
    - Backend: Node.js + Express + TypeScript
    - File upload: multer, max 10MB, validate .json/.zip only
  OPTION B (if I say ".NET"):
    - Frontend: React + TypeScript + Vite, Tailwind CSS (same)
    - Backend: ASP.NET Core Web API (.NET 8)
  Default to Option A unless told otherwise.

PROJECT STRUCTURE (Option A)
  /portal
    /client          → React + TS + Vite app
    /server          → Express + TS API
    /shared          → shared TypeScript types (artifact schema, Gemini
                        result schema) used by both client and server
    docker-compose.yml (optional, for local dev convenience)

INPUT SCHEMA — DIAGNOSTIC ARTIFACT (from the offline Agent app; treat as
a fixed external contract, define as a shared TypeScript interface)

  interface DiagnosticArtifact {
    artifactId: string;
    generatedAtUtc: string;
    plantId: string;
    incident: {
      detectedAtUtc: string;
      severity: "Warning" | "Error" | "Critical";
      source: "Sensor" | "COM" | "Application";
      errorCode: string | null;
      primaryMessage: string;
    };
    recentLogEntries: Array<{
      timestamp: string;
      level: string;
      source: string;
      sensorName?: string;
      value?: string;
      errorCode?: string;
      message: string;
      stackTrace?: string;
    }>;
    sensorSnapshot: Array<{
      name: string; value: number; unit: string; status: string;
    }>;
    comConnectionState: {
      port: string; status: string; lastEventUtc: string;
    };
    aiAnalysis: {
      modelUsed: string; summary: string;
      suspectedRootCause: string; confidence: "Low" | "Medium" | "High";
    };
    operatorChatTranscript: Array<{
      role: "operator" | "assistant"; message: string; timestampUtc: string;
    }>;
  }

FUNCTIONAL REQUIREMENTS

1. UPLOAD PAGE
   - Drag-and-drop zone + file browse button
   - Accept .json directly, or .zip (unzip server-side and find the
     first .json entry)
   - Client-side + server-side validation against the schema above
     (use zod on both client and server, sharing the schema definition
     via /shared if practical)
   - Show a clear error if the file doesn't match the expected shape
   - On successful validation, show a summary card (plant ID, severity,
     primary message, timestamp) before submitting for analysis

2. GEMINI ANALYSIS
   - Backend endpoint: POST /api/artifacts/analyze
   - Use the official Google Generative AI SDK (@google/generative-ai
     for Node, or equivalent) — API key from environment variable
     GEMINI_API_KEY, never hardcoded, never sent to the frontend
   - Use a model like "gemini-2.0-flash" or "gemini-1.5-pro" (make the
     model name configurable via env var GEMINI_MODEL)
   - Construct a prompt that:
       - Explains the assistant's role: expert industrial software
         diagnostics engineer
       - Includes the incident details, relevant log entries, sensor
         snapshot, COM state, and the offline AI's own summary
       - Explicitly instructs Gemini to respond ONLY with valid JSON
         matching this exact shape (define as a TS interface + zod
         schema, validate the response and retry once with a stricter
         prompt if invalid JSON comes back):

         interface GeminiAnalysisResult {
           rootCause: string;
           confidenceScore: number;       // 0-100
           severityAssessment: "Low" | "Medium" | "High" | "Critical";
           suggestedFixSummary: string;
           suspectedFileOrModule: string | null;
           suspectedLineHint: string | null;
           codeRecommendation: string | null;   // short code snippet or null
           reproductionSteps: string[];
           testingRecommendations: string[];
           relatedPastIssueNotes: string | null;
         }

   - Persist the analysis result + original artifact temporarily in
     memory or a lightweight local store (SQLite via better-sqlite3, or
     just in-memory Map for hackathon MVP — your call, note the tradeoff
     in README) keyed by artifactId, so the "Send to SAP BTP" step can
     retrieve it without re-uploading.

3. RESULTS PAGE
   - Nicely designed results view:
       - Top summary card: severity badge, confidence score (visual
         gauge or progress bar), root cause headline
       - "Suggested Fix" section with syntax-highlighted code block if
         codeRecommendation is present (use a lightweight highlighter
         like react-syntax-highlighter)
       - "Reproduction Steps" as a numbered list
       - "Testing Recommendations" as a checklist-style list
       - Collapsible "Raw artifact data" section for the original
         uploaded JSON (for transparency/debugging)
   - "Send to SAP BTP" button (see below)

4. SAP BTP INTEGRATION
   - Backend endpoint: POST /api/artifacts/:artifactId/send-to-btp
   - Build an SapBtpClient service that:
       - Obtains an OAuth2 access token using client-credentials grant
         from SAP BTP's XSUAA token endpoint (env vars: SAP_BTP_TOKEN_URL,
         SAP_BTP_CLIENT_ID, SAP_BTP_CLIENT_SECRET)
       - Caches the token in memory until near expiry
       - POSTs a combined payload to a configurable destination URL
         (env var SAP_BTP_INGEST_URL) — this should be treated as a
         generic REST endpoint (e.g. a SAP CAP custom OData action or a
         SAP Cloud Integration iFlow HTTPS sender) — structure the
         payload as:

         {
           artifactId, plantId, incidentSeverity, generatedAtUtc,
           originalArtifact: DiagnosticArtifact,
           geminiAnalysis: GeminiAnalysisResult,
           submittedAtUtc: string
         }

       - Handle and surface errors clearly (auth failure vs. network
         failure vs. SAP-side validation failure — different messages
         for each)
   - On success, show the returned SAP record ID (assume the endpoint
     returns { id: string } or similar — make this configurable/parsable,
     note the assumption in code comments since real SAP CAP service
     response shape may vary)
   - Add a mock/stub mode (env var SAP_BTP_MOCK_MODE=true) that fakes a
     successful response without a real network call, so the app is
     demoable even without live SAP BTP credentials during the hackathon

5. CONFIGURATION
   .env.example file listing all required environment variables:
     GEMINI_API_KEY=
     GEMINI_MODEL=gemini-2.0-flash
     SAP_BTP_TOKEN_URL=
     SAP_BTP_CLIENT_ID=
     SAP_BTP_CLIENT_SECRET=
     SAP_BTP_INGEST_URL=
     SAP_BTP_MOCK_MODE=true
     PORT=5000

NON-FUNCTIONAL REQUIREMENTS
- Full TypeScript, strict mode, on both client and server
- Input validation with zod at every API boundary
- No API keys or secrets ever sent to the frontend or logged
- Clean loading states and error boundaries in the React app
- Responsive layout (works at 1024px+ comfortably; doesn't need to be
  pixel-perfect on mobile but shouldn't break)
- CORS configured correctly for local dev (client on Vite dev server,
  server on Express)
- README.md with:
    - Setup instructions (env vars, npm install, dev run commands for
      both client and server)
    - Architecture diagram (ASCII or mermaid) showing:
        Upload → Validate → Gemini → Results UI → SAP BTP
    - Notes on the mock SAP BTP mode for demoing without real credentials
    - The full DiagnosticArtifact and GeminiAnalysisResult TypeScript
      interfaces, documented as the two key data contracts

DELIVERABLES
- /client (React+TS+Vite+Tailwind), /server (Express+TS), /shared (types)
- .env.example in /server
- README.md as described above
- At least 6 tests total across client/server (server: schema validation,
  Gemini prompt/response parsing with a mocked API call, SAP BTP client
  with mocked HTTP; client: at least 2 component tests with
  React Testing Library — e.g. upload validation error state, results
  page rendering with a fixture GeminiAnalysisResult)

ACCEPTANCE CRITERIA
- Uploading a valid sample artifact JSON produces a results page with a
  populated GeminiAnalysisResult (using a real or mocked Gemini call,
  your choice for dev, but real call must work if GEMINI_API_KEY is set)
- Uploading an invalid/malformed JSON shows a clear validation error and
  does NOT call Gemini
- Clicking "Send to SAP BTP" in mock mode shows a success state with a
  fake record ID, without making a real network call
- No secrets appear in browser dev tools network tab or console

Please scaffold the full project now: start with /shared type
definitions, then /server (Express app, routes, GeminiClient,
SapBtpClient, validation schemas), then /client (Vite React app: Upload
page, Results page, API client hooks). Ask me before assuming the exact
Gemini SDK method names if you're not certain of the current SDK
version's API shape — state your assumption clearly in a code comment
so I can verify it.
```

---

## 🔗 How the Three Apps Connect (Reference Diagram)

```
┌─────────────────────────┐      ┌──────────────────────────┐
│  APP 1: PlantSimulator   │      │  APP 2: PlantDoctor.Agent │
│  (WPF + WCF, .NET)       │      │  (WPF + Ollama, .NET)     │
│                          │      │                            │
│  Sensors • COM • Errors  │─logs→│  Tails JSONL logs          │
│  Writes JSONL logs       │ file │  Chat + auto-analysis       │
│                          │      │  Exports diagnostic-        │
│                          │      │  artifact-*.json            │
└─────────────────────────┘      └──────────────┬─────────────┘
                                                    │
                                     USB / Email /  │
                                     File Transfer  │
                                                    ▼
                                  ┌──────────────────────────────┐
                                  │  APP 3: PlantDoctor.Portal    │
                                  │  (React + Node/Express)       │
                                  │                                │
                                  │  Upload artifact               │
                                  │  → Gemini analysis (JSON)      │
                                  │  → Display results             │
                                  │  → POST to SAP BTP endpoint    │
                                  └──────────────────────────────┘
```

**Key contract between apps**: the `DiagnosticArtifact` JSON schema is defined identically in Prompt 2's README and Prompt 3's shared types — this is the single source of truth linking your offline and online worlds. Keep the schema in sync if you change it.

---

## 💡 Tips for Using These with Copilot

1. **Paste one prompt at a time** — don't combine all three in one Copilot session; each is a separate solution/repo.
2. **Use Copilot Workspace or Agent mode** if available — these prompts are structured for multi-file scaffolding, not single-function autocomplete.
3. **After scaffolding**, follow up with targeted prompts like:
   - `"Now implement the SensorSimulationService in PlantSimulator.Core with realistic random-walk value generation"`
   - `"Add error handling to OllamaClient for connection refused"`
   - `"Write the zod schema for DiagnosticArtifact in /shared"`
4. **Keep the schema doc handy** — paste the `DiagnosticArtifact` JSON shape into any follow-up prompt to keep Copilot consistent across sessions.
5. **For the hackathon demo**, App 3's `SAP_BTP_MOCK_MODE=true` lets you demo the full flow without needing live BTP credentials until the last mile.

---

## ✅ What You Have Now

- **Prompt 1**: Complete WPF+WCF plant simulator spec (sensors, COM, errors, logging)
- **Prompt 2**: Complete offline Ollama agent spec (log tailing, chat, artifact export)
- **Prompt 3**: Complete web portal spec (upload, Gemini analysis, SAP BTP push)
- **Connecting diagram** showing how the JSON artifact bridges all three
- **Usage tips** for feeding these into Copilot effectively

Each prompt is long and detailed on purpose — Copilot (and most coding agents) produce dramatically better scaffolds when given explicit schemas, acceptance criteria, and file structure up front, rather than a one-line ask.

Good luck with the build! 🚀

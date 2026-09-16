# PlantSimulator — Technical Developer Document

**Version 1.0** | September 2026

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Project Structure](#2-project-structure)
3. [Dependency Injection & Application Lifecycle](#3-dependency-injection--application-lifecycle)
4. [Core Components](#4-core-components)
5. [WCF Service Layer](#5-wcf-service-layer)
6. [UI Layer — MVVM Architecture](#6-ui-layer--mvvm-architecture)
7. [Data Flow](#7-data-flow)
8. [JSONL Logging System](#8-jsonl-logging-system)
9. [Error Injection Framework](#9-error-injection-framework)
10. [COM Port Simulation](#10-com-port-simulation)
11. [Sensor Simulation Algorithm](#11-sensor-simulation-algorithm)
12. [Configuration & Customization](#12-configuration--customization)
13. [Testing Strategy](#13-testing-strategy)
14. [Build & Deployment](#14-build--deployment)
15. [Known Issues & Technical Debt](#15-known-issues--technical-debt)
16. [Future Enhancements](#16-future-enhancements)
17. [Appendix A — Interface Reference](#appendix-a--interface-reference)
18. [Appendix B — NuGet Dependencies](#appendix-b--nuget-dependencies)

---

## 1. Architecture Overview

### 1.1 Design Philosophy

PlantSimulator follows a **layered architecture** with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────┐
│                        UI Layer (WPF)                       │
│  MainWindow.xaml → MainViewModel → DispatcherTimer (1.5s)  │
├─────────────────────────────────────────────────────────────┤
│                    Service Layer (WCF)                      │
│  PlantMonitorService → IPlantMonitorService (net.pipe)     │
├─────────────────────────────────────────────────────────────┤
│                       Core Layer                            │
│  SensorSimulationService | ComPortSimulator | ErrorInjector │
│                       JsonlPlantLogger                      │
├─────────────────────────────────────────────────────────────┤
│                    Contracts Layer (DTOs)                   │
│  SensorReadingDto | ErrorEventDto | ComEventDto            │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 Key Architectural Decisions

| Decision | Rationale |
| -------- | --------- |
| **Self-hosted WCF** | Mirrors industrial architecture where UI and device communication are separated via a service layer |
| **net.pipe binding** | In-process IPC — no network exposure, maximum security for local communication |
| **DI via Microsoft.Extensions.Hosting** | Standard .NET DI container enables testability and loose coupling |
| **JSONL log files** | Append-only, line-delimited format enables real-time tailing by App 2 (PlantDoctor.Agent) |
| **CommunityToolkit.Mvvm** | Source-generator-based MVVM boilerplate eliminates INotifyPropertyChanged boilerplate |
| **Random-walk sensor model** | Produces natural-looking fluctuations without requiring complex physics simulation |

### 1.3 Technology Stack

| Layer | Technology | Version |
| ----- | ---------- | ------- |
| **Framework** | .NET 8.0 (net8.0-windows) | 8.0.30 |
| **UI** | WPF (Windows Presentation Foundation) | .NET 8.0 |
| **MVVM** | CommunityToolkit.Mvvm | 8.3.2 |
| **Service** | CoreWCF (WCF for .NET Core) | 1.9.0 |
| **DI** | Microsoft.Extensions.Hosting | 8.0.0 |
| **Testing** | xUnit + FluentAssertions | 2.9.0 / 6.12.0 |
| **Logging** | System.Text.Json (manual JSONL) | .NET 8.0 |

---

## 2. Project Structure

```
PlantSimulator/
├── PlantSimulator.sln                    # Solution file (5 projects)
├── NuGet.config                          # Package source mapping for CoreWCF
│
├── src/
│   ├── PlantSimulator.Contracts/         # WCF contract + DTOs
│   │   ├── IPlantMonitorService.cs       # [ServiceContract] + [DataContract] classes
│   │   └── PlantSimulator.Contracts.csproj
│   │
│   ├── PlantSimulator.Core/              # Business logic (no UI, no WCF)
│   │   ├── Sensors/
│   │   │   ├── SensorDefinition.cs       # required properties record-like class
│   │   │   ├── ISensorSimulationService.cs
│   │   │   └── SensorSimulationService.cs
│   │   ├── Com/
│   │   │   ├── IComPortSimulator.cs
│   │   │   └── ComPortSimulator.cs
│   │   ├── Errors/
│   │   │   ├── IErrorInjector.cs
│   │   │   └── ErrorInjector.cs
│   │   ├── Logging/
│   │   │   ├── IPlantLogger.cs
│   │   │   └── JsonlPlantLogger.cs
│   │   └── PlantSimulator.Core.csproj
│   │
│   ├── PlantSimulator.ServiceHost/       # WCF self-hosting
│   │   ├── PlantMonitorService.cs        # IPlantMonitorService implementation
│   │   ├── ServiceHostBuilder.cs         # IHostBuilder extension methods
│   │   └── PlantSimulator.ServiceHost.csproj
│   │
│   └── PlantSimulator.UI/                # WPF application
│       ├── App.xaml / App.xaml.cs        # Application lifecycle, DI, WCF startup
│       ├── MainWindow.xaml / .cs         # Three-panel layout, DispatcherTimer
│       ├── DefaultSensors.cs             # 6 sensor definitions
│       ├── SettingsDialog.xaml.cs        # Settings modal (programmatic UI)
│       ├── ViewModels/
│       │   └── MainViewModel.cs          # MVVM viewmodel, commands, tick logic
│       ├── Views/
│       │   ├── MainWindow.xaml.cs        # Code-behind, timer management
│       │   └── SettingsDialog.xaml.cs    # Programmatic settings dialog
│       ├── Converters/
│       │   └── StatusToBrushConverter.cs # IValueConverter: status → SolidColorBrush
│       └── PlantSimulator.UI.csproj
│
└── tests/
    └── PlantSimulator.Tests/             # xUnit test suite
        ├── CoreTests.cs                  # 68 tests (Sensor, Error, COM, Logger, DTOs)
        └── PlantSimulator.Tests.csproj
```

### 2.1 Project Dependencies

```
PlantSimulator.Contracts    (no dependencies)
        ↑
PlantSimulator.Core         (depends on Contracts)
        ↑
PlantSimulator.ServiceHost  (depends on Contracts)
        ↑
PlantSimulator.UI           (depends on Core, ServiceHost, Contracts)
        ↑
PlantSimulator.Tests        (depends on Core, Contracts)
```

---

## 3. Dependency Injection & Application Lifecycle

### 3.1 DI Container Configuration

The DI container is built in `App.xaml.cs` → `InitializeApp()`:

```csharp
Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
    .ConfigureServices(services =>
    {
        // Singleton registrations
        services.AddSingleton<IPlantLogger>(_ => new JsonlPlantLogger(logFolder));
        services.AddSingleton<IEnumerable<SensorDefinition>>(_ => DefaultSensors.Build());
        services.AddSingleton<ISensorSimulationService, SensorSimulationService>();
        services.AddSingleton<IComPortSimulator, ComPortSimulator>();
        services.AddSingleton<IErrorInjector, ErrorInjector>();
        services.AddSingleton<MainViewModel>();
    })
    .AddPlantMonitorHost()  // WCF service registration
    .Build();
```

**Lifetime:** All services are **Singleton** — one instance per application lifetime. This is appropriate because:
- Sensor state must persist across ticks
- COM port state must persist across connect/disconnect cycles
- Logger must maintain file handle across log calls

### 3.2 Application Lifecycle

```
┌──────────────────────────────────────────────────────────────┐
│                        App_OnStartup                         │
│  1. InitializeApp()                                          │
│     a. Build DI container                                    │
│     b. Register all singletons                               │
│     c. Start WCF service host                                │
│  2. Create MainWindow                                        │
│  3. Show MainWindow                                          │
│  4. MainWindow constructor gets MainViewModel from DI        │
│  5. MainWindow starts DispatcherTimer (1.5s interval)        │
├──────────────────────────────────────────────────────────────┤
│                        Main Loop                             │
│  DispatcherTimer.Tick → MainViewModel.Tick()                 │
│  → SensorSimulationService.Tick() for each sensor            │
│  → ComPortSimulator.NextFrame() if connected                 │
│  → JsonlPlantLogger.Log() for each reading                   │
├──────────────────────────────────────────────────────────────┤
│                        App_OnExit                            │
│  1. _wcfHost.Close() + Dispose()                             │
│  2. Host.Dispose() (disposes all singletons)                 │
└──────────────────────────────────────────────────────────────┘
```

### 3.3 Global Exception Handling

Two handlers catch all unhandled exceptions:

```csharp
// UI thread exceptions
this.DispatcherUnhandledException += (s, e) =>
{
    MessageBox.Show($"UI Error: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
        "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
    e.Handled = true;
    Shutdown();
};

// Background thread exceptions
AppDomain.CurrentDomain.UnhandledException += (s, e) =>
{
    if (e.ExceptionObject is Exception ex)
    {
        MessageBox.Show($"Fatal Error: {ex.Message}\n\n{ex.StackTrace}",
            "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    Shutdown();
};
```

---

## 4. Core Components

### 4.1 SensorSimulationService

**File:** `src/PlantSimulator.Core/Sensors/SensorSimulationService.cs`

**Responsibility:** Generate realistic sensor readings using a random-walk algorithm.

**Thread Safety:** All public methods are protected by `lock (_lock)`.

**Internal State:**

| Field | Type | Purpose |
| ----- | ---- | ------- |
| `_rng` | `Random` | Thread-local random number generator |
| `_current` | `Dictionary<string, double>` | Current value for each sensor (keyed by name) |
| `_frozen` | `HashSet<string>` | Names of sensors currently frozen |
| `_outOfRange` | `HashSet<string>` | Names of sensors currently reporting out-of-range |
| `_lock` | `object` | Mutex for thread-safe access |

**Tick Algorithm:**

```csharp
// Step size = 1% of range × random(-0.5 to 0.5)
var step = (s.Max - s.Min) * 0.01 * (_rng.NextDouble() - 0.5);
value = Math.Clamp(value + step, s.Min, s.Max);
```

**Status Calculation:**

```csharp
var status = value >= s.CriticalThreshold ? "Critical"
    : value >= s.WarningThreshold ? "Warning" : "OK";
```

**Fault Injection Methods:**

| Method | Effect |
| ------ | ------ |
| `FreezeSensor(name)` | Adds sensor name to `_frozen` set — value stops changing |
| `InjectOutOfRange(name)` | Adds sensor name to `_outOfRange` set — next tick returns `Max × 10` |

### 4.2 ComPortSimulator

**File:** `src/PlantSimulator.Core/Com/ComPortSimulator.cs`

**Responsibility:** Simulate serial port communication with TX/RX hex frames.

**State Machine:**

```
Disconnected ──Connect()──→ Connected ──Disconnect()──→ Disconnected
                  ↑                              │
                  │                            InjectDisconnect()
                  │                              ↓
                  └──────────────────── Error
```

**Frame Format:**

```
TX: AA BB CC DD EE FF 00 11
RX: 12 34 56 78 9A BC DE F0
```

Each frame: `[TX|RX]: XX XX XX XX XX XX XX XX` (9 space-separated tokens, 8 hex bytes).

**Event Pattern:**

```csharp
public event EventHandler<ComEventDto>? Event;

private void Raise(string? rawFrame) =>
    Event?.Invoke(this, new ComEventDto { ... });
```

### 4.3 ErrorInjector

**File:** `src/PlantSimulator.Core/Errors/ErrorInjector.cs`

**Responsibility:** Create `ErrorEventDto` instances representing fault scenarios.

**Methods:**

| Method | ErrorCode | Level | Source |
| ------ | --------- | ----- | ------ |
| `SensorTimeout(name)` | `SENSOR_TIMEOUT` | Error | Sensor |
| `ComDisconnect(port)` | `COM_DROPPED` | Critical | COM |
| `OverflowException()` | `UNHANDLED_EXCEPTION` | Critical | Application |
| `OutOfRangeValue(name, value, unit)` | `OUT_OF_RANGE` | Warning | Sensor |

**OverflowException Implementation:**

Uses a `checked` block to force an actual `OverflowException`:

```csharp
try
{
    checked { int x = int.MaxValue; x++; }
    return new ErrorEventDto(); // unreachable
}
catch (OverflowException ex)
{
    return new ErrorEventDto
    {
        StackTrace = ex.ToString()
    };
}
```

### 4.4 JsonlPlantLogger

**File:** `src/PlantSimulator.Core/Logging/JsonlPlantLogger.cs`

**Responsibility:** Write structured JSON-lines log files with daily rollover.

**File Naming:** `plant-YYYYMMDD.jsonl` (e.g., `plant-20260909.jsonl`)

**File Share:** `FileShare.ReadWrite` — allows App 2 to read the file concurrently.

**Daily Rollover Logic:**

```csharp
private void EnsureWriter()
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    if (_writer is null || today != _openDate)
    {
        _writer?.Dispose();
        _writer = new StreamWriter(
            new FileStream(CurrentLogFilePath, FileMode.Append,
                FileAccess.Write, FileShare.ReadWrite));
        _openDate = today;
    }
}
```

**JSON Output Schemas:**

**Sensor Reading:**

```json
{
  "timestamp": "2026-09-09T12:00:00.123Z",
  "level": "Info",
  "source": "Sensor",
  "sensorName": "Temperature",
  "value": "75.123",
  "message": "Temperature=75.12°C"
}
```

**Error Event:**

```json
{
  "timestamp": "2026-09-09T12:00:01.456Z",
  "level": "Critical",
  "source": "Application",
  "errorCode": "UNHANDLED_EXCEPTION",
  "message": "Arithmetic operation resulted in an overflow.",
  "stackTrace": "   at PlantSimulator.Core.Errors.ErrorInjector.OverflowException()..."
}
```

**COM Event:**

```json
{
  "timestamp": "2026-09-09T12:00:02.789Z",
  "level": "Info",
  "source": "COM",
  "message": "COM COM1 Connected"
}
```

---

## 5. WCF Service Layer

### 5.1 Service Contract

**File:** `src/PlantSimulator.Contracts/IPlantMonitorService.cs`

**Endpoint:** `net.pipe://localhost/PlantDoctor/monitor`

**Binding:** `NetNamedPipeBinding` (in-process IPC)

**Namespace:** `http://plantdoctor.local/plant-monitor`

### 5.2 Service Operations

| Operation | Direction | Description |
| --------- | --------- | ----------- |
| `ReportSensorReading(SensorReadingDto)` | One-way | Receives sensor reading, fans out to logger + event |
| `ReportError(ErrorEventDto)` | One-way | Receives error event, fans out to logger + event |
| `ReportComEvent(ComEventDto)` | One-way | Receives COM event, fans out to logger + event |
| `GetCurrentSnapshot()` | Request-reply | Returns current state of all sensors + COM |

### 5.3 Service Implementation

**File:** `src/PlantSimulator.ServiceHost/PlantMonitorService.cs`

**Responsibility:** Fan out incoming reports to the logger and raise events.

**Internal State:**

| Field | Type | Purpose |
| ----- | ---- | ------- |
| `_lastSensors` | `ConcurrentDictionary<string, SensorReadingDto>` | Last known value for each sensor |
| `_lastCom` | `ComEventDto?` | Last known COM state |

**Events:**

```csharp
public event EventHandler<ErrorEventDto>? ErrorReported;
public event EventHandler<SensorReadingDto>? SensorReported;
public event EventHandler<ComEventDto>? ComReported;
```

These events allow subscribers (e.g., PlantDoctor.Agent) to react to events in real time without polling the log file.

### 5.4 Service Host Configuration

**File:** `src/PlantSimulator.ServiceHost/ServiceHostBuilder.cs`

Extension methods for `IHostBuilder`:

```csharp
// Registration
public static IHostBuilder AddPlantMonitorHost(this IHostBuilder builder)

// Endpoint configuration
public static void ConfigurePlantMonitorEndpoints(IServiceBuilder serviceBuilder)
```

**PipeBaseAddress constant:** `"net.pipe://localhost/PlantDoctor"`

---

## 6. UI Layer — MVVM Architecture

### 6.1 ViewModels

**MainViewModel** — `src/PlantSimulator.UI/ViewModels/MainViewModel.cs`

**Properties:**

| Property | Type | Binding |
| -------- | ---- | ------- |
| `Readings` | `ObservableCollection<SensorReadingDto>` | Sensor Readings DataGrid |
| `ComTraffic` | `ObservableCollection<string>` | COM Port ListBox |
| `Events` | `ObservableCollection<ErrorEventDto>` | Error Log DataGrid |
| `ComStatus` | `string` (ObservableProperty) | Status ellipse + TextBlock |
| `SelectedPort` | `string` (ObservableProperty) | COM port ComboBox |

**Commands (RelayCommand):**

| Command | Method | Effect |
| ------- | ------ | ------ |
| `ConnectCommand` | `Connect()` | `_com.Connect(SelectedPort)` |
| `DisconnectCommand` | `Disconnect()` | `_com.Disconnect()` |
| `InjectSensorTimeoutCommand` | `InjectSensorTimeout()` | Freeze first sensor + log error |
| `InjectComDisconnectCommand` | `InjectComDisconnect()` | Simulate COM drop + log error |
| `InjectOverflowCommand` | `InjectOverflow()` | Log overflow exception |
| `InjectOutOfRangeCommand` | `InjectOutOfRange()` | Force Max×10 + log warning |
| `OpenSettingsCommand` | `OpenSettings()` | Show SettingsDialog |

**Tick Method:**

```csharp
public void Tick()
{
    // Update all sensors
    foreach (var def in _sensors.Sensors)
    {
        var r = _sensors.Tick(def);
        // Replace existing reading or add new
        var existing = Readings.FirstOrDefault(x => x.Name == r.Name);
        if (existing != null) Readings.Remove(existing);
        Readings.Add(r);
        _log.Log(r);
    }

    // Add COM traffic if connected
    if (ComStatus == "Connected")
    {
        ComTraffic.Insert(0, _com.NextFrame());
        while (ComTraffic.Count > 200) ComTraffic.RemoveAt(ComTraffic.Count - 1);
    }
}
```

**COM Traffic Management:** Keeps the **most recent 200 frames**. Older frames are removed from the end (oldest) to maintain performance.

### 6.2 Views

**MainWindow** — `src/PlantSimulator.UI/Views/MainWindow.xaml`

**Layout:** Grid with Menu (Row 0) + Three-panel Content (Row 1)

```
┌─────────────────────────────────────────────────────────────┐
│  File  │  Help                                              │
├─────────────────────────────────────────────────────────────┤
│  Sensor Readings  │  COM Port     │  Error & Event Log      │
│  (Column 0)       │  (Column 1)   │  (Column 2)             │
│  DataGrid         │  ComboBox     │  ToolBar                │
│                   │  Buttons      │  Buttons                │
│                   │  Ellipse      │  DataGrid               │
│                   │  ListBox      │                         │
└─────────────────────────────────────────────────────────────┘
```

**Code-Behind:** `MainWindow.xaml.cs`

```csharp
public MainWindow()
{
    // Get MainViewModel from App.Host.Services (DI)
    var vm = App.Host.Services.GetRequiredService<MainViewModel>();
    DataContext = vm;

    // Start 1.5s timer
    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
    _timer.Tick += (s, e) => vm.Tick();
    _timer.Start();
}
```

**SettingsDialog** — `src/PlantSimulator.UI/Views/SettingsDialog.xaml.cs`

Programmatic UI (no XAML file). Controls:

| Control | Purpose |
| ------- | ------- |
| `TextBox _intervalBox` | Sensor update interval in ms (default: 1500) |
| `TextBox _logFolderBox` | Log folder path (default: `%LOCALAPPDATA%\PlantDoctor\logs`) |
| `ListBox _sensorList` | Multi-select list of active sensors |

### 6.3 Converters

**StatusToBrushConverter** — `src/PlantSimulator.UI/Converters/StatusToBrushConverter.cs`

Implements `IValueConverter` for status string → SolidColorBrush:

| Input | Output |
| ----- | ------ |
| `"OK"` | 🟢 Green |
| `"Connected"` | 🟢 Green |
| `"Warning"` | 🟠 Orange |
| `"Disconnected"` | 🟠 Orange |
| `"Critical"` | 🔴 Red |
| `"Error"` | 🔴 Red |
| _anything else_ | ⚪ Gray |

**Usage in XAML:**

```xml
<Ellipse Fill="{Binding ComStatus, Converter={StaticResource StatusToBrushConverter}}"/>
```

---

## 7. Data Flow

### 7.1 Normal Operation (Tick Cycle)

```
┌─────────────┐     ┌──────────────────┐     ┌───────────────────┐
│ Dispatcher  │────→│  MainViewModel   │────→│ SensorSimulation  │
│   Timer     │     │     .Tick()      │     │    .Tick()        │
│  (1.5s)     │     └──────────────────┘     └───────────────────┘
│             │            │                            │
│             │            ↓                            ↓
│             │     ┌──────────────┐           ┌───────────────┐
│             │     │ Readings     │           │ JsonlPlant    │
│             │     │ ObservableCollection │   │    Logger     │
│             │     └──────────────┘           └───────────────┘
│             │            │                            │
│             │            ↓                            ↓
│             │     ┌──────────────┐           ┌───────────────┐
│             │     │  DataGrid    │           │  JSONL File   │
│             │     │  (UI)        │           │  (Disk)       │
│             │     └──────────────┘           └───────────────┘
│             │
│             │  ┌──────────────────┐
│             │  │ ComPortSimulator │
│             │  │   .NextFrame()   │
│             │  └──────────────────┘
│             │            │
│             │            ↓
│             │     ┌──────────────┐
│             │     │ ComTraffic   │
│             │     │ ObservableCollection │
│             │     └──────────────┘
│             │            │
│             │            ↓
│             │     ┌──────────────┐
│             │     │   ListBox    │
│             │     │   (UI)       │
│             │     └──────────────┘
└─────────────┘
```

### 7.2 Error Injection Flow

```
┌─────────────┐     ┌──────────────────┐     ┌───────────────────┐
│  Toolbar    │────→│  MainViewModel   │────→│  ErrorInjector    │
│  Button     │     │   .Inject*()     │     │   .Overflow()     │
└─────────────┘     └──────────────────┘     └───────────────────┘
                            │                            │
                            ↓                            ↓
                            │                     ┌───────────────┐
                            │                     │ ErrorEventDto │
                            │                     └───────────────┘
                            │                            │
                            ↓                            ↓
                    ┌──────────────┐           ┌───────────────┐
                    │  Events      │           │ JsonlPlant    │
                    │ ObservableCollection │   │    Logger     │
                    └──────────────┘           └───────────────┘
                            │                            │
                            ↓                            ↓
                    ┌──────────────┐           ┌───────────────┐
                    │  DataGrid    │           │  JSONL File   │
                    │  (UI)        │           │  (Disk)       │
                    └──────────────┘           └───────────────┘
```

### 7.3 WCF Service Flow

```
┌──────────────────┐     ┌───────────────────┐     ┌───────────────┐
│ SensorSimulation │────→│ PlantMonitor      │────→│ JsonlPlant    │
│   .Tick()        │     │ Service           │     │   Logger      │
│   .Report*()     │     │   .Report*()      │     │   .Log()      │
└──────────────────┘     └───────────────────┘     └───────────────┘
                            │
                            ↓
                    ┌───────────────┐
                    │  Events       │
                    │ (ErrorReported│
                    │  SensorReported│
                    │  ComReported) │
                    └───────────────┘
                            │
                            ↓
                    ┌───────────────┐
                    │  Subscribers  │
                    │ (App 2, etc.) │
                    └───────────────┘
```

---

## 8. JSONL Logging System

### 8.1 Integration Contract

The JSONL log file is the **primary integration point** with PlantDoctor.Agent (App 2).

**File Path:** `%LOCALAPPDATA%\PlantDoctor\logs\plant-YYYYMMDD.jsonl`

**Access Pattern:**
- PlantSimulator: `FileMode.Append` + `FileAccess.Write` + `FileShare.ReadWrite`
- PlantDoctor.Agent: `FileShare.Read` (concurrent read)

### 8.2 Log Entry Types

| Source | Level Mapping | Fields Included |
| ------ | ------------- | --------------- |
| **Sensor** | OK → Info, Warning → Warning, Critical → Critical | timestamp, level, source, sensorName, value, message |
| **COM** | Error → Error, else → Info | timestamp, level, source, message |
| **Application** | As provided | timestamp, level, source, sensorName, value, errorCode, message, stackTrace |

### 8.3 Daily Rollover

- Rollover occurs at **midnight UTC** (not local time)
- Old files are **never overwritten** — each day gets a new file
- The `_openDate` field tracks the current log date

### 8.4 Thread Safety

All log writes are protected by `lock (_lock)`:

```csharp
private void WriteLine(object obj)
{
    var json = JsonSerializer.Serialize(obj);
    lock (_lock)
    {
        EnsureWriter();
        _writer!.WriteLine(json);
        _writer.Flush();
    }
}
```

---

## 9. Error Injection Framework

### 9.1 Error Scenarios

| Scenario | Button | Mechanism | Effect |
| -------- | ------ | --------- | ------ |
| **Sensor Timeout** | Sensor Timeout | `FreezeSensor()` | Sensor value stops changing |
| **COM Disconnect** | COM Disconnect | `InjectDisconnect()` | COM status → Error |
| **Overflow** | Overflow | `checked { int.MaxValue++ }` | Actual OverflowException |
| **Out of Range** | Out of Range | `InjectOutOfRange()` | Sensor reports Max × 10 |

### 9.2 Error Recovery

| Error | Recovery |
| ----- | -------- |
| Sensor Timeout | Restart application |
| COM Disconnect | Click Connect again |
| Overflow | Restart application |
| Out of Range | Restart application |

---

## 10. COM Port Simulation

### 10.1 Frame Generation

```csharp
public string NextFrame()
{
    var bytes = new byte[8];
    _rng.NextBytes(bytes);
    var frame = string.Join(' ', bytes.Select(b => b.ToString("X2")));
    return (_rng.Next(2) == 0 ? "TX: " : "RX: ") + frame;
}
```

Each call generates 8 random bytes formatted as uppercase hex, prefixed with `TX: ` or `RX: `.

### 10.2 Event System

```csharp
public event EventHandler<ComEventDto>? Event;

// Raised on Connect, Disconnect, InjectDisconnect
private void Raise(string? rawFrame) =>
    Event?.Invoke(this, new ComEventDto
    {
        TimestampUtc = DateTime.UtcNow,
        Port = CurrentPort ?? "-",
        Status = Status,
        RawFrame = rawFrame
    });
```

**MainViewModel subscribes:**

```csharp
_com.Event += (_, e) => { ComStatus = e.Status; _log.Log(e); };
```

---

## 11. Sensor Simulation Algorithm

### 11.1 Random Walk Model

Each sensor follows a **bounded random walk**:

```
step = range × 0.01 × random(-0.5, 0.5)
newValue = clamp(currentValue + step, Min, Max)
```

**Parameters:**

| Parameter | Value | Rationale |
| --------- | ----- | --------- |
| Step multiplier | 1% of range | Small enough for natural fluctuations |
| Random range | ±0.5 | Uniform distribution around zero |
| Clamping | Min–Max | Prevents values from exceeding bounds |

### 11.2 Default Sensors

| Sensor | Unit | Min | Max | Initial | Warning | Critical |
| ------ | ---- | --- | --- | ------- | ------- | -------- |
| Temperature | °C | 0 | 200 | 75 | 140 | 170 |
| Pressure | bar | 0 | 20 | 8 | 15 | 18 |
| VibrationX | mm/s | 0 | 50 | 5 | 30 | 40 |
| VibrationY | mm/s | 0 | 50 | 5 | 30 | 40 |
| RPM | rpm | 0 | 6000 | 1500 | 5000 | 5500 |
| Voltage | V | 0 | 500 | 230 | 400 | 450 |

**Definition:** `src/PlantSimulator.UI/DefaultSensors.cs`

---

## 12. Configuration & Customization

### 12.1 Adding Custom Sensors

Modify `DefaultSensors.cs`:

```csharp
public static IEnumerable<SensorDefinition> Build() => new[]
{
    new SensorDefinition
    {
        Name = "pH",
        Unit = "",
        Min = 0,
        Max = 14,
        InitialValue = 7,
        WarningThreshold = 10,
        CriticalThreshold = 12
    },
    // ... existing sensors
};
```

### 12.2 Changing Update Interval

The default is **1500 ms**. Change in `MainWindow.xaml.cs`:

```csharp
_timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) }; // 1 second
```

### 12.3 Changing Log Folder

Default: `%LOCALAPPDATA%\PlantDoctor\logs`

Change in `App.xaml.cs` → `InitializeApp()`:

```csharp
var logFolder = @"C:\Custom\Log\Folder";
```

### 12.4 Adding New Error Types

1. Add method to `IErrorInjector.cs`
2. Implement in `ErrorInjector.cs`
3. Add `[RelayCommand]` method to `MainViewModel.cs`
4. Add button to `MainWindow.xaml` ToolBar

---

## 13. Testing Strategy

### 13.1 Test Framework

| Framework | Version | Purpose |
| --------- | ------- | ------- |
| xUnit | 2.9.0 | Test runner |
| FluentAssertions | 6.12.0 | Readable assertions |

### 13.2 Test Coverage

**68 tests** across 5 test classes:

| Test Class | Tests | Coverage |
| ---------- | ----- | -------- |
| `SensorSimulationServiceTests` | 16 | Positive, negative, edge cases |
| `ErrorInjectorTests` | 18 | All error types, edge cases |
| `ComPortSimulatorTests` | 17 | Connect/disconnect/events |
| `JsonlPlantLoggerTests` | 11 | File I/O, JSON output, disposal |
| `SensorDefinitionTests` + `DtoTests` | 5 | Property validation |

### 13.3 Running Tests

```powershell
dotnet test .\tests\PlantSimulator.Tests\PlantSimulator.Tests.csproj
```

### 13.4 Test Categories

Each test class follows the **Positive / Negative / Edge Case** pattern:

| Category | Description | Example |
| -------- | ----------- | ------- |
| **Positive** | Expected behavior under normal conditions | `Tick_ReturnsValueWithinBounds` |
| **Negative** | Graceful handling of invalid input | `FreezeSensor_UnknownSensor_DoesNotThrow` |
| **Edge Case** | Boundary conditions | `Tick_WithZeroRange_ProducesConstantValue` |

---

## 14. Build & Deployment

### 14.1 Build Commands

```powershell
# Restore packages
dotnet restore .\PlantSimulator.sln

# Build all projects
dotnet build .\PlantSimulator.sln

# Run tests
dotnet test .\tests\PlantSimulator.Tests\PlantSimulator.Tests.csproj

# Run application
dotnet run --project .\src\PlantSimulator.UI\PlantSimulator.UI.csproj
```

### 14.2 Publish for Distribution

```powershell
dotnet publish .\src\PlantSimulator.UI\PlantSimulator.UI.csproj `
    -c Release `
    -r win-x64 `
    --self-contained `
    -o .\publish
```

### 14.3 NuGet Configuration

**File:** `NuGet.config`

Required for CoreWCF package resolution with wildcard package source mapping:

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org">
      <pattern wildcard="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

---

## 15. Known Issues & Technical Debt

### 15.1 NuGet Vulnerability Warnings

CoreWCF 1.9.0 has known vulnerabilities:

| Package | Severity | Advisory |
| ------- | -------- | -------- |
| `CoreWCF.Primitives` 1.9.0 | Critical | GHSA-xjr9-gg9q-jx3v |
| `CoreWCF.Primitives` 1.9.0 | High | GHSA-2288-8h3r-cqgg |
| `CoreWCF.Primitives` 1.9.0 | High | GHSA-48pq-2xq3-c2m4 |
| `CoreWCF.NetNamedPipe` 1.9.0 | Moderate | GHSA-6jj2-4q5c-x8g6 |

**Impact:** Low — PlantSimulator runs offline with no network exposure.

### 15.2 CA1416 Platform Warning

```
CA1416: This call site is reachable on all platforms. 'NetNamedPipeBinding'
is only supported on: 'windows'.
```

**Impact:** Informational only — the project targets `net8.0-windows` explicitly.

### 15.3 Hardcoded Sensor Definitions

Sensors are defined in `DefaultSensors.cs` with no external configuration. To add sensors, code must be modified and rebuilt.

### 15.4 No Settings Persistence

Settings changes (interval, log folder, active sensors) are not persisted across sessions. The Settings dialog is read-only at runtime.

### 15.5 COM Traffic Memory Limit

COM traffic is capped at 200 frames in the `ObservableCollection<string>`. This is a reasonable limit but should be monitored if increased.

---

## 16. Future Enhancements

### 16.1 High Priority

| Enhancement | Description | Effort |
| ----------- | ----------- | ------ |
| **Settings Persistence** | Save/restore settings to app.config or JSON file | Low |
| **net.tcp Endpoint** | Expose WCF over net.tcp for cross-process communication with App 2 | Medium |
| **Custom Sensor Config** | Load sensor definitions from JSON/YAML config file | Low |

### 16.2 Medium Priority

| Enhancement | Description | Effort |
| ----------- | ----------- | ------ |
| **Sensor Calibration** | Allow users to adjust Min/Max/Threshold per sensor | Medium |
| **Log Export** | Export log data to CSV/Excel | Low |
| **Real Sensor Input** | Optional integration with actual serial port or Modbus | High |
| **Dashboard Charts** | Add LineChart for sensor trends over time | Medium |

### 16.3 Low Priority

| Enhancement | Description | Effort |
| ----------- | ----------- | ------ |
| **Multi-Plant Support** | Simulate multiple plants simultaneously | High |
| **Scenario Presets** | Predefined scenarios (normal, stress test, fault cascade) | Medium |
| **Localization** | Support non-English UI strings | Medium |

---

## Appendix A — Interface Reference

### IPlantMonitorService

```csharp
[ServiceContract(Namespace = "http://plantdoctor.local/plant-monitor")]
public interface IPlantMonitorService
{
    [OperationContract(IsOneWay = true)]
    void ReportSensorReading(SensorReadingDto dto);

    [OperationContract(IsOneWay = true)]
    void ReportError(ErrorEventDto dto);

    [OperationContract(IsOneWay = true)]
    void ReportComEvent(ComEventDto dto);

    [OperationContract]
    PlantSnapshotDto GetCurrentSnapshot();
}
```

### ISensorSimulationService

```csharp
public interface ISensorSimulationService
{
    IReadOnlyList<SensorDefinition> Sensors { get; }
    SensorReadingDto Tick(SensorDefinition s);
    void FreezeSensor(string name);
    void InjectOutOfRange(string name);
}
```

### IComPortSimulator

```csharp
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
```

### IErrorInjector

```csharp
public interface IErrorInjector
{
    ErrorEventDto SensorTimeout(string name);
    ErrorEventDto ComDisconnect(string port);
    ErrorEventDto OverflowException();
    ErrorEventDto OutOfRangeValue(string name, double value, string unit);
}
```

### IPlantLogger

```csharp
public interface IPlantLogger : IDisposable
{
    void Log(ErrorEventDto e);
    void Log(SensorReadingDto r);
    void Log(ComEventDto e);
}
```

### SensorDefinition

```csharp
public sealed class SensorDefinition
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required double Min { get; init; }
    public required double Max { get; init; }
    public required double InitialValue { get; init; }
    public required double WarningThreshold { get; init; }
    public required double CriticalThreshold { get; init; }
}
```

---

## Appendix B — NuGet Dependencies

### PlantSimulator.Contracts

| Package | Version | Purpose |
| ------- | ------- | ------- |
| CoreWCF.NetNamedPipe | 1.9.0 | WCF service contract attributes |

### PlantSimulator.Core

| Package | Version | Purpose |
| ------- | ------- | ------- |
| PlantSimulator.Contracts | (project) | DTO definitions |

### PlantSimulator.ServiceHost

| Package | Version | Purpose |
| ------- | ------- | ------- |
| CoreWCF.NetNamedPipe | 1.9.0 | WCF service hosting |
| CoreWCF.Primitives | 1.9.0 | WCF core primitives |
| Microsoft.Extensions.Hosting | 8.0.0 | DI container |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | Logging abstractions |
| PlantSimulator.Contracts | (project) | Service contract |

### PlantSimulator.UI

| Package | Version | Purpose |
| ------- | ------- | ------- |
| CommunityToolkit.Mvvm | 8.3.2 | MVVM source generators |
| CoreWCF.NetNamedPipe | 1.9.0 | WCF binding types |
| Microsoft.Extensions.DependencyInjection | 8.0.0 | DI container |
| Microsoft.Extensions.Hosting | 8.0.0 | Host builder |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | Logging abstractions |
| Serilog.Extensions.Hosting | 8.0.0 | Serilog host integration |
| System.ServiceModel.Primitives | 8.0.0 | System.ServiceModel.ServiceHost |
| PlantSimulator.Contracts | (project) | DTOs |
| PlantSimulator.Core | (project) | Core logic |
| PlantSimulator.ServiceHost | (project) | WCF hosting |

### PlantSimulator.Tests

| Package | Version | Purpose |
| ------- | ------- | ------- |
| FluentAssertions | 6.12.0 | Assertion library |
| xunit | 2.9.0 | Test framework |
| xunit.runner.visualstudio | 2.8.2 | VS test adapter |
| Microsoft.NET.Test.Sdk | 17.11.1 | Test SDK |
| PlantSimulator.Core | (project) | Core logic |
| PlantSimulator.Contracts | (project) | DTOs |

---

*End of Technical Developer Document*

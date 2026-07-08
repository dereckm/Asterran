# Project Architecture

Asterran is structured into decoupled, modular assemblies to separate desktop rendering, core monitoring engine controllers, input connectors, and guardrail rules execution.

```mermaid
graph TD
    Viewer[Asterran.Viewer WPF shell] -->|Loads WebView2 UI| HTML[React Frontend wwwroot]
    Viewer -->|IPC Bridge| Engine[Asterran.Engine]
    Engine -->|Scans Changes| Guardrails[Asterran.Engine.Guardrails]
    Engine -->|Subscribes Feeds| Connectors[Asterran.Connectors]
    Engine -->|Watches Directories| Watcher[CodebaseWatcher]
```

---

## 1. Assemblies and Layers

### Asterran.Viewer (WPF Desktop App)
Acts as the desktop shell holding a WebView2 control. It configures a virtual host mapping to serve assets from `wwwroot` and sets up an asynchronous IPC channel (`postMessage`) to bridge frontend interactions (like file selection and manual clearance clicks) to the core engine.

### wwwroot (React 18 Frontend)
A modular React single-page application split into functional components:
- **`Sidebar.js`**: Controls status, directories path configurations, and task progress indicators.
- **`ArchitectureMap.js`**: Computes layout ranks and draws SVG UML diagrams.
- **`InspectionPanel.js`**: Consolidates Guardrails, File Lists, and Line Diffs into a tabbed layout.
- **`styles/`**: Component-scoped CSS stylesheets.

### Asterran.Engine (Core Controller)
The heart of the application. It coordinates:
- **`CodebaseWatcher.cs`**: Monitors workspace directories, debouncing filesystem inputs to filter noise.
- **`ArchitectureAnalyzer.cs`**: Parses C# project file xml tags (`.csproj`) to build a dependency graph and resolve file status maps.
- **`TaskQueue.cs`**: Manages a background queue to execute tasks and report progress percentages to the UI.

### Asterran.Engine.Guardrails (Scanning Engine)
A decoupled scanning library with zero external dependencies. It defines:
- **`IGenericLexer`**: An interface for lexical regex tokenizers.
- **`IGuardrailRule`**: Scanners that match tokens against code violation criteria.
- **`GuardrailEngine`**: Evaluator orchestrator.

### Asterran.Connectors (Event Providers)
Implements connectors to hook into third-party transcript streams (such as active developer timelines) and raise events whenever actions or prompts occur.

---

## 2. IPC Communication Schema

Frontend to Backend commands and Backend to Frontend update event patterns are sent as JSON payloads:

### Frontend to Backend:
- `init`: Triggers config synchronizations.
- `start` / `stop`: Controls watcher state toggles.
- `selectWorkspace`: Opens a folders dialog.
- `setConversationId`: Binds to a target activity log file.
- `clearViolation`: Manually acknowledges and removes an active flag.

### Backend to Frontend:
- `config`: Binds path and status values.
- `engineStatus`: Broadcasts monitoring state.
- `activity`: Stream timelines prompt alerts.
- `fileChange`: Dispatches line count diff data.
- `taskQueue`: Synchronizes background task progress percentages.
- `architecture`: Transmits resolved project nodes and active violations lists.

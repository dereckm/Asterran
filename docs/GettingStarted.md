# Getting Started

Follow these instructions to configure, compile, test, and launch the Asterran Codebase Monitor.

---

## 1. Prerequisites

- **Operating System**: Windows (required for WPF and WebView2 components).
- **SDK**: .NET 8.0 SDK.
- **Git**: Installed and configured for version control.

---

## 2. Compilation and Build

To compile all projects in the solution under the Release configuration, run:

```powershell
dotnet build -c Release
```

This restores NuGet packages (like xUnit and NSubstitute), compiles the C# assemblies, and copies the `wwwroot` React application assets to the Release folder output directory.

---

## 3. Running Unit Tests

Asterran has dedicated unit test projects containing 14 comprehensive assertions. To execute the test suite, run:

```powershell
dotnet test Asterran.sln -c Release
```

The test assemblies cover:
- **`Asterran.Engine.Guardrails.Test`**: Verifies regular expression lexers and rules evaluation.
- **`Asterran.Engine.Test`**: Verifies the debounced task queue scheduler and directory watchers.
- **`Asterran.Connectors.Test`**: Employs NSubstitute mocks to verify transcript feed event channels.

---

## 4. Launching the Monitor

To run the application:
1. Double-click the **`run.bat`** file in the root directory.
2. The WPF window will launch, loading the **Architecture Map** tab by default.
3. Click the **Start Monitor** button in the Control Panel to begin observing folder alterations.

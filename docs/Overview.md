# Asterran - Real-Time Codebase & Architecture Monitor

Asterran is a developer-focused codebase monitoring tool designed to track, map, and safeguard local software projects in real-time. It visualizes solution dependencies and intercepts modifications (such as changes proposed by AI assistants or manual edits) against configurable guardrails to flag security, structural, or cryptographic risks.

---

## Core Features

1. **Interactive UML Dependency Map**
   Dynamically visualizes solution C# projects and their dependencies. Clicking a project node expands it, revealing its source files and structure.

2. **Real-Time Codebase Watching**
   Subscribes to file-system triggers using debounced background worker tasks to instantly identify file changes (additions, modifications, and deletions).

3. **Language-Abstracted Guardrails**
   Scans modified source lines using regex-based lexical analyzers (supporting C#, Python, and JavaScript/TypeScript) to intercept credentials leaks, obsolete cryptography, and process spawn commands.

4. **Persistent Developer Audit Clearance**
   Flags risky code on the visual map (turning project nodes Red). The violations persist across clean modifications and stack dynamically until a developer reviews the warning and manually clicks the **Clear** button.

5. **LLM Activity Timelines**
   Bridges connectivity with LLM transcript streams to log prompt inputs, thoughts, and task execution progress alongside code modifications. Supports two selectable connectors via the sidebar toggle: **Gemini** (Antigravity sessions under `~/.gemini/`) and **Claude** (Claude Code sessions under `~/.claude/projects/`).

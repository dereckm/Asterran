const { useState, useEffect, useRef } = React;

function App() {
    const [workspacePath, setWorkspacePath] = useState("c:\\Asterran");
    const [conversationId, setConversationId] = useState("");
    const [connectorType, setConnectorType] = useState("gemini");
    const [isRunning, setIsRunning] = useState(false);
    const [noProjectsWarningDismissed, setNoProjectsWarningDismissed] = useState(false);
    const [changedFiles, setChangedFiles] = useState({});
    const [activeFileDiffPath, setActiveFileDiffPath] = useState(null);
    const [projects, setProjects] = useState([]);
    const [violations, setViolations] = useState([]);
    const [tasks, setTasks] = useState([]);
    const [timelineItems, setTimelineItems] = useState([]);
    const [activeLeftTab, setActiveLeftTab] = useState("architecture");
    const [activeRightTab, setActiveRightTab] = useState("guardrails");
    const [activeCheckedProjects, setActiveCheckedProjects] = useState({});
    const [expandedProjectName, setExpandedProjectName] = useState(null);
    const activeFileDiffPathRef = useRef(null);

    useEffect(() => {
        activeFileDiffPathRef.current = activeFileDiffPath;
    }, [activeFileDiffPath]);

    useEffect(() => {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage({ command: "init" });
            window.chrome.webview.addEventListener("message", handleIpcMessage);
        } else {
            setupMockData();
        }
        return () => {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.removeEventListener("message", handleIpcMessage);
            }
        };
    }, []);

    const handleIpcMessage = (event) => {
        const payload = event.data;
        if (!payload || !payload.event) return;

        switch (payload.event) {
            case "config":
                setWorkspacePath(payload.data.workspacePath);
                setConversationId(payload.data.conversationId || "");
                setConnectorType(payload.data.connectorType || "gemini");
                setIsRunning(payload.data.isRunning);
                break;
            case "engineStatus":
                setIsRunning(payload.data.isRunning);
                break;
            case "activity":
                setTimelineItems(prev => [payload.data, ...prev]);
                break;
            case "fileChange":
                handleFileChange(payload.data);
                break;
            case "taskQueue":
                setTasks(payload.data || []);
                break;
            case "architecture":
                setProjects(payload.data.projects || []);
                setViolations(payload.data.violations || []);
                
                if (payload.data.violations && payload.data.violations.length > 0) {
                    setActiveRightTab("guardrails");
                }
                break;
        }
    };

    const handleFileChange = (fileChange) => {
        setChangedFiles(prev => {
            const next = { ...prev };
            if (fileChange.ChangeType === "Deleted") {
                delete next[fileChange.FilePath];
                if (activeFileDiffPathRef.current === fileChange.FilePath) {
                    setActiveFileDiffPath(null);
                }
            } else {
                next[fileChange.FilePath] = fileChange;
            }
            return next;
        });
    };

    const setupMockData = () => {
        setProjects([
            { ProjectName: "Asterran.Connectors", Status: "Green", Dependencies: [], SourceFiles: ["ILlmConnector.cs", "AntigravityTranscriptConnector.cs"] },
            { ProjectName: "Asterran.Engine", Status: "Green", Dependencies: ["Asterran.Connectors"], SourceFiles: ["EngineController.cs", "ArchitectureAnalyzer.cs"] },
            { ProjectName: "Asterran.Viewer", Status: "Green", Dependencies: ["Asterran.Engine", "Asterran.Connectors"], SourceFiles: ["MainWindow.xaml.cs"] },
            { ProjectName: "Asterran.Test", Status: "Green", Dependencies: ["Asterran.Engine", "Asterran.Connectors"], SourceFiles: ["Program.cs"] }
        ]);
    };

    const postMessageToHost = (message) => {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(message);
        }
    };

    const handleStartStop = () => {
        if (!isRunning) setNoProjectsWarningDismissed(false);
        postMessageToHost({ command: isRunning ? "stop" : "start" });
    };

    const handleBrowseWorkspace = () => {
        postMessageToHost({ command: "selectWorkspace" });
    };

    const handleConversationIdChange = (val) => {
        setConversationId(val);
        postMessageToHost({ command: "setConversationId", conversationId: val });
    };

    const handleConnectorTypeChange = (val) => {
        setConnectorType(val);
        postMessageToHost({ command: "setConnectorType", connectorType: val });
    };

    const handleClearTimeline = () => {
        setTimelineItems([]);
    };

    const handleClearViolation = (id) => {
        postMessageToHost({ command: "clearViolation", violationId: id });
    };

    return (
        <div className="app-container">
            <Sidebar
                isRunning={isRunning}
                workspacePath={workspacePath}
                conversationId={conversationId}
                connectorType={connectorType}
                onStartStop={handleStartStop}
                onBrowseWorkspace={handleBrowseWorkspace}
                onConversationIdChange={handleConversationIdChange}
                onConnectorTypeChange={handleConnectorTypeChange}
                tasks={tasks}
            />

            <main className="main-content">
                <header className="main-header">
                    <div className="header-info">
                        <h1>Real-Time Codebase Monitor</h1>
                        <p>{isRunning ? `Observing: ${workspacePath}` : "Engine Stopped"}</p>
                    </div>
                </header>

                <div className="dashboard-grid">
                    <section className="card timeline-card">
                        <div className="card-header tab-header">
                            <div className="tabs">
                                <button 
                                    className={`tab-btn ${activeLeftTab === "architecture" ? "active" : ""}`}
                                    onClick={() => setActiveLeftTab("architecture")}
                                >
                                    <i className="fa-solid fa-network-wired"></i> Architecture Map
                                </button>
                                <button 
                                    className={`tab-btn ${activeLeftTab === "timeline" ? "active" : ""}`}
                                    onClick={() => setActiveLeftTab("timeline")}
                                >
                                    <i className="fa-solid fa-brain"></i> Activity Stream
                                </button>
                            </div>
                            {activeLeftTab === "timeline" && (
                                <button className="btn btn-secondary btn-sm" onClick={handleClearTimeline}>Clear</button>
                            )}
                        </div>

                        <div className="card-content">
                            {activeLeftTab === "architecture" && isRunning && projects.length === 0 && !noProjectsWarningDismissed && (
                                <div className="empty-workspace-warning">
                                    <i className="fa-solid fa-triangle-exclamation"></i>
                                    <span>No projects found in <strong>{workspacePath}</strong>. Check that the path contains .csproj files, or dismiss if starting a new project.</span>
                                    <button className="warning-dismiss-btn" onClick={() => setNoProjectsWarningDismissed(true)} title="Dismiss"><i className="fa-solid fa-xmark"></i></button>
                                </div>
                            )}
                            {activeLeftTab === "architecture" ? (
                                <ArchitectureMap
                                    projects={projects}
                                    expandedProjectName={expandedProjectName}
                                    setExpandedProjectName={setExpandedProjectName}
                                    activeCheckedProjects={activeCheckedProjects}
                                    setActiveCheckedProjects={setActiveCheckedProjects}
                                />
                            ) : (
                                <div className="timeline-container">
                                    {timelineItems.length === 0 ? (
                                        <div className="timeline-empty">
                                            <i className="fa-solid fa-bolt empty-icon"></i>
                                            <p>No LLM activity detected yet.</p>
                                        </div>
                                    ) : (
                                        timelineItems.map((item, idx) => (
                                            <div key={idx} className={`timeline-item ${item.ActivityType.toLowerCase()}`}>
                                                <div className="timeline-body">
                                                    <div className="timeline-meta">
                                                        <span className="timeline-type">{item.ActivityType}</span>
                                                        <span className="timeline-time">{new Date(item.Timestamp).toLocaleTimeString()}</span>
                                                    </div>
                                                    <div className="timeline-text">{item.Content}</div>
                                                </div>
                                            </div>
                                        ))
                                    )}
                                </div>
                            )}
                        </div>
                    </section>

                    <InspectionPanel 
                        activeTab={activeRightTab}
                        setActiveTab={setActiveRightTab}
                        violations={violations}
                        changedFiles={changedFiles}
                        activeFileDiffPath={activeFileDiffPath}
                        setActiveFileDiffPath={setActiveFileDiffPath}
                        onClearViolation={handleClearViolation}
                    />
                </div>
            </main>
        </div>
    );
}

// React 18 Mounting
const root = ReactDOM.createRoot(document.getElementById("root"));
root.render(<App />);

const { useState, useEffect, useRef } = React;

function App() {
    const [workspacePath, setWorkspacePath] = useState("c:\\Asterran");
    const [conversationId, setConversationId] = useState("");
    const [isRunning, setIsRunning] = useState(false);
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

    useEffect(() => {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage({ command: "init" });
            window.chrome.webview.addEventListener("message", handleIpcMessage);
        } else {
            // Load mock data when running in browser environments
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
                
                // If there are new active violations, default the inspection tab to guardrails
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
                if (activeFileDiffPath === fileChange.FilePath) {
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
            { ProjectName: "Asterran.Engine", Status: "Green", Dependencies: ["Asterran.Connectors"], SourceFiles: ["EngineController.cs", "ArchitectureAnalyzer.cs", "CodebaseWatcher.cs"] },
            { ProjectName: "Asterran.Viewer", Status: "Green", Dependencies: ["Asterran.Engine", "Asterran.Connectors"], SourceFiles: ["MainWindow.xaml.cs", "App.xaml.cs"] },
            { ProjectName: "Asterran.Test", Status: "Green", Dependencies: ["Asterran.Engine", "Asterran.Connectors"], SourceFiles: ["Program.cs"] }
        ]);
    };

    const postMessageToHost = (message) => {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(message);
        } else {
            console.log("Mock IPC Outbound:", message);
        }
    };

    const handleStartStop = () => {
        postMessageToHost({ command: isRunning ? "stop" : "start" });
    };

    const handleBrowseWorkspace = () => {
        postMessageToHost({ command: "selectWorkspace" });
    };

    const handleConversationIdChange = (val) => {
        setConversationId(val);
        postMessageToHost({ command: "setConversationId", conversationId: val });
    };

    const handleClearTimeline = () => {
        setTimelineItems([]);
    };

    const handleClearViolation = (id) => {
        postMessageToHost({ command: "clearViolation", violationId: id });
    };

    return (
        <div className="app-container">
            <aside className="sidebar">
                <div className="sidebar-header">
                    <div className="logo">
                        <i className="fa-solid fa-compass-drafting logo-icon"></i>
                        <span>Asterran</span>
                    </div>
                    <div className={`status-indicator ${isRunning ? "active" : ""}`}>
                        <span className="pulse-dot"></span>
                        <span className="status-text">{isRunning ? "Monitoring" : "Idle"}</span>
                    </div>
                </div>

                <div className="sidebar-content">
                    <div className="nav-section">
                        <div className="nav-section-title">Control Panel</div>
                        <button className={`btn btn-primary ${isRunning ? "running" : ""}`} onClick={handleStartStop}>
                            <i className={`fa-solid ${isRunning ? "fa-stop" : "fa-play"}`}></i> {isRunning ? "Stop Monitor" : "Start Monitor"}
                        </button>
                    </div>

                    <div className="nav-section">
                        <div className="nav-section-title">Target Workspace</div>
                        <div className="form-group">
                            <label>Path</label>
                            <div className="input-with-button">
                                <input type="text" readOnly value={workspacePath} />
                                <button onClick={handleBrowseWorkspace} title="Browse Workspace"><i className="fa-solid fa-folder-open"></i></button>
                            </div>
                        </div>
                        <div className="form-group">
                            <label>Antigravity Session ID</label>
                            <input 
                                type="text" 
                                value={conversationId} 
                                onChange={(e) => handleConversationIdChange(e.target.value.trim())} 
                                placeholder="Auto-detecting active session..." 
                            />
                            <span className="input-tip">Leave blank to auto-detect the latest log file.</span>
                        </div>
                    </div>

                    <div className="nav-section">
                        <div className="nav-section-title">Task Queue Status</div>
                        <div className="queue-status-box">
                            <div className="metric">
                                <span className="label">Pending/Running Tasks</span>
                                <span className="value">{tasks.length}</span>
                            </div>
                            <div className="task-list-mini">
                                {tasks.length === 0 ? (
                                    <div className="empty-state">Queue is empty</div>
                                ) : (
                                    tasks.map(task => (
                                        <div key={task.TaskId} className={`mini-task-item ${task.Status.toLowerCase()}`}>
                                            <div className="header">
                                                <span>{task.Name}</span>
                                                <span>{task.Progress}%</span>
                                            </div>
                                            <div className="bar-container">
                                                <div className="bar" style={{ width: `${task.Progress}%` }}></div>
                                            </div>
                                            {task.Details && <div className="input-tip">{task.Details}</div>}
                                        </div>
                                    ))
                                )}
                            </div>
                        </div>
                    </div>
                </div>

                <div className="sidebar-footer">
                    <span>Asterran Engine v1.1.0</span>
                </div>
            </aside>

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

function ArchitectureMap({ projects, expandedProjectName, setExpandedProjectName, activeCheckedProjects, setActiveCheckedProjects }) {
    const visibleProjects = projects.filter(p => activeCheckedProjects[p.ProjectName] !== false);

    // Coordinate & layer layouts
    const layers = {};
    visibleProjects.forEach(p => layers[p.ProjectName] = 0);
    
    for (let k = 0; k < visibleProjects.length; k++) {
        let updated = false;
        visibleProjects.forEach(p => {
            p.Dependencies.forEach(dep => {
                if (layers[dep] !== undefined) {
                    if (layers[p.ProjectName] <= layers[dep]) {
                        layers[p.ProjectName] = layers[dep] + 1;
                        updated = true;
                    }
                }
            });
        });
        if (!updated) break;
    }
    
    const projectsByLayer = {};
    visibleProjects.forEach(p => {
        const l = layers[p.ProjectName];
        if (!projectsByLayer[l]) projectsByLayer[l] = [];
        projectsByLayer[l].push(p);
    });
    
    const maxLayer = Math.max(...Object.keys(projectsByLayer).map(Number), 0);
    
    const width = 700;
    const height = 450;
    const positions = {};
    
    const topPadding = 45;
    const bottomPadding = 45;
    const sidePadding = 120;
    
    const layerHeight = maxLayer > 0 ? (height - topPadding - bottomPadding) / maxLayer : 0;
    
    Object.keys(projectsByLayer).forEach(lStr => {
        const l = Number(lStr);
        const layerNodes = projectsByLayer[l].sort((a, b) => a.ProjectName.localeCompare(b.ProjectName));
        const y = topPadding + l * layerHeight;
        
        layerNodes.forEach((proj, idx) => {
            let x = width / 2;
            if (layerNodes.length > 1) {
                x = sidePadding + idx * (width - 2 * sidePadding) / (layerNodes.length - 1);
            }
            positions[proj.ProjectName] = { x, y };
        });
    });
    
    const nodeWidth = 180;
    const nodeHeights = {};
    visibleProjects.forEach(proj => {
        if (proj.ProjectName === expandedProjectName) {
            const fileCount = proj.SourceFiles ? proj.SourceFiles.length : 0;
            nodeHeights[proj.ProjectName] = Math.max(50, 45 + Math.min(fileCount, 8) * 16 + 12);
        } else {
            nodeHeights[proj.ProjectName] = 45;
        }
    });

    const edges = [];
    visibleProjects.forEach(proj => {
        const startPos = positions[proj.ProjectName];
        proj.Dependencies.forEach(depName => {
            const endPos = positions[depName];
            if (startPos && endPos) {
                const sx = startPos.x;
                const sy = startPos.y - nodeHeights[proj.ProjectName] / 2;
                const ex = endPos.x;
                const ey = endPos.y + nodeHeights[depName] / 2;
                
                const depProj = projects.find(p => p.ProjectName === depName);
                const isActive = proj.Status !== "Green" || (depProj && depProj.Status !== "Green");
                const my = sy - (sy - ey) * 0.45;
                
                edges.push({
                    id: `${proj.ProjectName}-${depName}`,
                    d: `M ${sx} ${sy} L ${sx} ${my} L ${ex} ${my} L ${ex} ${ey}`,
                    isActive
                });
            }
        });
    });

    return (
        <div className="architecture-layout">
            <div className="architecture-sidebar">
                <div className="sidebar-title"><i className="fa-solid fa-folder-tree"></i> Solution Projects</div>
                <div className="project-tree">
                    {projects.length === 0 ? (
                        <div className="empty-state">No projects loaded</div>
                    ) : (
                        projects.map(proj => (
                            <label key={proj.ProjectName} className="project-tree-item">
                                <input 
                                    type="checkbox"
                                    checked={activeCheckedProjects[proj.ProjectName] !== false}
                                    onChange={(e) => {
                                        setActiveCheckedProjects(prev => ({
                                            ...prev,
                                            [proj.ProjectName]: e.target.checked
                                        }));
                                    }}
                                />
                                <i className={`fa-solid fa-cube project-cube-icon ${proj.Status.toLowerCase()}`}></i>
                                <span className="project-name-label">{proj.ProjectName}</span>
                            </label>
                        ))
                    )}
                </div>
            </div>

            <div className="architecture-main">
                <div className="svg-wrapper">
                    <svg id="architecture-svg" width="100%" height="100%" viewBox="0 0 700 450">
                        <defs>
                            <marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
                                <path d="M 0 1 L 9 5 L 0 9 z" className="uml-arrowhead" />
                            </marker>
                            <marker id="arrow-active" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
                                <path d="M 0 1 L 9 5 L 0 9 z" className="uml-arrowhead active" />
                            </marker>
                        </defs>

                        {visibleProjects.length === 0 ? (
                            <text x="50%" y="50%" dominantBaseline="middle" textAnchor="middle" fill="#71717a">
                                Please select projects in the sidebar
                            </text>
                        ) : (
                            <>
                                {edges.map(edge => (
                                    <path 
                                        key={edge.id}
                                        d={edge.d}
                                        className={`uml-edge-line ${edge.isActive ? "active" : ""}`}
                                        markerEnd={`url(#${edge.isActive ? "arrow-active" : "arrow"})`}
                                    />
                                ))}

                                {visibleProjects.map(proj => {
                                    const pos = positions[proj.ProjectName];
                                    const h = nodeHeights[proj.ProjectName];
                                    const isExpanded = proj.ProjectName === expandedProjectName;

                                    return (
                                        <g 
                                            key={proj.ProjectName}
                                            transform={`translate(${pos.x - nodeWidth / 2}, ${pos.y - h / 2})`}
                                            onClick={() => setExpandedProjectName(isExpanded ? null : proj.ProjectName)}
                                            style={{ cursor: "pointer" }}
                                        >
                                            <rect 
                                                width={nodeWidth} 
                                                height={h} 
                                                className={`uml-node-rect ${proj.Status.toLowerCase()}`}
                                            />
                                            
                                            {/* Folder Icon */}
                                            <path 
                                                d="M 12 10 L 22 10 C 23.1 10 24 10.9 24 12 L 24 30 C 24 31.1 23.1 32 22 32 L 6 32 C 4.9 32 4 31.1 4 30 L 4 10 C 4 8.9 4.9 8 6 8 L 10 8 L 12 10 Z" 
                                                className={`project-folder-icon ${proj.Status.toLowerCase()}`}
                                                transform="scale(0.7) translate(14, 12)"
                                            />

                                            <text x={42} y={24} className="node-title">
                                                {proj.ProjectName.length > 18 ? proj.ProjectName.substring(0, 16) + "..." : proj.ProjectName}
                                            </text>

                                            {isExpanded && proj.SourceFiles && (
                                                <>
                                                    <line x1={8} y1={36} x2={172} y2={36} stroke="rgba(255, 255, 255, 0.08)" strokeWidth={1} />
                                                    {proj.SourceFiles.slice(0, 8).map((file, fIdx) => (
                                                        <g key={file} transform={`translate(10, ${48 + fIdx * 16})`}>
                                                            <path d="M6 2c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6H6zm7 7V3.5L18.5 9H13z" fill="rgba(255,255,255,0.4)" transform="scale(0.5) translate(0, -4)" />
                                                            <text x={16} y={8} className="node-file-text">
                                                                {file.split('/').pop().length > 22 ? file.split('/').pop().substring(0, 20) + "..." : file.split('/').pop()}
                                                            </text>
                                                        </g>
                                                    ))}
                                                    {proj.SourceFiles.length > 8 && (
                                                        <text x={26} y={48 + 8 * 16 + 6} className="node-file-more-text">
                                                            + {proj.SourceFiles.length - 8} more files...
                                                        </text>
                                                    )}
                                                </>
                                            )}
                                        </g>
                                    );
                                })}
                            </>
                        )}
                    </svg>
                </div>

                <div className="architecture-legend">
                    <div className="legend-item"><span className="legend-dot green"></span> Unchanged</div>
                    <div className="legend-item"><span className="legend-dot yellow"></span> Changing</div>
                    <div className="legend-item"><span className="legend-dot red"></span> Risky / Flagged</div>
                </div>
            </div>
        </div>
    );
}

function InspectionPanel({ activeTab, setActiveTab, violations, changedFiles, activeFileDiffPath, setActiveFileDiffPath, onClearViolation }) {
    const activeFile = activeFileDiffPath ? changedFiles[activeFileDiffPath] : null;

    const selectFileForDiff = (path) => {
        setActiveFileDiffPath(path);
        setActiveTab("diff");
    };

    return (
        <section className="card inspection-card">
            <div className="card-header tab-header">
                <div className="tabs">
                    <button className={`tab-btn ${activeTab === "guardrails" ? "active" : ""}`} onClick={() => setActiveTab("guardrails")}>
                        <i className="fa-solid fa-triangle-exclamation"></i> Guardrails {violations.length > 0 && <span className="badge badge-red">{violations.length}</span>}
                    </button>
                    <button className={`tab-btn ${activeTab === "changes" ? "active" : ""}`} onClick={() => setActiveTab("changes")}>
                        <i className="fa-solid fa-file-code"></i> Live Changes {Object.keys(changedFiles).length > 0 && <span className="badge">{Object.keys(changedFiles).length}</span>}
                    </button>
                    <button className={`tab-btn ${activeTab === "diff" ? "active" : ""}`} onClick={() => setActiveTab("diff")}>
                        <i className="fa-solid fa-code-compare"></i> Diff
                    </button>
                </div>
            </div>

            <div className="card-content">
                {activeTab === "guardrails" && (
                    <div className="guardrails-container">
                        {violations.length === 0 ? (
                            <div className="guardrails-empty">
                                <i className="fa-solid fa-circle-check empty-icon" style={{ color: "#10b981" }}></i>
                                <p>No active guardrail violations.</p>
                                <p className="sub">Audit scans are clean.</p>
                            </div>
                        ) : (
                            violations.map(v => (
                                <div key={v.Id} className="guardrail-violation-item">
                                    <div className="violation-info">
                                        <div className="violation-title">
                                            <i className="fa-solid fa-triangle-exclamation" style={{ color: "#ef4444", marginRight: "6px" }}></i>
                                            {v.RuleName}
                                        </div>
                                        <div className="violation-details">{v.ProjectName} &bull; {v.FilePath.split(/[/\\]/).pop()}</div>
                                        <div className="violation-msg">{v.Message}</div>
                                    </div>
                                    <button className="btn btn-sm btn-clear-violation" onClick={() => onClearViolation(v.Id)}>
                                        <i className="fa-solid fa-check"></i> Clear
                                    </button>
                                </div>
                            ))
                        )}
                    </div>
                )}

                {activeTab === "changes" && (
                    <div className="files-container">
                        {Object.keys(changedFiles).length === 0 ? (
                            <div className="files-empty">
                                <i className="fa-solid fa-magnifying-glass empty-icon"></i>
                                <p>No codebase changes tracked.</p>
                            </div>
                        ) : (
                            Object.values(changedFiles).map(file => (
                                <div 
                                    key={file.FilePath} 
                                    className={`file-item ${activeFileDiffPath === file.FilePath ? "active" : ""} ${file.ChangeType.toLowerCase()}`}
                                    onClick={() => selectFileForDiff(file.FilePath)}
                                >
                                    <div className="file-info">
                                        <i className="fa-solid fa-file-code file-icon"></i>
                                        <div className="file-name-container">
                                            <span className="file-name">{file.RelativePath.split(/[/\\]/).pop()}</span>
                                            <span className="file-path">{file.RelativePath}</span>
                                        </div>
                                    </div>
                                    <span className="badge">+{file.LinesAdded} -{file.LinesDeleted}</span>
                                </div>
                            ))
                        )}
                    </div>
                )}

                {activeTab === "diff" && (
                    <div className="diff-container">
                        {!activeFile ? (
                            <div className="diff-empty">
                                <i className="fa-solid fa-terminal empty-icon"></i>
                                <p>Select a file from the 'Live Changes' tab to inspect diffs.</p>
                            </div>
                        ) : !activeFile.Diff || activeFile.Diff.length === 0 ? (
                            <div className="diff-empty">
                                <i className="fa-solid fa-terminal empty-icon"></i>
                                <p>No lines modified or file content is empty.</p>
                            </div>
                        ) : (
                            <>
                                <div className="diff-file-header-label">{activeFile.RelativePath}</div>
                                <table className="diff-table">
                                    <tbody>
                                        {activeFile.Diff.map((line, lIdx) => {
                                            const rowClass = line.Type.toLowerCase();
                                            const oldNum = line.OldLineNumber > 0 ? line.OldLineNumber : "";
                                            const newNum = line.NewLineNumber > 0 ? line.NewLineNumber : "";
                                            let prefix = " ";
                                            if (line.Type === "Added") prefix = "+";
                                            else if (line.Type === "Deleted") prefix = "-";

                                            return (
                                                <tr key={lIdx} className={`diff-row ${rowClass}`}>
                                                    <td className="diff-num">{oldNum}</td>
                                                    <td className="diff-num">{newNum}</td>
                                                    <td className="diff-content">{prefix} {line.Text}</td>
                                                </tr>
                                            );
                                        })}
                                    </tbody>
                                </table>
                            </>
                        )}
                    </div>
                )}
            </div>
        </section>
    );
}

// React 18 Mounting
const root = ReactDOM.createRoot(document.getElementById("root"));
root.render(<App />);

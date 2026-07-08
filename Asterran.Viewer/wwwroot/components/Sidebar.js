function Sidebar({ isRunning, workspacePath, conversationId, onStartStop, onBrowseWorkspace, onConversationIdChange, tasks }) {
    return (
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
                    <button className={`btn btn-primary ${isRunning ? "running" : ""}`} onClick={onStartStop}>
                        <i className={`fa-solid ${isRunning ? "fa-stop" : "fa-play"}`}></i> {isRunning ? "Stop Monitor" : "Start Monitor"}
                    </button>
                </div>

                <div className="nav-section">
                    <div className="nav-section-title">Target Workspace</div>
                    <div className="form-group">
                        <label>Path</label>
                        <div className="input-with-button">
                            <input type="text" readOnly value={workspacePath} />
                            <button onClick={onBrowseWorkspace} title="Browse Workspace"><i className="fa-solid fa-folder-open"></i></button>
                        </div>
                    </div>
                    <div className="form-group">
                        <label>Antigravity Session ID</label>
                        <input 
                            type="text" 
                            value={conversationId} 
                            onChange={(e) => onConversationIdChange(e.target.value.trim())} 
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
    );
}

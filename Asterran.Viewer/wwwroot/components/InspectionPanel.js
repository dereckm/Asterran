function PathGetFileName(path) {
    if (!path) return "";
    const index = Math.max(path.lastIndexOf("/"), path.lastIndexOf("\\"));
    if (index === -1) return path;
    return path.substring(index + 1);
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
                                        <div className="violation-details">{v.ProjectName} &bull; {PathGetFileName(v.FilePath)}</div>
                                        <div className="violation-msg">{v.Message}</div>
                                    </div>
                                    <div className="violation-actions">
                                        {v.FilePath && changedFiles[v.FilePath] && (
                                            <button className="btn btn-sm btn-view-diff" onClick={() => selectFileForDiff(v.FilePath)} title="View diff for this file">
                                                <i className="fa-solid fa-code-compare"></i> Diff
                                            </button>
                                        )}
                                        <button className="btn btn-sm btn-clear-violation" onClick={() => onClearViolation(v.Id)}>
                                            <i className="fa-solid fa-check"></i> Clear
                                        </button>
                                    </div>
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
                                            <span className="file-name">{PathGetFileName(file.FilePath)}</span>
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

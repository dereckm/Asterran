function ArchitectureMap({ projects, expandedProjectName, setExpandedProjectName, activeCheckedProjects, setActiveCheckedProjects }) {
    const visibleProjects = projects.filter(p => activeCheckedProjects[p.ProjectName] !== false);

    // Compute layout layers & positions
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

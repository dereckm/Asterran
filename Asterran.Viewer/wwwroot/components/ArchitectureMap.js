const { useState, useRef } = React;

function ArchitectureMap({ projects, expandedProjectName, setExpandedProjectName, activeCheckedProjects, setActiveCheckedProjects }) {
    const [zoom, setZoom] = useState(1);
    const [pan, setPan] = useState({ x: 0, y: 0 });
    const [isDragging, setIsDragging] = useState(false);
    const [dragStart, setDragStart] = useState({ x: 0, y: 0 });
    const [expandedFolders, setExpandedFolders] = useState({});

    const handleMouseDown = (e) => {
        if (e.button !== 0) return;
        if (e.target.closest('.map-controls') || e.target.closest('input') || e.target.closest('button')) {
            return;
        }
        setIsDragging(true);
        setDragStart({ x: e.clientX - pan.x, y: e.clientY - pan.y });
    };

    const handleMouseMove = (e) => {
        if (!isDragging) return;
        setPan({
            x: e.clientX - dragStart.x,
            y: e.clientY - dragStart.y
        });
    };

    const handleMouseUp = () => {
        setIsDragging(false);
    };

    const handleWheel = (e) => {
        const zoomFactor = 1.08;
        let newZoom = zoom;
        if (e.deltaY < 0) {
            newZoom = Math.min(zoom * zoomFactor, 3.0);
        } else {
            newZoom = Math.max(zoom / zoomFactor, 0.4);
        }
        setZoom(newZoom);
    };

    const zoomIn = () => setZoom(z => Math.min(z * 1.15, 3.0));
    const zoomOut = () => setZoom(z => Math.max(z / 1.15, 0.4));
    const resetView = () => {
        setZoom(1);
        setPan({ x: 0, y: 0 });
    };

    const toggleFolder = (e, key) => {
        e.stopPropagation();
        setExpandedFolders(prev => ({
            ...prev,
            [key]: !prev[key]
        }));
    };

    const visibleProjects = projects.filter(p => activeCheckedProjects[p.ProjectName] !== false);

    const groupProjectFiles = (proj) => {
        const rootFiles = [];
        const folderGroups = {};

        if (proj.SourceFiles) {
            proj.SourceFiles.forEach(file => {
                const parts = file.split('/');
                if (parts.length === 1) {
                    rootFiles.push(file);
                } else {
                    const folder = parts[0];
                    if (!folderGroups[folder]) {
                        folderGroups[folder] = [];
                    }
                    folderGroups[folder].push(file);
                }
            });
        }

        return { rootFiles, folderGroups };
    };

    // Calculate layout ranks & coordinates
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
    
    const nodeWidth = 200; // expanded slightly for nesting layout space
    const nodeHeights = {};
    visibleProjects.forEach(proj => {
        if (proj.ProjectName === expandedProjectName) {
            const { rootFiles, folderGroups } = groupProjectFiles(proj);
            let currentY = 40;

            currentY += rootFiles.length * 16;

            Object.keys(folderGroups).forEach(folderName => {
                const files = folderGroups[folderName];
                const key = `${proj.ProjectName}-${folderName}`;
                const isExpanded = expandedFolders[key] === true;
                const visibleCount = isExpanded ? files.length : Math.min(files.length, 4);

                currentY += 24; 
                currentY += visibleCount * 16;
                if (files.length > 4) {
                    currentY += 16; 
                }
                currentY += 16; 
            });

            nodeHeights[proj.ProjectName] = Math.max(50, currentY + 10);
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
                            <label key={proj.ProjectName} className="project-tree-item" title={proj.ProjectName}>
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

            <div className="architecture-main" 
                 onMouseDown={handleMouseDown}
                 onMouseMove={handleMouseMove}
                 onMouseUp={handleMouseUp}
                 onMouseLeave={handleMouseUp}
                 onWheel={handleWheel}
                 style={{ cursor: isDragging ? "grabbing" : "grab" }}>
                
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
                            <g transform={`translate(${pan.x}, ${pan.y}) scale(${zoom})`}>
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

                                    // Pre-calculate grouped files for expanded nodes
                                    const { rootFiles, folderGroups } = isExpanded ? groupProjectFiles(proj) : { rootFiles: [], folderGroups: {} };
                                    let currentY = 40;

                                    return (
                                        <g 
                                            key={proj.ProjectName}
                                            transform={`translate(${pos.x - nodeWidth / 2}, ${pos.y - h / 2})`}
                                            onClick={() => setExpandedProjectName(isExpanded ? null : proj.ProjectName)}
                                            style={{ cursor: "pointer" }}
                                        >
                                            {/* Outer Card Background */}
                                            <rect 
                                                width={nodeWidth} 
                                                height={h} 
                                                className={`uml-node-rect ${proj.Status.toLowerCase()}`}
                                            />
                                            
                                            {/* Folder icon */}
                                            <path 
                                                d="M 12 10 L 22 10 C 23.1 10 24 10.9 24 12 L 24 30 C 24 31.1 23.1 32 22 32 L 6 32 C 4.9 32 4 31.1 4 30 L 4 10 C 4 8.9 4.9 8 6 8 L 10 8 L 12 10 Z" 
                                                className={`project-folder-icon ${proj.Status.toLowerCase()}`}
                                                transform="scale(0.7) translate(14, 12)"
                                            />

                                            <text x={42} y={24} className="node-title">
                                                {proj.ProjectName}
                                            </text>

                                            {isExpanded && (
                                                <>
                                                    <line x1={8} y1={36} x2={192} y2={36} stroke="rgba(255, 255, 255, 0.08)" strokeWidth={1} />
                                                    
                                                    {/* Draw root level files */}
                                                    {rootFiles.map(file => {
                                                        const y = currentY;
                                                        currentY += 16;
                                                        return (
                                                            <g key={file} transform={`translate(10, ${y})`}>
                                                                <path d="M6 2c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6H6zm7 7V3.5L18.5 9H13z" fill="rgba(255,255,255,0.4)" transform="scale(0.5) translate(0, -4)" />
                                                                <text x={16} y={8} className="node-file-text">
                                                                    {file}
                                                                </text>
                                                            </g>
                                                        );
                                                    })}

                                                    {/* Draw Folder Groups (nested nodes within node) */}
                                                    {Object.keys(folderGroups).map(folderName => {
                                                        const files = folderGroups[folderName];
                                                        const key = `${proj.ProjectName}-${folderName}`;
                                                        const isFolderExpanded = expandedFolders[key] === true;
                                                        const visibleCount = isFolderExpanded ? files.length : Math.min(files.length, 4);
                                                        
                                                        const folderY = currentY;
                                                        
                                                        // Render files inside subfolder
                                                        const filesJsx = files.slice(0, visibleCount).map((file, fIdx) => (
                                                            <g key={file} transform={`translate(18, ${folderY + 22 + fIdx * 16})`}>
                                                                <path d="M6 2c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6H6zm7 7V3.5L18.5 9H13z" fill="rgba(255,255,255,0.3)" transform="scale(0.4) translate(0, -4)" />
                                                                <text x={14} y={6} className="node-file-text" style={{ fontSize: "8px" }}>
                                                                    {file.split('/').pop()}
                                                                </text>
                                                            </g>
                                                        ));

                                                        let clickLinkJsx = null;
                                                        if (files.length > 4) {
                                                            const clickY = folderY + 22 + visibleCount * 16;
                                                            clickLinkJsx = (
                                                                <text 
                                                                    x={32} 
                                                                    y={clickY + 6} 
                                                                    className="node-file-link" 
                                                                    onClick={(e) => toggleFolder(e, key)}
                                                                >
                                                                    {isFolderExpanded ? "Collapse files" : `+ ${files.length - 4} more files...`}
                                                                </text>
                                                            );
                                                        }

                                                        // Compute folder box heights dynamically
                                                        const boxHeight = 22 + visibleCount * 16 + (files.length > 4 ? 16 : 0) + 6;
                                                        currentY += boxHeight + 16; // file items + spacing

                                                        return (
                                                            <g key={folderName}>
                                                                {/* Inner nested directory rectangle card */}
                                                                <rect 
                                                                    x={10} 
                                                                    y={folderY} 
                                                                    width={180} 
                                                                    height={boxHeight} 
                                                                    className="nested-folder-rect"
                                                                />
                                                                
                                                                {/* Folder name header */}
                                                                <path d="M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z" fill="var(--accent-purple)" fillOpacity="0.45" transform="scale(0.55) translate(28, 14)" />
                                                                <text x={34} y={folderY + 13} className="node-folder-title">
                                                                    {folderName}/
                                                                </text>

                                                                {filesJsx}
                                                                {clickLinkJsx}
                                                            </g>
                                                        );
                                                    })}
                                                </>
                                            )}
                                        </g>
                                    );
                                })}
                            </g>
                        )}
                    </svg>
                </div>

                {/* Floating Pan & Zoom controls */}
                <div className="map-controls">
                    <button className="control-btn" onClick={zoomIn} title="Zoom In"><i className="fa-solid fa-plus"></i></button>
                    <button className="control-btn" onClick={zoomOut} title="Zoom Out"><i className="fa-solid fa-minus"></i></button>
                    <button className="control-btn" onClick={resetView} title="Reset View"><i className="fa-solid fa-arrows-to-dot"></i></button>
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

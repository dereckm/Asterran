using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Asterran.Engine.Guardrails;

namespace Asterran.Engine
{
    public class ProjectNode
    {
        public string ProjectName { get; set; }
        public string ProjectPath { get; set; }
        public string Status { get; set; } = "Green"; // Green, Yellow, Red
        public List<string> Dependencies { get; set; } = new List<string>();
        public DateTime LastModified { get; set; } = DateTime.MinValue;
        public string FlagReason { get; set; }
        public List<string> SourceFiles { get; set; } = new List<string>();
    }

    public class GuardrailViolation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectName { get; set; }
        public string FilePath { get; set; }
        public string RuleName { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class ArchitectureAnalyzer
    {
        private readonly string _workspacePath;
        private readonly Dictionary<string, ProjectNode> _projects = new Dictionary<string, ProjectNode>();
        private readonly List<GuardrailViolation> _violations = new List<GuardrailViolation>();
        private readonly object _lock = new object();
        private readonly GuardrailEngine _guardrailEngine = new GuardrailEngine();

        public ArchitectureAnalyzer(string workspacePath)
        {
            _workspacePath = Path.GetFullPath(workspacePath);
            ScanWorkspace();
        }

        public void ScanWorkspace()
        {
            lock (_lock)
            {
                _projects.Clear();
                if (!Directory.Exists(_workspacePath)) return;

                try
                {
                    var csprojFiles = Directory.GetFiles(_workspacePath, "*.csproj", SearchOption.AllDirectories);
                    
                    foreach (var file in csprojFiles)
                    {
                        string projectName = Path.GetFileNameWithoutExtension(file);
                        _projects[projectName] = new ProjectNode
                        {
                            ProjectName = projectName,
                            ProjectPath = file,
                            Status = "Green",
                            Dependencies = new List<string>(),
                            SourceFiles = new List<string>()
                        };
                    }

                    foreach (var file in csprojFiles)
                    {
                        string projectName = Path.GetFileNameWithoutExtension(file);
                        
                        string xml = File.ReadAllText(file);
                        var matches = Regex.Matches(xml, @"<ProjectReference\s+Include=[""']([^""']+)[""']");
                        foreach (Match match in matches)
                        {
                            string refPath = match.Groups[1].Value;
                            string refProjectName = Path.GetFileNameWithoutExtension(refPath);
                            
                            if (_projects.ContainsKey(refProjectName))
                            {
                                _projects[projectName].Dependencies.Add(refProjectName);
                            }
                        }

                        string projDir = Path.GetDirectoryName(file);
                        var filesList = new List<string>();
                        if (Directory.Exists(projDir))
                        {
                            var allFiles = Directory.GetFiles(projDir, "*.*", SearchOption.AllDirectories);
                            foreach (var f in allFiles)
                            {
                                string ext = Path.GetExtension(f).ToLower();
                                if (ext == ".cs" || ext == ".xaml" || ext == ".html" || ext == ".css" || ext == ".js" || ext == ".json")
                                {
                                    string relPath = Path.GetRelativePath(projDir, f);
                                    
                                    if (!relPath.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                                        !relPath.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                                        !relPath.StartsWith(".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                                        !relPath.StartsWith(".vs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                                        !relPath.StartsWith("TestWorkspace" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                                    {
                                        filesList.Add(relPath.Replace('\\', '/'));
                                    }
                                }
                            }
                        }
                        filesList.Sort();
                        _projects[projectName].SourceFiles = filesList;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error scanning architecture: {ex.Message}");
                }
            }
        }

        public List<ProjectNode> GetArchitectureState()
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                var list = new List<ProjectNode>();

                foreach (var kvp in _projects)
                {
                    var project = kvp.Value;
                    
                    // Check active violations for this project
                    var projViolations = _violations.Where(v => v.ProjectName == project.ProjectName).ToList();
                    if (projViolations.Any())
                    {
                        project.Status = "Red";
                        project.FlagReason = string.Join("; ", projViolations.Select(v => v.Message));
                    }
                    else if (project.Status == "Red")
                    {
                        // Reset to Green once all violations are cleared
                        project.Status = "Green";
                        project.FlagReason = null;
                    }

                    if (project.Status == "Yellow")
                    {
                        if (now - project.LastModified > TimeSpan.FromSeconds(15))
                        {
                            project.Status = "Green";
                        }
                    }

                    list.Add(new ProjectNode
                    {
                        ProjectName = project.ProjectName,
                        ProjectPath = project.ProjectPath,
                        Status = project.Status,
                        Dependencies = project.Dependencies.ToList(),
                        LastModified = project.LastModified,
                        FlagReason = project.FlagReason,
                        SourceFiles = project.SourceFiles.ToList()
                    });
                }

                return list;
            }
        }

        public string GetProjectForFile(string filePath)
        {
            lock (_lock)
            {
                string targetDir = Path.GetDirectoryName(filePath);
                string bestMatchProject = null;
                int bestMatchDepth = -1;

                foreach (var kvp in _projects)
                {
                    string projDir = Path.GetDirectoryName(kvp.Value.ProjectPath);
                    if (targetDir.StartsWith(projDir, StringComparison.OrdinalIgnoreCase))
                    {
                        int depth = projDir.Split(Path.DirectorySeparatorChar).Length;
                        if (depth > bestMatchDepth)
                        {
                            bestMatchDepth = depth;
                            bestMatchProject = kvp.Key;
                        }
                    }
                }

                return bestMatchProject;
            }
        }

        public void SetProjectChanging(string filePath)
        {
            lock (_lock)
            {
                string projectName = GetProjectForFile(filePath);
                if (string.IsNullOrEmpty(projectName) || !_projects.ContainsKey(projectName)) return;

                var project = _projects[projectName];
                
                // If it has active violations (Red), keep it Red, but update activity time
                if (project.Status != "Red")
                {
                    project.Status = "Yellow";
                }
                project.LastModified = DateTime.Now;
            }
        }

        public void NotifyFileEdit(string filePath, int linesAdded, int linesDeleted, List<DiffLine> diff)
        {
            lock (_lock)
            {
                string projectName = GetProjectForFile(filePath);
                if (string.IsNullOrEmpty(projectName) || !_projects.ContainsKey(projectName)) return;

                var project = _projects[projectName];
                project.LastModified = DateTime.Now;

                var result = ClassifyChangeRisk(filePath, linesAdded, linesDeleted, diff);
                if (result.IsViolated)
                {
                    bool exists = _violations.Any(v => v.ProjectName == projectName && v.FilePath == filePath && v.Message == result.Message);
                    if (!exists)
                    {
                        _violations.Add(new GuardrailViolation
                        {
                            ProjectName = projectName,
                            FilePath = filePath,
                            RuleName = result.RuleName ?? "Safety Guardrail",
                            Message = result.Message
                        });
                    }
                }
            }
        }

        public List<GuardrailViolation> GetActiveViolations()
        {
            lock (_lock)
            {
                return _violations.ToList();
            }
        }

        public void ClearViolation(string violationId)
        {
            lock (_lock)
            {
                _violations.RemoveAll(v => v.Id == violationId);
            }
        }

        private GuardrailResult ClassifyChangeRisk(string filePath, int linesAdded, int linesDeleted, List<DiffLine> diff)
        {
            int totalLines = linesAdded + diff.Count(d => d.Type == "Unchanged");
            if (linesDeleted > 10 && linesDeleted > totalLines * 0.5)
            {
                return new GuardrailResult
                {
                    IsViolated = true,
                    RuleName = "Massive Deletions Guardrail",
                    Message = $"Guardrail violated: Massive code deletion ({linesDeleted} lines deleted, >50% of file)"
                };
            }

            string newContent = "";
            if (File.Exists(filePath))
            {
                try
                {
                    newContent = File.ReadAllText(filePath);
                }
                catch
                {
                    newContent = string.Join("\n", diff.Where(d => d.Type != "Deleted").Select(d => d.Text));
                }
            }
            else
            {
                newContent = string.Join("\n", diff.Where(d => d.Type != "Deleted").Select(d => d.Text));
            }

            string addedCode = string.Join("\n", diff.Where(d => d.Type == "Added").Select(d => d.Text));
            var result = _guardrailEngine.Evaluate(filePath, newContent, addedCode);
            if (result.IsViolated)
            {
                return result;
            }

            return new GuardrailResult { IsViolated = false };
        }
    }
}

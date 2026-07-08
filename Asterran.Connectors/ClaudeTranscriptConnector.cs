using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Asterran.Connectors
{
    public class ClaudeTranscriptConnector : ILlmConnector
    {
        public event EventHandler<LlmActivityEventArgs>? OnActivity;
        private readonly string _projectsRoot;
        private readonly string? _sessionId;
        private string? _targetFilePath;
        private FileSystemWatcher? _watcher;
        private long _lastPosition = 0;
        private int _stepIndex = 0;
        private readonly object _lock = new object();
        private CancellationTokenSource? _cts;

        public ClaudeTranscriptConnector(string? sessionId = null)
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _projectsRoot = Path.Combine(userProfile, ".claude", "projects");
            _sessionId = sessionId;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _lastPosition = 0;
            _stepIndex = 0;

            _targetFilePath = !string.IsNullOrEmpty(_sessionId)
                ? FindSessionFile(_sessionId)
                : FindMostRecentSession();

            if (string.IsNullOrEmpty(_targetFilePath) || !File.Exists(_targetFilePath))
            {
                Console.WriteLine("No Claude session file found. Monitoring for new sessions...");
                StartRootWatcher();
                return;
            }

            StartFileWatcher(_targetFilePath);
        }

        public void Stop()
        {
            _cts?.Cancel();
            lock (_lock)
            {
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                    _watcher = null;
                }
            }
        }

        private string? FindSessionFile(string sessionId)
        {
            if (!Directory.Exists(_projectsRoot)) return null;
            try
            {
                foreach (var dir in Directory.GetDirectories(_projectsRoot))
                {
                    string candidate = Path.Combine(dir, sessionId + ".jsonl");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding Claude session: {ex.Message}");
            }
            return null;
        }

        private string? FindMostRecentSession()
        {
            if (!Directory.Exists(_projectsRoot)) return null;
            string? mostRecent = null;
            DateTime lastWrite = DateTime.MinValue;
            try
            {
                foreach (var file in Directory.GetFiles(_projectsRoot, "*.jsonl", SearchOption.AllDirectories))
                {
                    var wt = File.GetLastWriteTime(file);
                    if (wt > lastWrite)
                    {
                        lastWrite = wt;
                        mostRecent = file;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning Claude projects: {ex.Message}");
            }
            return mostRecent;
        }

        private void StartRootWatcher()
        {
            lock (_lock)
            {
                if (_watcher != null) _watcher.Dispose();

                if (!Directory.Exists(_projectsRoot))
                {
                    try { Directory.CreateDirectory(_projectsRoot); }
                    catch { return; }
                }

                _watcher = new FileSystemWatcher(_projectsRoot)
                {
                    IncludeSubdirectories = true,
                    Filter = "*.jsonl",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
                };

                _watcher.Created += (s, e) =>
                {
                    Console.WriteLine($"Detected new Claude session: {e.FullPath}");
                    Stop();
                    StartFileWatcher(e.FullPath);
                };

                _watcher.EnableRaisingEvents = true;
            }
        }

        private void StartFileWatcher(string filePath)
        {
            _targetFilePath = filePath;
            Console.WriteLine($"Watching Claude session: {filePath}");

            ReadNewLines();

            lock (_lock)
            {
                if (_watcher != null) _watcher.Dispose();

                string dir = Path.GetDirectoryName(filePath)!;
                string filename = Path.GetFileName(filePath);

                _watcher = new FileSystemWatcher(dir, filename)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };

                _watcher.Changed += (s, e) => ReadNewLines();
                _watcher.EnableRaisingEvents = true;
            }
        }

        private void ReadNewLines()
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(_targetFilePath) || !File.Exists(_targetFilePath)) return;
                try
                {
                    using var fs = new FileStream(_targetFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (fs.Length < _lastPosition) _lastPosition = 0;
                    fs.Seek(_lastPosition, SeekOrigin.Begin);
                    using var reader = new StreamReader(fs, Encoding.UTF8);
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line)) ParseAndEmitLine(line);
                    }
                    _lastPosition = fs.Position;
                }
                catch (IOException) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading Claude session: {ex.Message}");
                }
            }
        }

        private void ParseAndEmitLine(string jsonLine)
        {
            try
            {
                var doc = JsonDocument.Parse(jsonLine);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeProp)) return;
                string? type = typeProp.GetString();

                if (!root.TryGetProperty("message", out var msgProp)) return;

                if (type == "user")
                {
                    string? text = ExtractTextContent(msgProp);
                    if (!string.IsNullOrEmpty(text))
                    {
                        OnActivity?.Invoke(this, new LlmActivityEventArgs
                        {
                            ActivityType = "Prompt",
                            Content = text,
                            Source = "User",
                            Timestamp = ParseTimestamp(root),
                            StepIndex = _stepIndex++
                        });
                    }
                }
                else if (type == "assistant")
                {
                    if (!msgProp.TryGetProperty("content", out var contentArr)
                        || contentArr.ValueKind != JsonValueKind.Array)
                        return;

                    string? thoughtText = null;

                    foreach (var block in contentArr.EnumerateArray())
                    {
                        if (!block.TryGetProperty("type", out var blockTypeProp)) continue;
                        string? blockType = blockTypeProp.GetString();

                        if (blockType == "text")
                        {
                            if (block.TryGetProperty("text", out var textProp))
                                thoughtText = textProp.GetString();
                        }
                        else if (blockType == "tool_use")
                        {
                            EmitToolUseAction(block, root);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(thoughtText))
                    {
                        OnActivity?.Invoke(this, new LlmActivityEventArgs
                        {
                            ActivityType = "Thought",
                            Content = thoughtText,
                            Source = "Model",
                            Timestamp = ParseTimestamp(root),
                            StepIndex = _stepIndex++
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing Claude JSONL line: {ex.Message}");
            }
        }

        private void EmitToolUseAction(JsonElement toolBlock, JsonElement root)
        {
            if (!toolBlock.TryGetProperty("name", out var nameProp)) return;
            string toolName = nameProp.GetString() ?? "";

            string? targetFile = null;
            string content = $"Tool: {toolName}";

            if (toolBlock.TryGetProperty("input", out var inputProp) && inputProp.ValueKind == JsonValueKind.Object)
            {
                if (inputProp.TryGetProperty("file_path", out var fpProp))
                {
                    targetFile = fpProp.GetString();
                    content = $"Tool: {toolName} \u2192 {Path.GetFileName(targetFile)}";
                }
                else if (inputProp.TryGetProperty("command", out var cmdProp))
                {
                    string cmd = cmdProp.GetString() ?? "";
                    content = $"Tool: {toolName} | {(cmd.Length > 80 ? cmd.Substring(0, 80) + "\u2026" : cmd)}";
                }
                else if (inputProp.TryGetProperty("pattern", out var patProp))
                {
                    content = $"Tool: {toolName} | {patProp.GetString() ?? ""}";
                }
            }

            OnActivity?.Invoke(this, new LlmActivityEventArgs
            {
                ActivityType = "Action",
                Content = content,
                Source = "Model",
                Timestamp = ParseTimestamp(root),
                StepIndex = _stepIndex++,
                TargetFile = targetFile
            });
        }

        private static string? ExtractTextContent(JsonElement msgElement)
        {
            if (!msgElement.TryGetProperty("content", out var content)) return null;

            if (content.ValueKind == JsonValueKind.String)
                return content.GetString();

            if (content.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in content.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out var t) && t.GetString() == "text"
                        && block.TryGetProperty("text", out var text))
                        return text.GetString();
                }
            }

            return null;
        }

        private static DateTime ParseTimestamp(JsonElement root)
        {
            if (root.TryGetProperty("timestamp", out var tsProp)
                && DateTime.TryParse(tsProp.GetString() ?? "", out var dt))
                return dt;
            return DateTime.Now;
        }
    }
}

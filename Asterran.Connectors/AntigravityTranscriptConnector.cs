using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Asterran.Connectors
{
    public class AntigravityTranscriptConnector : ILlmConnector
    {
        public event EventHandler<LlmActivityEventArgs> OnActivity;
        private string _targetFilePath;
        private string _brainRootPath;
        private FileSystemWatcher _watcher;
        private long _lastPosition = 0;
        private CancellationTokenSource _cts;
        private readonly object _lock = new object();

        public AntigravityTranscriptConnector(string specificConversationId = null)
        {
            // Dynamically resolve the user profile directory (e.g., C:\Users\K24)
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _brainRootPath = Path.Combine(userProfile, ".gemini", "antigravity", "brain");

            if (!string.IsNullOrEmpty(specificConversationId))
            {
                _targetFilePath = Path.Combine(_brainRootPath, specificConversationId, ".system_generated", "logs", "transcript.jsonl");
            }
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();

            if (string.IsNullOrEmpty(_targetFilePath))
            {
                // Auto-detect the most recent conversation log
                _targetFilePath = FindMostRecentTranscript();
            }

            if (string.IsNullOrEmpty(_targetFilePath) || !File.Exists(_targetFilePath))
            {
                Console.WriteLine("No transcript file found to watch. Monitoring the brain folder for updates...");
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

        private string FindMostRecentTranscript()
        {
            if (!Directory.Exists(_brainRootPath)) return null;

            string mostRecentFile = null;
            DateTime lastWriteTime = DateTime.MinValue;

            try
            {
                var folders = Directory.GetDirectories(_brainRootPath);
                foreach (var folder in folders)
                {
                    string logFile = Path.Combine(folder, ".system_generated", "logs", "transcript.jsonl");
                    if (File.Exists(logFile))
                    {
                        var writeTime = File.GetLastWriteTime(logFile);
                        if (writeTime > lastWriteTime)
                        {
                            lastWriteTime = writeTime;
                            mostRecentFile = logFile;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning brain directory: {ex.Message}");
            }

            return mostRecentFile;
        }

        private void StartRootWatcher()
        {
            lock (_lock)
            {
                if (_watcher != null) _watcher.Dispose();

                if (!Directory.Exists(_brainRootPath))
                {
                    try
                    {
                        Directory.CreateDirectory(_brainRootPath);
                    }
                    catch { return; }
                }

                _watcher = new FileSystemWatcher(_brainRootPath)
                {
                    IncludeSubdirectories = true,
                    Filter = "transcript.jsonl",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
                };

                _watcher.Created += (s, e) => {
                    Console.WriteLine($"Detected new transcript file created: {e.FullPath}");
                    Stop();
                    StartFileWatcher(e.FullPath);
                };

                _watcher.EnableRaisingEvents = true;
            }
        }

        private void StartFileWatcher(string filePath)
        {
            _targetFilePath = filePath;
            Console.WriteLine($"Watching transcript file: {filePath}");

            // Read existing contents first
            ReadNewLines();

            lock (_lock)
            {
                if (_watcher != null) _watcher.Dispose();

                string directory = Path.GetDirectoryName(filePath);
                string filename = Path.GetFileName(filePath);

                _watcher = new FileSystemWatcher(directory, filename)
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
                    using (var fs = new FileStream(_targetFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        if (fs.Length < _lastPosition)
                        {
                            // File was truncated or restarted
                            _lastPosition = 0;
                        }

                        fs.Seek(_lastPosition, SeekOrigin.Begin);

                        using (var reader = new StreamReader(fs, Encoding.UTF8))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                if (string.IsNullOrWhiteSpace(line)) continue;
                                ParseAndEmitLine(line);
                            }

                            _lastPosition = fs.Position;
                        }
                    }
                }
                catch (IOException)
                {
                    // File may be locked temporarily, we will retry on the next event
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading transcript lines: {ex.Message}");
                }
            }
        }

        private void ParseAndEmitLine(string jsonLine)
        {
            try
            {
                var doc = JsonDocument.Parse(jsonLine);
                var root = doc.RootElement;

                int stepIndex = root.TryGetProperty("step_index", out var stepProp) ? stepProp.GetInt32() : 0;
                string source = root.TryGetProperty("source", out var srcProp) ? srcProp.GetString() : "";
                string type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "";
                string content = root.TryGetProperty("content", out var contentProp) ? contentProp.GetString() : "";

                string activityType = null;
                string mappedSource = null;

                if (type == "USER_INPUT")
                {
                    activityType = "Prompt";
                    mappedSource = "User";
                }
                else if (type == "PLANNER_RESPONSE")
                {
                    activityType = "Thought";
                    mappedSource = "Model";
                }
                else if (type == "RUN_COMMAND" || type == "WRITE_FILE" || type == "REPLACE_FILE" || type == "LIST_DIRECTORY" || type == "VIEW_FILE")
                {
                    activityType = "Action";
                    mappedSource = "System"; // The output of the action
                }

                // If it is a planner response, we can also extract if it has tool calls and raise them as actions
                if (type == "PLANNER_RESPONSE" && root.TryGetProperty("tool_calls", out var toolsProp) && toolsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tool in toolsProp.EnumerateArray())
                    {
                        if (tool.TryGetProperty("name", out var nameProp))
                        {
                            string toolName = nameProp.GetString();
                            string toolSummary = "";
                            string targetFile = null;

                            if (tool.TryGetProperty("args", out var argsProp) && argsProp.ValueKind == JsonValueKind.Object)
                            {
                                if (argsProp.TryGetProperty("toolSummary", out var sumProp))
                                {
                                    toolSummary = sumProp.GetString();
                                }
                                if (argsProp.TryGetProperty("TargetFile", out var fileProp))
                                {
                                    targetFile = fileProp.GetString();
                                }
                            }

                            OnActivity?.Invoke(this, new LlmActivityEventArgs
                            {
                                ActivityType = "Action",
                                Content = $"LLM calls tool: {toolName} ({(string.IsNullOrEmpty(toolSummary) ? "No summary" : toolSummary)})",
                                Source = "Model",
                                Timestamp = DateTime.Now,
                                StepIndex = stepIndex,
                                TargetFile = targetFile
                            });
                        }
                    }
                }

                if (activityType != null)
                {
                    OnActivity?.Invoke(this, new LlmActivityEventArgs
                    {
                        ActivityType = activityType,
                        Content = content,
                        Source = mappedSource,
                        Timestamp = DateTime.Now,
                        StepIndex = stepIndex
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing JSON transcript line: {ex.Message}");
            }
        }
    }
}

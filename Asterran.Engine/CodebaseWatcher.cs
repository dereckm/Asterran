using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Asterran.Engine
{
    public class DiffLine
    {
        public string Type { get; set; } // "Added", "Deleted", "Unchanged"
        public string Text { get; set; }
        public int OldLineNumber { get; set; }
        public int NewLineNumber { get; set; }
    }

    public class FileChangeEventArgs : EventArgs
    {
        public string FilePath { get; set; }
        public string RelativePath { get; set; }
        public string ChangeType { get; set; } // "Created", "Modified", "Deleted"
        public DateTime Timestamp { get; set; }
        public int LinesAdded { get; set; }
        public int LinesDeleted { get; set; }
        public List<DiffLine> Diff { get; set; }
    }

    public class CodebaseWatcher
    {
        private readonly string _workspacePath;
        private FileSystemWatcher _watcher;
        private readonly ConcurrentDictionary<string, string[]> _fileContentCache = new ConcurrentDictionary<string, string[]>();
        private readonly ConcurrentDictionary<string, Timer> _debouncers = new ConcurrentDictionary<string, Timer>();
        private readonly TaskQueue _taskQueue;

        public event EventHandler<FileChangeEventArgs> FileChanged;

        public CodebaseWatcher(string workspacePath, TaskQueue taskQueue)
        {
            _workspacePath = Path.GetFullPath(workspacePath);
            _taskQueue = taskQueue;
        }

        public void Start()
        {
            if (!Directory.Exists(_workspacePath))
            {
                Directory.CreateDirectory(_workspacePath);
            }

            // Initialize cache with existing files
            InitializeCache();

            _watcher = new FileSystemWatcher(_workspacePath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                InternalBufferSize = 65536
            };

            _watcher.Created += OnFileSystemEvent;
            _watcher.Changed += OnFileSystemEvent;
            _watcher.Deleted += OnFileSystemEvent;
            _watcher.Renamed += OnFileSystemRenamed;

            _watcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            foreach (var timer in _debouncers.Values)
            {
                timer.Dispose();
            }
            _debouncers.Clear();
        }

        private void InitializeCache()
        {
            try
            {
                var files = Directory.GetFiles(_workspacePath, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (ShouldIgnore(file)) continue;

                    try
                    {
                        string[] lines = File.ReadAllLines(file);
                        _fileContentCache[file] = lines;
                    }
                    catch
                    {
                        // Ignore files that can't be read during initialization
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing cache: {ex.Message}");
            }
        }

        private bool ShouldIgnore(string path)
        {
            string relativePath = Path.GetRelativePath(_workspacePath, path);
            var parts = relativePath.Split(Path.DirectorySeparatorChar);

            // Ignore common directories
            if (parts.Any(p => p == ".git" || p == "bin" || p == "obj" || p == ".vs" ||
                              p == "node_modules" || p == ".gemini" || p == ".claude" || p == ".agents" ||
                              p == "packages" || p == ".idea" || p == "out" || p == "dist"))
            {
                return true;
            }

            // Ignore binary file extensions
            string ext = Path.GetExtension(path).ToLower();
            string[] binaryExtensions = { ".exe", ".dll", ".pdb", ".png", ".jpg", ".jpeg", ".gif", 
                                          ".ico", ".zip", ".rar", ".7z", ".mp3", ".mp4", ".pdf", 
                                          ".xlsx", ".docx", ".suo", ".user", ".db", ".sqlite" };
            if (binaryExtensions.Contains(ext))
            {
                return true;
            }

            return false;
        }

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            if (ShouldIgnore(e.FullPath)) return;

            // Debounce event to handle rapid edits or multiple OS triggers
            _debouncers.AddOrUpdate(e.FullPath,
                path => new Timer(DebounceCallback, e, 300, Timeout.Infinite),
                (path, existingTimer) =>
                {
                    existingTimer.Change(300, Timeout.Infinite);
                    return existingTimer;
                });
        }

        private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
        {
            // Treat rename as deletion of old and creation of new
            OnFileSystemEvent(sender, new FileSystemEventArgs(WatcherChangeTypes.Deleted, Path.GetDirectoryName(e.OldFullPath), Path.GetFileName(e.OldFullPath)));
            OnFileSystemEvent(sender, new FileSystemEventArgs(WatcherChangeTypes.Created, Path.GetDirectoryName(e.FullPath), Path.GetFileName(e.FullPath)));
        }

        private void DebounceCallback(object state)
        {
            var e = (FileSystemEventArgs)state;
            _debouncers.TryRemove(e.FullPath, out var timer);
            timer?.Dispose();

            // Enqueue analysis in the task queue
            _taskQueue.Enqueue($"Analyze {Path.GetFileName(e.FullPath)}", async task =>
            {
                await AnalyzeFileChangeAsync(e.FullPath, e.ChangeType.ToString(), task);
            }, Path.GetRelativePath(_workspacePath, e.FullPath));
        }

        private async Task AnalyzeFileChangeAsync(string filePath, string changeType, TaskItem task)
        {
            string relativePath = Path.GetRelativePath(_workspacePath, filePath);
            var eventArgs = new FileChangeEventArgs
            {
                FilePath = filePath,
                RelativePath = relativePath,
                ChangeType = changeType,
                Timestamp = DateTime.Now,
                Diff = new List<DiffLine>()
            };

            _taskQueue.UpdateProgress(task, 10, "Reading file contents...");

            if (changeType == "Deleted")
            {
                _fileContentCache.TryRemove(filePath, out var oldLines);
                if (oldLines != null)
                {
                    eventArgs.LinesDeleted = oldLines.Length;
                    eventArgs.Diff = oldLines.Select((line, index) => new DiffLine 
                    { 
                        Type = "Deleted", 
                        Text = line, 
                        OldLineNumber = index + 1, 
                        NewLineNumber = 0 
                    }).ToList();
                }
                _taskQueue.UpdateProgress(task, 100, "File deletion tracked.");
            }
            else
            {
                // Created or Modified
                try
                {
                    // Retry reading a few times in case the file is still locked by the editor
                    string[] newLines = null;
                    for (int i = 0; i < 5; i++)
                    {
                        try
                        {
                            newLines = File.ReadAllLines(filePath);
                            break;
                        }
                        catch (IOException)
                        {
                            await Task.Delay(100);
                        }
                    }

                    if (newLines == null)
                    {
                        throw new IOException($"Could not read file {filePath} - file locked by another process.");
                    }

                    _taskQueue.UpdateProgress(task, 40, "Computing diff...");

                    _fileContentCache.TryGetValue(filePath, out var oldLines);
                    if (oldLines == null)
                    {
                        oldLines = Array.Empty<string>();
                        eventArgs.ChangeType = "Created"; // Correct to created if not in cache
                    }

                    if (oldLines.Length > 800 || newLines.Length > 800)
                    {
                        // Skip detailed LCS diff for very large files to maintain high UI performance
                        eventArgs.LinesAdded = newLines.Length;
                        eventArgs.LinesDeleted = oldLines.Length;
                        eventArgs.Diff.Add(new DiffLine 
                        { 
                            Type = "Unchanged", 
                            Text = $"[File is too large to render inline diff ({newLines.Length} lines)]", 
                            OldLineNumber = 1, 
                            NewLineNumber = 1 
                        });
                    }
                    else
                    {
                        var diff = ComputeLcsDiff(oldLines, newLines);
                        eventArgs.Diff = diff;
                        eventArgs.LinesAdded = diff.Count(d => d.Type == "Added");
                        eventArgs.LinesDeleted = diff.Count(d => d.Type == "Deleted");
                    }

                    // Update Cache
                    _fileContentCache[filePath] = newLines;
                    _taskQueue.UpdateProgress(task, 100, "Diff analysis complete.");
                }
                catch (Exception ex)
                {
                    _taskQueue.UpdateProgress(task, 100, $"Error analyzing file: {ex.Message}");
                    return;
                }
            }

            // Raise the event
            FileChanged?.Invoke(this, eventArgs);
        }

        private List<DiffLine> ComputeLcsDiff(string[] oldLines, string[] newLines)
        {
            int m = oldLines.Length;
            int n = newLines.Length;
            int[,] lcs = new int[m + 1, n + 1];

            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    if (oldLines[i - 1] == newLines[j - 1])
                        lcs[i, j] = lcs[i - 1, j - 1] + 1;
                    else
                        lcs[i, j] = Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
                }
            }

            var diff = new List<DiffLine>();
            int x = m, y = n;

            while (x > 0 || y > 0)
            {
                if (x > 0 && y > 0 && oldLines[x - 1] == newLines[y - 1])
                {
                    diff.Insert(0, new DiffLine { Type = "Unchanged", Text = oldLines[x - 1], OldLineNumber = x, NewLineNumber = y });
                    x--;
                    y--;
                }
                else if (y > 0 && (x == 0 || lcs[x, y - 1] >= lcs[x - 1, y]))
                {
                    diff.Insert(0, new DiffLine { Type = "Added", Text = newLines[y - 1], OldLineNumber = 0, NewLineNumber = y });
                    y--;
                }
                else
                {
                    diff.Insert(0, new DiffLine { Type = "Deleted", Text = oldLines[x - 1], OldLineNumber = x, NewLineNumber = 0 });
                    x--;
                }
            }

            return diff;
        }
    }
}

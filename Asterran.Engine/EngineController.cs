using System;
using System.Collections.Generic;
using System.IO;
using Asterran.Connectors;

namespace Asterran.Engine
{
    public class EngineController
    {
        private ILlmConnector _connector;
        private CodebaseWatcher _watcher;
        private readonly TaskQueue _taskQueue;
        private readonly ArchitectureAnalyzer _architectureAnalyzer;
        private System.Threading.Timer _architectureTimer;
        private string _workspacePath;
        private string _conversationId;

        public event EventHandler<LlmActivityEventArgs> OnActivity;
        public event EventHandler<FileChangeEventArgs> OnFileChange;
        public event EventHandler<List<TaskItem>> OnTaskQueueUpdate;
        public event EventHandler<List<ProjectNode>> OnArchitectureUpdate;

        public TaskQueue TaskQueue => _taskQueue;
        public string WorkspacePath => _workspacePath;
        public string ConversationId => _conversationId;

        public EngineController(string workspacePath, string conversationId = null)
        {
            _workspacePath = Path.GetFullPath(workspacePath);
            _conversationId = conversationId;
            _taskQueue = new TaskQueue();
            _architectureAnalyzer = new ArchitectureAnalyzer(_workspacePath);

            // Wire up task queue event aggregation
            _taskQueue.TaskQueued += (s, t) => TriggerQueueUpdate();
            _taskQueue.TaskStarted += (s, t) => TriggerQueueUpdate();
            _taskQueue.TaskProgressUpdated += (s, t) => TriggerQueueUpdate();
            _taskQueue.TaskCompleted += (s, t) => TriggerQueueUpdate();

            _connector = new AntigravityTranscriptConnector(conversationId);

            _connector.OnActivity += (s, e) =>
            {
                // If the LLM proposes editing a file (e.g. tool call write_to_file), set project to Yellow (Changing)
                if (!string.IsNullOrEmpty(e.TargetFile))
                {
                    _architectureAnalyzer.SetProjectChanging(e.TargetFile);
                    TriggerArchitectureUpdate();
                }
                OnActivity?.Invoke(this, e);
            };

            _watcher = new CodebaseWatcher(_workspacePath, _taskQueue);
            _watcher.FileChanged += (s, e) =>
            {
                // Completed edit: run guardrail analysis to set project to Green (Stable) or Red (Failed Guardrails)
                _architectureAnalyzer.NotifyFileEdit(e.FilePath, e.LinesAdded, e.LinesDeleted, e.Diff);
                TriggerArchitectureUpdate();
                OnFileChange?.Invoke(this, e);
            };
        }

        public void Start()
        {
            _taskQueue.Start();
            _watcher.Start();
            _connector.Start();

            // Perform initial scan
            _architectureAnalyzer.ScanWorkspace();
            TriggerArchitectureUpdate();

            // Set up background timer to update architecture statuses and clear active timeouts
            _architectureTimer = new System.Threading.Timer(
                state => TriggerArchitectureUpdate(),
                null,
                1000,
                1000
            );
        }

        public void Stop()
        {
            _connector.Stop();
            _watcher.Stop();
            _taskQueue.Stop();
            _architectureTimer?.Dispose();
            _architectureTimer = null;
        }

        public void SwitchConnector(ILlmConnector newConnector)
        {
            _connector.Stop();
            _connector = newConnector;
            _connector.OnActivity += (s, e) => OnActivity?.Invoke(this, e);
            _connector.Start();
        }

        private void TriggerQueueUpdate()
        {
            var tasks = _taskQueue.GetPendingAndRunningTasks();
            OnTaskQueueUpdate?.Invoke(this, tasks);
        }

        private void TriggerArchitectureUpdate()
        {
            var state = _architectureAnalyzer.GetArchitectureState();
            OnArchitectureUpdate?.Invoke(this, state);
        }

        public List<GuardrailViolation> GetActiveViolations()
        {
            return _architectureAnalyzer.GetActiveViolations();
        }

        public void ClearViolation(string violationId)
        {
            _architectureAnalyzer.ClearViolation(violationId);
            TriggerArchitectureUpdate();
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Asterran.Engine
{
    public class TaskItem
    {
        public string TaskId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Running, Completed, Failed
        public int Progress { get; set; } = 0; // 0 to 100
        public string Details { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public Func<TaskItem, Task> Action { get; set; }
    }

    public class TaskQueue
    {
        private readonly ConcurrentQueue<TaskItem> _queue = new ConcurrentQueue<TaskItem>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private CancellationTokenSource _cts;
        private Task _workerTask;
        private TaskItem _activeTask;

        public event EventHandler<TaskItem> TaskQueued;
        public event EventHandler<TaskItem> TaskStarted;
        public event EventHandler<TaskItem> TaskProgressUpdated;
        public event EventHandler<TaskItem> TaskCompleted;

        public List<TaskItem> GetPendingAndRunningTasks()
        {
            var list = new List<TaskItem>();
            lock (_queue)
            {
                if (_activeTask != null)
                {
                    list.Add(_activeTask);
                }
                list.AddRange(_queue);
            }
            return list;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _workerTask = Task.Run(() => WorkerLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _signal.Release(); // Wake up worker if sleeping
        }

        public void Enqueue(string name, Func<TaskItem, Task> action, string details = "")
        {
            var task = new TaskItem
            {
                Name = name,
                Action = action,
                Details = details
            };

            _queue.Enqueue(task);
            TaskQueued?.Invoke(this, task);
            _signal.Release();
        }

        public void UpdateProgress(TaskItem task, int progress, string details = null)
        {
            task.Progress = progress;
            if (details != null)
            {
                task.Details = details;
            }
            TaskProgressUpdated?.Invoke(this, task);
        }

        private async Task WorkerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _signal.WaitAsync(token);

                    if (token.IsCancellationRequested) break;

                    if (_queue.TryDequeue(out var task))
                    {
                        lock (_queue)
                        {
                            _activeTask = task;
                        }

                        task.Status = "Running";
                        TaskStarted?.Invoke(this, task);

                        try
                        {
                            if (task.Action != null)
                            {
                                await task.Action(task);
                            }
                            task.Status = "Completed";
                            task.Progress = 100;
                        }
                        catch (Exception ex)
                        {
                            task.Status = "Failed";
                            task.Details = $"Error: {ex.Message}";
                        }
                        finally
                        {
                            lock (_queue)
                            {
                                _activeTask = null;
                            }
                            TaskCompleted?.Invoke(this, task);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in TaskQueue loop: {ex.Message}");
                }
            }
        }
    }
}

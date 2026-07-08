using Xunit;
using Asterran.Engine;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Asterran.Engine.Test
{
    public class TaskQueueTests
    {
        [Fact]
        public async Task TaskQueue_RunsEnqueuedTask()
        {
            var queue = new TaskQueue();
            queue.Start();

            bool taskExecuted = false;
            var tcs = new TaskCompletionSource<bool>();

            queue.Enqueue("Test Task", async task =>
            {
                taskExecuted = true;
                tcs.SetResult(true);
                await Task.CompletedTask;
            });

            await tcs.Task;
            queue.Stop();

            Assert.True(taskExecuted);
        }

        [Fact]
        public async Task TaskQueue_UpdatesProgress()
        {
            var queue = new TaskQueue();
            var updates = new List<(int Progress, string Details)>();
            queue.TaskProgressUpdated += (s, t) => updates.Add((t.Progress, t.Details));
            
            queue.Start();

            var tcs = new TaskCompletionSource<bool>();

            queue.Enqueue("Test Progress", async task =>
            {
                queue.UpdateProgress(task, 50, "Halfway");
                tcs.SetResult(true);
                await Task.CompletedTask;
            });

            await tcs.Task;
            queue.Stop();

            Assert.NotEmpty(updates);
            Assert.Contains(updates, u => u.Progress == 50 && u.Details == "Halfway");
        }
    }
}

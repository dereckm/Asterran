using System;

namespace Asterran.Connectors
{
    public class LlmActivityEventArgs : EventArgs
    {
        public string ActivityType { get; set; } // "Prompt", "Thought", "Action", "Status"
        public string Content { get; set; }
        public string Source { get; set; } // "User", "Model", "System"
        public DateTime Timestamp { get; set; }
        public int StepIndex { get; set; }
        public string TargetFile { get; set; }
    }

    public interface ILlmConnector
    {
        event EventHandler<LlmActivityEventArgs> OnActivity;
        void Start();
        void Stop();
    }
}

using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Asterran.Engine;
using Asterran.Connectors;
using Microsoft.Win32;

namespace Asterran.Viewer
{
    public partial class MainWindow : Window
    {
        private EngineController _engine;
        private string _workspacePath = "c:\\Asterran";
        private string _conversationId = "";
        private string _connectorType = "gemini";
        private bool _isEngineRunning = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize WebView2
                await WebView.EnsureCoreWebView2Async(null);

                // Set virtual host mapping to serve local wwwroot files
                string wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
                if (!Directory.Exists(wwwrootPath))
                {
                    Directory.CreateDirectory(wwwrootPath);
                }

                WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "asterran.local",
                    wwwrootPath,
                    CoreWebView2HostResourceAccessKind.Allow);

                // Wire up IPC event handlers
                WebView.WebMessageReceived += OnWebMessageReceived;

                // Navigate to local dashboard
                WebView.Source = new Uri("https://asterran.local/index.html");

                // Initialize Engine with default paths
                InitEngine();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing WebView2: {ex.Message}\nMake sure Edge WebView2 Runtime is installed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitEngine()
        {
            if (_engine != null)
            {
                _engine.Stop();
                _engine.OnActivity -= Engine_OnActivity;
                _engine.OnFileChange -= Engine_OnFileChange;
                _engine.OnTaskQueueUpdate -= Engine_OnTaskQueueUpdate;
                _engine.OnArchitectureUpdate -= Engine_OnArchitectureUpdate;
            }

            _engine = new EngineController(_workspacePath, _conversationId, _connectorType);
            _engine.OnActivity += Engine_OnActivity;
            _engine.OnFileChange += Engine_OnFileChange;
            _engine.OnTaskQueueUpdate += Engine_OnTaskQueueUpdate;
            _engine.OnArchitectureUpdate += Engine_OnArchitectureUpdate;

            if (_isEngineRunning)
            {
                _engine.Start();
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _engine?.Stop();
        }

        // Engine event forwarders to JavaScript IPC
        private void Engine_OnActivity(object sender, LlmActivityEventArgs e)
        {
            SendToWeb("activity", e);
        }

        private void Engine_OnFileChange(object sender, FileChangeEventArgs e)
        {
            SendToWeb("fileChange", e);
        }

        private void Engine_OnTaskQueueUpdate(object sender, System.Collections.Generic.List<TaskItem> e)
        {
            SendToWeb("taskQueue", e);
        }

        private void Engine_OnArchitectureUpdate(object sender, System.Collections.Generic.List<ProjectNode> e)
        {
            SendToWeb("architecture", new { projects = e, violations = _engine.GetActiveViolations() });
        }

        private void SendToWeb(string eventName, object data)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (WebView?.CoreWebView2 != null)
                    {
                        var payload = new { @event = eventName, data = data };
                        string json = JsonSerializer.Serialize(payload);
                        WebView.CoreWebView2.PostWebMessageAsJson(json);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending IPC message: {ex.Message}");
                }
            }));
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.WebMessageAsJson;
                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("command", out var cmdProp))
                    {
                        string command = cmdProp.GetString();
                        switch (command)
                        {
                            case "init":
                                // Web page finished loading and is ready to receive config
                                SendToWeb("config", new
                                {
                                    workspacePath = _workspacePath,
                                    conversationId = _conversationId,
                                    connectorType = _connectorType,
                                    isRunning = _isEngineRunning
                                });
                                break;

                            case "start":
                                _isEngineRunning = true;
                                _engine.Start();
                                SendToWeb("engineStatus", new { isRunning = true });
                                break;

                            case "stop":
                                _isEngineRunning = false;
                                _engine.Stop();
                                SendToWeb("engineStatus", new { isRunning = false });
                                break;

                            case "selectWorkspace":
                                SelectWorkspace();
                                break;

                            case "setConversationId":
                                if (root.TryGetProperty("conversationId", out var convoProp))
                                {
                                    _conversationId = convoProp.GetString();
                                    InitEngine();
                                }
                                break;

                            case "setConnectorType":
                                if (root.TryGetProperty("connectorType", out var ctProp))
                                {
                                    _connectorType = ctProp.GetString();
                                    _conversationId = "";
                                    InitEngine();
                                    SendToWeb("config", new
                                    {
                                        workspacePath = _workspacePath,
                                        conversationId = _conversationId,
                                        connectorType = _connectorType,
                                        isRunning = _isEngineRunning
                                    });
                                }
                                break;

                            case "clearViolation":
                                if (root.TryGetProperty("violationId", out var violProp))
                                {
                                    string violationId = violProp.GetString();
                                    _engine.ClearViolation(violationId);
                                }
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error handling IPC message: {ex.Message}");
            }
        }

        private void SelectWorkspace()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Workspace Folder to Monitor",
                InitialDirectory = _workspacePath
            };

            if (dialog.ShowDialog() == true)
            {
                _workspacePath = dialog.FolderName;
                InitEngine();
                SendToWeb("config", new
                {
                    workspacePath = _workspacePath,
                    conversationId = _conversationId,
                    connectorType = _connectorType,
                    isRunning = _isEngineRunning
                });
            }
        }
    }
}
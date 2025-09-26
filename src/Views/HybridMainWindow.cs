using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using SuperWhisperWPF.Core;
using SuperWhisperWPF.Services;

namespace SuperWhisperWPF.Views
{
    /// <summary>
    /// Hybrid window that uses WebView2 for modern HTML/CSS/JS UI
    /// while keeping native C# backend for performance.
    /// This gives us the best of both worlds - beautiful UI + native speed.
    /// </summary>
    public class HybridMainWindow : Window
    {
        private WebView2 webView;
        private WhisperEngine whisperEngine;
        private AudioCapture audioCapture;
        private RecordingModeManager modeManager;
        private StreamingAudioProcessor streamingProcessor;
        private GlobalHotkey globalHotkey;

        public HybridMainWindow()
        {
            InitializeWindow();
            InitializeWebView();
            InitializeBackend();
        }

        private void InitializeWindow()
        {
            Title = "Lumina - Modern UI";
            Width = 800;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None; // Frameless window
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
        }

        private void InitializeWebView()
        {
            webView = new WebView2
            {
                Name = "webView"
            };

            Content = webView;

            // Initialize WebView2
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            // Set up WebView2 environment with custom settings
            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Lumina", "WebView2"
                )
            );

            await webView.EnsureCoreWebView2Async(env);

            // Configure WebView2
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            // Load the HTML UI
            var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebUI", "index.html");
            if (File.Exists(htmlPath))
            {
                webView.CoreWebView2.Navigate($"file:///{htmlPath.Replace('\\', '/')}");
            }
            else
            {
                // Load from embedded resource or CDN as fallback
                webView.NavigateToString(GetEmbeddedHtml());
            }

            // Set up JavaScript bridge
            await SetupJavaScriptBridge();
        }

        private async Task SetupJavaScriptBridge()
        {
            // Create the bridge object that JavaScript can call
            var bridge = new LuminaBridge(this);

            // Add the bridge to JavaScript
            webView.CoreWebView2.AddHostObjectToScript("lumina", bridge);

            // Listen for messages from JavaScript
            webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // Inject initialization script
            await webView.CoreWebView2.ExecuteScriptAsync(@"
                console.log('Lumina bridge initialized');

                // Make bridge methods easier to call
                window.lumina = chrome.webview.hostObjects.lumina;

                // Add keyboard shortcuts
                document.addEventListener('keydown', (e) => {
                    if (e.ctrlKey && e.code === 'Space') {
                        e.preventDefault();
                        window.lumina.toggleRecording();
                    }
                });

                // Notify that bridge is ready
                window.dispatchEvent(new Event('luminaReady'));
            ");
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = e.TryGetWebMessageAsString();
            var data = JsonConvert.DeserializeObject<dynamic>(message);

            // Handle messages from JavaScript
            switch (data?.type?.ToString())
            {
                case "toggleTheme":
                    ThemeService.Instance.ToggleTheme();
                    break;
                case "changeMode":
                    modeManager.SetActiveMode(data.mode.ToString());
                    break;
                case "export":
                    ExportTranscription(data.format.ToString());
                    break;
            }
        }

        private void InitializeBackend()
        {
            // Initialize all our native components
            whisperEngine = new WhisperEngine();
            audioCapture = new AudioCapture();
            modeManager = new RecordingModeManager();
            streamingProcessor = new StreamingAudioProcessor();

            // Set up event handlers
            audioCapture.SpeechEnded += OnSpeechEnded;
            audioCapture.AudioLevelChanged += OnAudioLevelChanged;

            streamingProcessor.PartialTranscription += OnPartialTranscription;
            streamingProcessor.FinalTranscription += OnFinalTranscription;

            // Initialize Whisper
            _ = Task.Run(async () => await whisperEngine.InitializeAsync());

            // Set up global hotkey (Alt+Shift+R to avoid conflicts)
            globalHotkey = HotkeyExtensions.CreateAltShiftR(this, ToggleRecording);
        }

        public void ToggleRecording()
        {
            // Call JavaScript to update UI
            _ = webView.CoreWebView2.ExecuteScriptAsync("window.handleRecord()");
        }

        private async void OnSpeechEnded(object sender, byte[] audioData)
        {
            var transcription = await whisperEngine.TranscribeAsync(audioData);

            // Send result to JavaScript
            await webView.CoreWebView2.ExecuteScriptAsync($@"
                window.setTranscription('{JsonConvert.ToString(transcription)}');
            ");
        }

        private void OnAudioLevelChanged(object sender, float level)
        {
            // Update audio visualizer in UI
            _ = webView.CoreWebView2.ExecuteScriptAsync($@"
                window.setAudioLevel({level});
            ");
        }

        private void OnPartialTranscription(object sender, string text)
        {
            // Show partial text while speaking
            _ = webView.CoreWebView2.ExecuteScriptAsync($@"
                window.setPartialText('{JsonConvert.ToString(text)}');
            ");
        }

        private void OnFinalTranscription(object sender, string text)
        {
            // Show final transcription
            _ = webView.CoreWebView2.ExecuteScriptAsync($@"
                window.setTranscription('{JsonConvert.ToString(text)}');
            ");
        }

        private void ExportTranscription(string format)
        {
            // Use our existing export service
            // Implementation here...
        }

        private string GetEmbeddedHtml()
        {
            // Return the HTML as a string if file not found
            return @"<!DOCTYPE html>
<html>
<head>
    <title>Lumina</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
            margin: 0;
            padding: 20px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
        }
        .container {
            text-align: center;
        }
        h1 {
            font-size: 48px;
            margin-bottom: 20px;
        }
        button {
            background: white;
            color: #667eea;
            border: none;
            padding: 12px 24px;
            font-size: 18px;
            border-radius: 8px;
            cursor: pointer;
            transition: transform 0.2s;
        }
        button:hover {
            transform: scale(1.05);
        }
    </style>
</head>
<body>
    <div class='container'>
        <h1>🎙️ Lumina</h1>
        <p>Modern transcription with native performance</p>
        <button onclick='window.lumina.startRecording()'>Start Recording</button>
    </div>
</body>
</html>";
        }
    }

    /// <summary>
    /// Bridge class that JavaScript can call
    /// </summary>
    [System.Runtime.InteropServices.ComVisible(true)]
    public class LuminaBridge
    {
        private readonly HybridMainWindow window;

        public LuminaBridge(HybridMainWindow window)
        {
            this.window = window;
        }

        public void StartRecording()
        {
            window.Dispatcher.Invoke(() =>
            {
                // Start recording logic
                Logger.Info("Recording started from JavaScript");
            });
        }

        public void StopRecording()
        {
            window.Dispatcher.Invoke(() =>
            {
                // Stop recording logic
                Logger.Info("Recording stopped from JavaScript");
            });
        }

        public void ToggleRecording()
        {
            window.Dispatcher.Invoke(() =>
            {
                window.ToggleRecording();
            });
        }

        public void SetMode(string mode)
        {
            Logger.Info($"Mode changed to: {mode}");
        }

        public string GetStatus()
        {
            return JsonConvert.SerializeObject(new
            {
                isRecording = false,
                mode = "quick",
                model = "base"
            });
        }
    }
}
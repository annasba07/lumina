using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using SuperWhisperWPF.Core;
using SuperWhisperWPF.Services;

namespace SuperWhisperWPF.Views
{
    /// <summary>
    /// Hybrid window that uses WebView2 for modern HTML/CSS/JS UI
    /// while keeping native C# backend for performance and native window features.
    /// This gives us the best of both worlds - beautiful UI + native functionality.
    /// </summary>
    public class HybridMainWindow : Window
    {
        private WebView2 webView;
        private WhisperEngine whisperEngine;
        private AudioCapture audioCapture;
        private RecordingModeManager modeManager;
        private StreamingAudioProcessor streamingProcessor;
        private GlobalHotkey globalHotkey;
        private NotifyIcon trayIcon;
        internal bool isRecording = false;

        public HybridMainWindow()
        {
            InitializeWindow();
            InitializeContent();
            InitializeBackend();
            InitializeSystemTray();
        }

        private void InitializeWindow()
        {
            Title = "Lumina";
            Width = 440;
            Height = 600;
            MinWidth = 380;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.CanResize;
        }

        private void InitializeContent()
        {
            // Create main border with rounded corners
            var border = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(249, 250, 251)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(1)
            };

            // Add drop shadow
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 30,
                ShadowDepth = 0,
                Opacity = 0.12,
                Color = Colors.Black
            };

            // Create grid for title bar and content
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Create custom title bar
            var titleBar = CreateTitleBar();
            Grid.SetRow(titleBar, 0);
            grid.Children.Add(titleBar);

            // Create WebView2 for main content
            webView = new WebView2
            {
                Name = "webView",
                Margin = new Thickness(0)
            };
            Grid.SetRow(webView, 1);
            grid.Children.Add(webView);

            border.Child = grid;
            Content = border;

            // Initialize WebView2
            InitializeAsync();
        }

        private Grid CreateTitleBar()
        {
            var titleBar = new Grid { Background = Brushes.Transparent };
            titleBar.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;

            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // App icon and title
            var titlePanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            titlePanel.Children.Add(new TextBlock
            {
                Text = "✨",
                FontSize = 16,
                Margin = new Thickness(0, 0, 8, 0)
            });

            titlePanel.Children.Add(new TextBlock
            {
                Text = "Lumina",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(17, 24, 39)),
                VerticalAlignment = VerticalAlignment.Center
            });

            Grid.SetColumn(titlePanel, 0);
            titleBar.Children.Add(titlePanel);

            // Window controls
            var controlsPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 4, 0)
            };

            // Minimize button
            var minimizeBtn = CreateWindowButton("─");
            minimizeBtn.Click += (s, e) => WindowState = WindowState.Minimized;
            controlsPanel.Children.Add(minimizeBtn);

            // Close button
            var closeBtn = CreateWindowButton("✕");
            closeBtn.Click += (s, e) => Close();
            controlsPanel.Children.Add(closeBtn);

            Grid.SetColumn(controlsPanel, 2);
            titleBar.Children.Add(controlsPanel);

            return titleBar;
        }

        private System.Windows.Controls.Button CreateWindowButton(string content)
        {
            var button = new System.Windows.Controls.Button
            {
                Content = content,
                Width = 46,
                Height = 32,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                FontSize = 14,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // Add hover effect
            button.MouseEnter += (s, e) => button.Background = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0));
            button.MouseLeave += (s, e) => button.Background = Brushes.Transparent;

            return button;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
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

                // Add keyboard shortcuts - now using Alt+Shift+R
                document.addEventListener('keydown', (e) => {
                    if (e.altKey && e.shiftKey && e.code === 'KeyR') {
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
                case "toggleRecording":
                    ToggleRecording();
                    break;
            }
        }

        private void InitializeBackend()
        {
            try
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

                Logger.Info("Backend initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize backend: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Failed to initialize audio capture: {ex.Message}\n\nPlease check your microphone settings.",
                    "Lumina - Initialization Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void InitializeSystemTray()
        {
            // Create system tray icon
            trayIcon = new NotifyIcon();
            trayIcon.Icon = CreateTrayIcon();
            trayIcon.Text = "Lumina - Press Alt+Shift+R to record";
            trayIcon.Visible = true;

            // Double-click to show/hide window
            trayIcon.DoubleClick += (s, e) => {
                if (WindowState == WindowState.Minimized)
                {
                    Show();
                    WindowState = WindowState.Normal;
                }
                else
                {
                    Hide();
                }
            };

            // Create context menu
            var contextMenu = new ContextMenuStrip();

            contextMenu.Items.Add("Show/Hide", null, (s, e) => {
                if (WindowState == WindowState.Minimized || !IsVisible)
                {
                    Show();
                    WindowState = WindowState.Normal;
                }
                else
                {
                    Hide();
                }
            });

            contextMenu.Items.Add("-");

            contextMenu.Items.Add("Exit", null, (s, e) => {
                System.Windows.Application.Current.Shutdown();
            });

            trayIcon.ContextMenuStrip = contextMenu;
        }

        private System.Drawing.Icon CreateTrayIcon()
        {
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lumina-icon.ico");
                if (File.Exists(iconPath))
                {
                    return new System.Drawing.Icon(iconPath);
                }
            }
            catch { }

            // Fallback to system icon
            return System.Drawing.SystemIcons.Application;
        }

        public void ToggleRecording()
        {
            try
            {
                isRecording = !isRecording;
                Logger.Info($"ToggleRecording: isRecording={isRecording}");

                if (isRecording)
                {
                    audioCapture.StartRecording();
                    Logger.Info("Recording started via ToggleRecording");
                }
                else
                {
                    audioCapture.StopRecording();
                    Logger.Info("Recording stopped via ToggleRecording");
                }

                // Update UI through JavaScript - don't call handleRecord again to avoid recursion
                _ = Dispatcher.InvokeAsync(async () =>
                {
                    if (webView?.CoreWebView2 != null)
                    {
                        var script = isRecording ?
                            "if(window.setRecordingState) window.setRecordingState(true);" :
                            "if(window.setRecordingState) window.setRecordingState(false);";
                        await webView.CoreWebView2.ExecuteScriptAsync(script);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in ToggleRecording: {ex.Message}");
            }
        }

        private async void OnSpeechEnded(object sender, byte[] audioData)
        {
            try
            {
                Logger.Info($"OnSpeechEnded: Processing {audioData.Length} bytes of audio");
                var transcription = await whisperEngine.TranscribeAsync(audioData);
                Logger.Info($"Transcription result: '{transcription}'");

                // Send result to JavaScript - escape the text properly
                var escapedText = transcription?.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n") ?? "";
                var script = $"window.setTranscription && window.setTranscription('{escapedText}');";

                await Dispatcher.InvokeAsync(async () =>
                {
                    if (webView?.CoreWebView2 != null)
                    {
                        await webView.CoreWebView2.ExecuteScriptAsync(script);
                        Logger.Info("Transcription sent to UI");
                    }
                    else
                    {
                        Logger.Warning("WebView2 not ready, couldn't send transcription");
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in OnSpeechEnded: {ex.Message}");
            }
        }

        private void OnAudioLevelChanged(object sender, float level)
        {
            // Update audio visualizer in UI - must be on UI thread
            _ = Dispatcher.InvokeAsync(async () =>
            {
                if (webView?.CoreWebView2 != null)
                {
                    await webView.CoreWebView2.ExecuteScriptAsync($@"
                        window.setAudioLevel && window.setAudioLevel({level});
                    ");
                }
            });
        }

        private void OnPartialTranscription(object sender, string text)
        {
            // Show partial text while speaking - must be on UI thread
            _ = Dispatcher.InvokeAsync(async () =>
            {
                if (webView?.CoreWebView2 != null)
                {
                    var escapedText = text?.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n") ?? "";
                    await webView.CoreWebView2.ExecuteScriptAsync($@"
                        window.setPartialText && window.setPartialText('{escapedText}');
                    ");
                }
            });
        }

        private void OnFinalTranscription(object sender, string text)
        {
            // Show final transcription - must be on UI thread
            _ = Dispatcher.InvokeAsync(async () =>
            {
                if (webView?.CoreWebView2 != null)
                {
                    var escapedText = text?.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n") ?? "";
                    await webView.CoreWebView2.ExecuteScriptAsync($@"
                        window.setTranscription && window.setTranscription('{escapedText}');
                    ");
                }
            });
        }

        private void ExportTranscription(string format)
        {
            // Use our existing export service
            // Implementation here...
        }

        protected override void OnClosed(EventArgs e)
        {
            // Cleanup
            trayIcon?.Dispose();
            globalHotkey?.Dispose();
            audioCapture?.Dispose();
            whisperEngine?.Dispose();
            base.OnClosed(e);
        }

        private string GetEmbeddedHtml()
        {
            // Return HTML that exactly replicates the native WPF UI
            return @"<!DOCTYPE html>
<html>
<head>
    <title>Lumina</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: -apple-system, 'Segoe UI', sans-serif;
            background: #F9FAFB;
            color: #111827;
            height: 100vh;
            overflow: hidden;
            padding: 20px;
        }

        /* Record Button Container */
        .record-section {
            display: flex;
            justify-content: center;
            margin-bottom: 20px;
        }

        .record-button {
            width: 120px;
            height: 120px;
            border-radius: 50%;
            background: linear-gradient(135deg, #667EEA 0%, #764BA2 100%);
            border: none;
            cursor: pointer;
            position: relative;
            transition: all 0.3s ease;
            box-shadow: 0 10px 30px rgba(102, 126, 234, 0.3);
            display: flex;
            align-items: center;
            justify-content: center;
        }

        .record-button:hover {
            transform: scale(1.05);
            box-shadow: 0 15px 40px rgba(102, 126, 234, 0.4);
        }

        .record-button.recording {
            animation: pulse 1.5s infinite;
            background: linear-gradient(135deg, #EF4444 0%, #DC2626 100%);
        }

        @keyframes pulse {
            0%, 100% { transform: scale(1); }
            50% { transform: scale(1.05); }
        }

        .record-icon {
            font-size: 40px;
        }

        /* Status Text */
        .status-text {
            text-align: center;
            margin: 10px 0;
            font-size: 14px;
            color: #6B7280;
        }

        .shortcut-hint {
            background: #E5E7EB;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 12px;
            font-weight: 600;
            margin-left: 5px;
        }

        /* Mode Selection */
        .modes-panel {
            display: grid;
            grid-template-columns: repeat(3, 1fr);
            gap: 10px;
            margin: 20px 0;
            padding: 15px;
            background: white;
            border-radius: 12px;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
        }

        .mode-button {
            padding: 12px;
            background: white;
            border: 2px solid #E5E7EB;
            border-radius: 8px;
            cursor: pointer;
            transition: all 0.2s;
            text-align: center;
            font-size: 14px;
        }

        .mode-button:hover {
            border-color: #667EEA;
            background: #F3F4F6;
        }

        .mode-button.active {
            border-color: #667EEA;
            background: linear-gradient(135deg, #667EEA 0%, #764BA2 100%);
            color: white;
        }

        /* Transcription Area */
        .transcription-container {
            flex: 1;
            background: white;
            border-radius: 12px;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
            padding: 20px;
            margin-top: 20px;
            max-height: calc(100vh - 400px);
            overflow-y: auto;
        }

        .transcription-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 15px;
            padding-bottom: 10px;
            border-bottom: 1px solid #E5E7EB;
        }

        .transcription-title {
            font-size: 16px;
            font-weight: 600;
            color: #374151;
        }

        .export-buttons {
            display: flex;
            gap: 8px;
        }

        .export-btn {
            padding: 6px 12px;
            background: #F3F4F6;
            border: 1px solid #E5E7EB;
            border-radius: 6px;
            cursor: pointer;
            font-size: 12px;
            transition: all 0.2s;
        }

        .export-btn:hover {
            background: #E5E7EB;
        }

        .transcription-text {
            min-height: 100px;
            padding: 15px;
            background: #F9FAFB;
            border-radius: 8px;
            font-size: 14px;
            line-height: 1.6;
            color: #374151;
        }

        .transcription-text:empty::before {
            content: 'Your transcription will appear here...';
            color: #9CA3AF;
            font-style: italic;
        }

        /* Audio Visualizer */
        .audio-level {
            height: 3px;
            background: #E5E7EB;
            border-radius: 3px;
            margin: 10px 0;
            overflow: hidden;
        }

        .audio-level-bar {
            height: 100%;
            background: linear-gradient(90deg, #667EEA 0%, #764BA2 100%);
            width: 0%;
            transition: width 0.1s ease;
        }

        /* Info Cards */
        .info-cards {
            display: grid;
            grid-template-columns: repeat(3, 1fr);
            gap: 10px;
            margin-top: 20px;
        }

        .info-card {
            background: white;
            padding: 12px;
            border-radius: 8px;
            text-align: center;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
        }

        .info-value {
            font-size: 20px;
            font-weight: 700;
            color: #667EEA;
        }

        .info-label {
            font-size: 11px;
            color: #6B7280;
            text-transform: uppercase;
            margin-top: 4px;
        }
    </style>
</head>
<body>
    <!-- Record Button -->
    <div class='record-section'>
        <button id='recordBtn' class='record-button' onclick='handleRecord()'>
            <span class='record-icon'>🎙️</span>
        </button>
    </div>

    <!-- Status -->
    <div class='status-text'>
        <span id='statusText'>Ready to record</span>
        <span class='shortcut-hint'>Alt+Shift+R</span>
    </div>

    <!-- Audio Level Indicator -->
    <div class='audio-level'>
        <div id='audioLevel' class='audio-level-bar'></div>
    </div>


    <!-- Transcription Area -->
    <div class='transcription-container'>
        <div class='transcription-header'>
            <div class='transcription-title'>Transcription</div>
            <div class='export-buttons'>
                <button class='export-btn' onclick='exportText(""copy"")'>📋 Copy</button>
                <button class='export-btn' onclick='exportText(""save"")'>💾 Save</button>
            </div>
        </div>
        <div id='transcriptionText' class='transcription-text'></div>
    </div>

    <!-- Info Cards -->
    <div class='info-cards'>
        <div class='info-card'>
            <div id='wordCount' class='info-value'>0</div>
            <div class='info-label'>Words</div>
        </div>
        <div class='info-card'>
            <div id='duration' class='info-value'>0:00</div>
            <div class='info-label'>Duration</div>
        </div>
        <div class='info-card'>
            <div id='latency' class='info-value'>-</div>
            <div class='info-label'>Processing</div>
        </div>
    </div>

    <script>
        let isRecording = false;
        let recordingStartTime = null;
        let recordingTimer = null;
        let processingStartTime = null;

        function handleRecord() {
            console.log('handleRecord called, current state:', isRecording);
            // Just toggle the C# side, it will call setRecordingState back
            if (window.lumina) {
                window.lumina.toggleRecording();
            }
        }

        // New function to update UI state from C#
        window.setRecordingState = (recording) => {
            console.log('setRecordingState called:', recording);
            isRecording = recording;
            const btn = document.getElementById('recordBtn');
            const status = document.getElementById('statusText');

            if (isRecording) {
                btn.classList.add('recording');
                status.textContent = 'Recording...';
                recordingStartTime = Date.now();
                startTimer();
            } else {
                btn.classList.remove('recording');
                status.textContent = 'Processing...';
                stopTimer();
                processingStartTime = Date.now();
                setTimeout(() => {
                    status.textContent = 'Ready to record';
                }, 2000);
            }
        };

        function startTimer() {
            recordingTimer = setInterval(() => {
                const elapsed = Math.floor((Date.now() - recordingStartTime) / 1000);
                const minutes = Math.floor(elapsed / 60);
                const seconds = elapsed % 60;
                document.getElementById('duration').textContent =
                    `${minutes}:${seconds.toString().padStart(2, '0')}`;
            }, 100);
        }

        function stopTimer() {
            if (recordingTimer) {
                clearInterval(recordingTimer);
                recordingTimer = null;
            }
        }

        // Removed setMode - not functional

        function exportText(format) {
            const text = document.getElementById('transcriptionText').textContent;
            if (format === 'copy') {
                navigator.clipboard.writeText(text);
                alert('Copied to clipboard!');
            } else {
                // Save functionality
                const blob = new Blob([text], {type: 'text/plain'});
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = 'transcription.txt';
                a.click();
            }
        }

        // Bridge functions
        window.setTranscription = (text) => {
            console.log('setTranscription called with:', text);
            const transcriptionEl = document.getElementById('transcriptionText');
            const existingText = transcriptionEl.textContent;

            // Append new transcription with line break if there's existing text
            if (existingText && existingText.trim()) {
                transcriptionEl.textContent = existingText + '\n\n' + text;
            } else {
                transcriptionEl.textContent = text;
            }

            // Update word count
            const allText = transcriptionEl.textContent;
            const words = allText.trim() ? allText.trim().split(/\s+/).length : 0;
            document.getElementById('wordCount').textContent = words;

            // Calculate actual processing time
            if (processingStartTime) {
                const processingTime = Date.now() - processingStartTime;
                const latencyEl = document.getElementById('latency');
                if (latencyEl) {
                    latencyEl.textContent = processingTime + 'ms';
                }
                processingStartTime = null;
            }
        };

        window.setPartialText = (text) => {
            const transcriptionEl = document.getElementById('transcriptionText');
            transcriptionEl.innerHTML = `<span style='opacity: 0.7'>${text}</span>`;
        };

        window.setAudioLevel = (level) => {
            document.getElementById('audioLevel').style.width = `${level * 100}%`;
        };

        // Handle hotkey
        document.addEventListener('keydown', (e) => {
            if (e.altKey && e.shiftKey && e.code === 'KeyR') {
                e.preventDefault();
                handleRecord();
            }
        });
    </script>
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
                if (!window.isRecording)
                {
                    window.ToggleRecording();
                }
            });
        }

        public void StopRecording()
        {
            window.Dispatcher.Invoke(() =>
            {
                if (window.isRecording)
                {
                    window.ToggleRecording();
                }
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
                isRecording = window.isRecording,
                mode = "quick",
                model = "base"
            });
        }
    }
}
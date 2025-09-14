using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq;

namespace SuperWhisperWindows
{
    internal class Program
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
        
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool FreeConsole();
        
        [STAThread]
        static void Main(string[] args)
        {
            // Enable console for debugging (only if not already attached)
            if (args.Contains("--debug") || System.Diagnostics.Debugger.IsAttached)
            {
                AllocConsole();
                Console.WriteLine("Debug console enabled. Logs will appear here and in the log file.");
            }
            
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            try
            {
                var app = new SuperWhisperApp();
                Application.Run();
            }
            catch (Exception ex)
            {
                Logger.Error($"Application startup failed: {ex.Message}", ex);
                MessageBox.Show($"SuperWhisper failed to start:\n\n{ex.Message}\n\nCheck the logs for more details.", 
                              "SuperWhisper Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (args.Contains("--debug") || System.Diagnostics.Debugger.IsAttached)
                {
                    FreeConsole();
                }
            }
        }
    }

    public class SuperWhisperApp
    {
        private NotifyIcon trayIcon;
        private AudioCapture audioCapture;
        private WhisperEngine whisperEngine;
        private GlobalHotkey globalHotkey;
        private RecordingOverlay overlay;
        private ResultsWindow resultsWindow;
        
        private bool isRecording = false;
        private bool isProcessing = false;

        public SuperWhisperApp()
        {
            // Initialize logging first
            Logger.Info("SuperWhisper Windows starting...");
            Logger.LogSystemInfo();
            
            InitializeComponents();
            SetupTrayIcon();
            
            Logger.Info("Starting background WhisperEngine initialization...");
            // Start whisper engine (keep model loaded)
            Task.Run(async () => 
            {
                var success = await whisperEngine.InitializeAsync();
                if (success)
                {
                    Logger.Info("WhisperEngine background initialization completed successfully");
                }
                else
                {
                    Logger.Error("WhisperEngine background initialization failed!");
                }
            });
        }

        private void InitializeComponents()
        {
            Logger.Info("Initializing components...");
            
            audioCapture = new AudioCapture();
            whisperEngine = new WhisperEngine();
            globalHotkey = new GlobalHotkey(OnHotkeyPressed);
            overlay = new RecordingOverlay();
            resultsWindow = new ResultsWindow();
            
            // Wire up events
            audioCapture.SpeechEnded += OnSpeechEnded;
            audioCapture.AudioLevelChanged += OnAudioLevelChanged;
            
            Logger.Info("All components initialized successfully");
        }

        private void SetupTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Icon = CreateMicrophoneIcon(),
                Text = "SuperWhisper - Ready (Ctrl+Space to toggle recording)",
                Visible = true,
                ContextMenuStrip = CreateContextMenu()
            };
            
            trayIcon.DoubleClick += (s, e) => ShowSettings();
        }
        
        private Icon CreateMicrophoneIcon()
        {
            var size = 16;
            var bitmap = new Bitmap(size, size);
            
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                // Draw microphone body (rounded rectangle)
                using (var brush = new SolidBrush(Color.FromArgb(100, 180, 255)))
                {
                    g.FillEllipse(brush, 4, 2, 8, 6); // Top rounded part
                    g.FillRectangle(brush, 4, 5, 8, 7); // Bottom rectangular part
                }
                
                // Draw microphone stand
                using (var pen = new Pen(Color.FromArgb(80, 80, 80), 2))
                {
                    g.DrawLine(pen, 8, 12, 8, 14);
                    g.DrawLine(pen, 5, 14, 11, 14);
                }
                
                // Draw sound waves
                using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 1))
                {
                    g.DrawArc(pen, 1, 4, 4, 4, 270, 180);
                    g.DrawArc(pen, 11, 4, 4, 4, 90, 180);
                }
            }
            
            return Icon.FromHandle(bitmap.GetHicon());
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Show Results", null, (s, e) => ShowResults());
            menu.Items.Add("-"); // Separator
            menu.Items.Add("Check Dependencies", null, (s, e) => CheckDependencies());
            menu.Items.Add("Show Logs", null, (s, e) => ShowLogs());
            menu.Items.Add("Settings", null, (s, e) => ShowSettings());
            menu.Items.Add("Exit", null, (s, e) => ExitApplication());
            return menu;
        }
        
        private void CheckDependencies()
        {
            Logger.Info("Manual dependency check requested...");
            DependencyChecker.CheckDependencies();
            DependencyChecker.ProvideFixSuggestions();
        }
        
        private void ShowLogs()
        {
            Logger.ShowLogLocation();
        }
        
        private void ShowResults()
        {
            resultsWindow.Show();
            resultsWindow.BringToFront();
        }

        private void OnHotkeyPressed()
        {
            Logger.Debug("Hotkey pressed (Ctrl+Space)");
            
            if (isProcessing) 
            {
                Logger.Debug("Ignoring hotkey - currently processing");
                return;
            }
            
            if (!isRecording)
            {
                Logger.Info("Starting recording...");
                StartRecording();
            }
            else
            {
                Logger.Info("Stopping recording...");
                StopRecording();
            }
        }
        
        private void StopRecording()
        {
            if (!isRecording) 
            {
                Logger.Debug("StopRecording called but not currently recording");
                return;
            }
            
            Logger.Info("Stopping audio capture...");
            audioCapture.StopRecording();
            // Note: OnSpeechEnded will be called automatically by AudioCapture
        }

        private void StartRecording()
        {
            try
            {
                isRecording = true;
                trayIcon.Text = "SuperWhisper - Recording... (Ctrl+Space to stop)";
                
                Logger.Info("Showing recording overlay and starting audio capture");
                overlay.Show("🎤 Recording - Press Ctrl+Space to stop");
                audioCapture.StartRecording();
                
                Logger.Info("Recording started successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error starting recording: {ex.Message}", ex);
                isRecording = false;
                overlay.ShowTemporary("Error starting recording", 3000);
            }
        }

        private async void OnSpeechEnded(object sender, byte[] audioData)
        {
            if (!isRecording) 
            {
                Logger.Debug("OnSpeechEnded called but not currently recording - ignoring");
                return;
            }
            
            Logger.Info($"Speech ended - received {audioData.Length} bytes of audio data");
            
            isRecording = false;
            isProcessing = true;
            
            trayIcon.Text = "SuperWhisper - Transcribing...";
            overlay.Show("✨ Transcribing speech...");
            
            try
            {
                var startTime = DateTime.UtcNow;
                Logger.Info("Starting transcription...");
                var transcription = await whisperEngine.TranscribeAsync(audioData);
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                Logger.Info($"Transcription completed in {processingTime:F1}ms");
                
                if (!string.IsNullOrWhiteSpace(transcription))
                {
                    Logger.Info($"Transcription successful: '{transcription}'");
                    resultsWindow.AddTranscription(transcription, (int)processingTime);
                    trayIcon.Text = $"SuperWhisper - Ready ({processingTime:F0}ms)";
                }
                else
                {
                    Logger.Warning("Transcription returned empty or whitespace");
                    trayIcon.Text = "SuperWhisper - No speech detected";
                    overlay.ShowTemporary("No speech detected", 2000);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Transcription failed: {ex.Message}", ex);
                trayIcon.Text = $"SuperWhisper - Error: {ex.Message}";
                overlay.ShowTemporary($"Error: {ex.Message}", 4000);
            }
            finally
            {
                isProcessing = false;
                overlay.Hide();
                Logger.Info("Speech processing completed");
            }
        }

        private void OnAudioLevelChanged(object sender, float level)
        {
            if (isRecording)
            {
                overlay.UpdateAudioLevel(level);
            }
        }

        private void ShowSettings()
        {
            MessageBox.Show("Settings panel coming soon!", "SuperWhisper", 
                          MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExitApplication()
        {
            trayIcon.Visible = false;
            globalHotkey?.Dispose();
            audioCapture?.Dispose();
            whisperEngine?.Dispose();
            resultsWindow?.Close();
            overlay?.Close();
            Application.Exit();
        }
    }
}
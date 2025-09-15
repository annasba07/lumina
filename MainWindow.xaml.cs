using ModernWpf.Controls;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Forms;

namespace SuperWhisperWPF
{
    public partial class MainWindow : Window
    {
        private WhisperEngine whisperEngine;
        private AudioCapture audioCapture;
        private GlobalHotkey globalHotkey;
        private RecordingOverlay recordingOverlay;
        private NotifyIcon trayIcon;
        private bool isRecording = false;
        private bool isInitialized = false;
        private DispatcherTimer typingTimer;
        private bool isExiting = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                Logger.Info("Initializing SuperWhisper WPF...");
                
                // Initialize Whisper engine
                whisperEngine = new WhisperEngine();
                var whisperInitialized = await whisperEngine.InitializeAsync();
                
                if (!whisperInitialized)
                {
                    UpdateStatus("❌ Whisper engine failed to initialize", Colors.Red);
                    return;
                }
                
                // Initialize audio capture
                audioCapture = new AudioCapture();
                audioCapture.SpeechEnded += OnSpeechEnded;
                audioCapture.AudioLevelChanged += OnAudioLevelChanged;
                
                // Initialize global hotkey (Ctrl+Space) using proven Windows Forms approach
                globalHotkey = HotkeyExtensions.CreateCtrlSpace(this, () => OnHotkeyPressed(null, null));
                
                // Initialize recording overlay
                recordingOverlay = new RecordingOverlay();
                
                // Initialize system tray
                InitializeSystemTray();
                
                // Initialize typing timer for word count
                typingTimer = new DispatcherTimer();
                typingTimer.Interval = TimeSpan.FromMilliseconds(500);
                typingTimer.Tick += (s, e) => UpdateWordCount();
                ResultsTextBox.TextChanged += (s, e) => {
                    typingTimer.Stop();
                    typingTimer.Start();
                };
                
                UpdateStatus("✅ Ready - Press Ctrl+Space to start recording", Colors.LimeGreen);
                EngineStatusText.Text = "Ready";
                isInitialized = true;
                
                Logger.Info("SuperWhisper WPF initialization completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Initialization failed: {ex.Message}", ex);
                UpdateStatus($"❌ Initialization failed: {ex.Message}", Colors.Red);
            }
        }

        private void OnHotkeyPressed(object sender, EventArgs e)
        {
            if (!isInitialized) return;
            
            Dispatcher.Invoke(() => ToggleRecording());
        }

        private void ToggleRecording()
        {
            try
            {
                if (!isRecording)
                {
                    StartRecording();
                }
                else
                {
                    StopRecording();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error toggling recording: {ex.Message}", ex);
                UpdateStatus($"❌ Recording error: {ex.Message}", Colors.Red);
            }
        }

        private void StartRecording()
        {
            if (isRecording) return;
            
            Logger.Info("Starting recording...");
            audioCapture.StartRecording();
            isRecording = true;
            
            UpdateStatus("🎤 Recording - Press Ctrl+Space to stop", Colors.Orange);
            StatusIcon.Glyph = "\uE720"; // Microphone icon
            
            // Show recording overlay
            recordingOverlay.Show("🎤 Recording - Press Ctrl+Space to stop");
        }

        private void StopRecording()
        {
            if (!isRecording) return;
            
            Logger.Info("Stopping recording...");
            audioCapture.StopRecording();
            isRecording = false;
            
            UpdateStatus("⏳ Processing audio...", Colors.DeepSkyBlue);
            StatusIcon.Glyph = "\uE8B5"; // Processing icon
            
            // Update recording overlay
            recordingOverlay.Show("⏳ Processing audio...");
        }

        private async void OnSpeechEnded(object sender, byte[] audioData)
        {
            try
            {
                Logger.Info($"Speech ended, processing {audioData.Length} bytes of audio data");
                
                // Reset recording state
                isRecording = false;
                
                // Process the audio
                Dispatcher.Invoke(() => {
                    UpdateStatus("⏳ Transcribing audio...", Colors.DeepSkyBlue);
                    recordingOverlay.Show("⏳ Transcribing audio...");
                });
                
                var transcription = await whisperEngine.TranscribeAsync(audioData);
                
                if (!string.IsNullOrWhiteSpace(transcription))
                {
                    // Update UI on the main thread
                    Dispatcher.Invoke(() =>
                    {
                        AppendTranscription(transcription);
                        UpdateStatus("✅ Transcription complete - Press Ctrl+Space to record again", Colors.LimeGreen);
                        recordingOverlay.ShowTemporary("✅ Transcription complete", 2000);
                    });
                    
                    Logger.Info($"Transcription completed: '{transcription}'");
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateStatus("⚠️ No speech detected - Press Ctrl+Space to try again", Colors.Orange);
                        recordingOverlay.ShowTemporary("⚠️ No speech detected", 2000);
                    });
                    
                    Logger.Warning("No transcription result");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing speech: {ex.Message}", ex);
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus($"❌ Processing error: {ex.Message}", Colors.Red);
                    recordingOverlay.ShowTemporary("❌ Processing error", 3000);
                });
            }
        }

        private void OnAudioLevelChanged(object sender, float level)
        {
            // Update audio level indicator on UI thread
            Dispatcher.Invoke(() =>
            {
                AudioLevelBar.Value = level;
            });
            
            // Also update recording overlay
            recordingOverlay?.UpdateAudioLevel(level);
        }

        private void UpdateStatus(string message, System.Windows.Media.Color color)
        {
            StatusText.Text = message;
            StatusIcon.Foreground = new SolidColorBrush(color);
        }

        private void AppendTranscription(string text)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var formattedText = $"[{timestamp}] {text}";
            
            if (!string.IsNullOrWhiteSpace(ResultsTextBox.Text))
            {
                ResultsTextBox.Text += Environment.NewLine + Environment.NewLine + formattedText;
            }
            else
            {
                ResultsTextBox.Text = formattedText;
            }
            
            // Scroll to end
            ResultsTextBox.ScrollToEnd();
            
            // Enable buttons
            CopyButton.IsEnabled = true;
            ClearButton.IsEnabled = true;
            
            // Update word count
            UpdateWordCount();
        }

        private void UpdateWordCount()
        {
            if (string.IsNullOrWhiteSpace(ResultsTextBox.Text))
            {
                WordCountText.Text = "0 words";
                return;
            }
            
            var words = ResultsTextBox.Text
                .Split(new char[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !w.StartsWith("[") || !w.EndsWith("]")) // Exclude timestamps
                .Count();
            
            WordCountText.Text = $"{words} word{(words != 1 ? "s" : "")}";
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var text = ResultsTextBox.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    System.Windows.Clipboard.SetText(text);
                    
                    // Visual feedback
                    var originalContent = CopyButton.Content;
                    var originalBackground = CopyButton.Background;
                    
                    CopyButton.Content = new StackPanel
                    {
                        Orientation = System.Windows.Controls.Orientation.Horizontal,
                        Children = {
                            new ModernWpf.Controls.FontIcon { Glyph = "\uE8FB", FontSize = 14, Margin = new Thickness(0,0,8,0) },
                            new TextBlock { Text = "Copied!" }
                        }
                    };
                    CopyButton.Background = new SolidColorBrush(Colors.Green);
                    
                    await Task.Delay(1500);
                    
                    CopyButton.Content = originalContent;
                    CopyButton.Background = originalBackground;
                    
                    Logger.Info("Text copied to clipboard");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error copying to clipboard: {ex.Message}", ex);
                UpdateStatus($"❌ Copy failed: {ex.Message}", Colors.Red);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ResultsTextBox.Clear();
            CopyButton.IsEnabled = false;
            ClearButton.IsEnabled = false;
            UpdateWordCount();
            UpdateStatus("🗑️ Results cleared", Colors.Gray);
            Logger.Info("Results cleared");
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            // Handle Ctrl+C for copying
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (CopyButton.IsEnabled)
                {
                    CopyButton_Click(null, null);
                }
            }
            
            base.OnKeyDown(e);
        }

        private void InitializeSystemTray()
        {
            // Create system tray icon
            trayIcon = new NotifyIcon();
            trayIcon.Icon = CreateTrayIcon();
            trayIcon.Text = "SuperWhisper - Press Ctrl+Space to record";
            trayIcon.Visible = true;
            
            // Double-click to show/hide window
            trayIcon.DoubleClick += (s, e) => {
                if (this.Visibility == Visibility.Visible)
                {
                    this.Hide();
                }
                else
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                }
            };
            
            // Context menu
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Show", null, (s, e) => {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
            });
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Exit", null, (s, e) => {
                isExiting = true;
                this.Close();
            });
            
            trayIcon.ContextMenuStrip = contextMenu;
        }
        
        private System.Drawing.Icon CreateTrayIcon()
        {
            // Create a simple icon programmatically
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.FillEllipse(new SolidBrush(System.Drawing.Color.DeepSkyBlue), 2, 2, 12, 12);
                g.FillEllipse(new SolidBrush(System.Drawing.Color.White), 5, 5, 6, 6);
            }
            return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!isExiting)
            {
                // Minimize to tray instead of closing
                e.Cancel = true;
                this.Hide();
                
                // Show notification
                trayIcon.ShowBalloonTip(2000, "SuperWhisper", 
                    "Application minimized to tray. Double-click the tray icon to restore. Ctrl+Space still works!", 
                    ToolTipIcon.Info);
                
                Logger.Info("Window minimized to tray - hotkeys remain active");
            }
            
            base.OnClosing(e);
        }
        
        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                this.Hide();
                
                // Show notification
                trayIcon.ShowBalloonTip(2000, "SuperWhisper", 
                    "Application minimized to tray. Double-click the tray icon to restore. Ctrl+Space still works!", 
                    ToolTipIcon.Info);
                
                Logger.Info("Window minimized to tray via minimize button - hotkeys remain active");
            }
            
            base.OnStateChanged(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                Logger.Info("Shutting down SuperWhisper...");
                
                trayIcon?.Dispose();
                recordingOverlay?.Close();
                globalHotkey?.Dispose();
                audioCapture?.Dispose();
                whisperEngine?.Dispose();
                typingTimer?.Stop();
                
                Logger.Info("SuperWhisper shutdown complete");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during shutdown: {ex.Message}", ex);
            }
            
            base.OnClosed(e);
        }
    }
}
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
        // Constants
        private const int TYPING_TIMER_DELAY_MS = 500;
        private const int BALLOON_TIP_TIMEOUT_MS = 2000;
        private const int OVERLAY_SUCCESS_DURATION_MS = 2000;
        private const int OVERLAY_ERROR_DURATION_MS = 3000;
        private const int COPY_FEEDBACK_DURATION_MS = 1500;
        private const int TRAY_ICON_SIZE = 16;

        private WhisperEngine whisperEngine;
        private AudioCapture audioCapture;
        private GlobalHotkey globalHotkey;
        private DispatcherTimer recordingTimer;
        private DateTime recordingStartTime;
        private System.Windows.Media.Animation.Storyboard toastPulseAnimation;
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
                    UpdateStatus("Failed", System.Windows.Media.Colors.Red);
                    return;
                }
                
                // Initialize audio capture
                audioCapture = new AudioCapture();
                audioCapture.SpeechEnded += OnSpeechEnded;
                audioCapture.AudioLevelChanged += OnAudioLevelChanged;
                audioCapture.ApproachingLimit += OnApproachingLimit;
                
                // Initialize global hotkey (Ctrl+Space) using proven Windows Forms approach
                globalHotkey = HotkeyExtensions.CreateCtrlSpace(this, () => OnHotkeyPressed(null, null));
                
                // Initialize recording timer
                recordingTimer = new DispatcherTimer();
                recordingTimer.Interval = TimeSpan.FromMilliseconds(100);
                recordingTimer.Tick += UpdateRecordingTime;

                // Initialize toast pulse animation
                InitializeToastAnimation();
                
                // Initialize system tray
                InitializeSystemTray();
                
                // Initialize typing timer for word count
                typingTimer = new DispatcherTimer();
                typingTimer.Interval = TimeSpan.FromMilliseconds(TYPING_TIMER_DELAY_MS);
                typingTimer.Tick += (s, e) => UpdateWordCount();
                ResultsTextBox.TextChanged += (s, e) => {
                    typingTimer.Stop();
                    typingTimer.Start();
                };
                
                UpdateStatus("Ready", System.Windows.Media.Colors.Green);

            // Clear placeholder text when app is ready
            if (ResultsTextBox.Text == "Your transcriptions will appear here...")
            {
                ResultsTextBox.Text = "";
                ResultsTextBox.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Gray);
                ResultsTextBox.GotFocus += (s, e) => {
                    if (string.IsNullOrWhiteSpace(ResultsTextBox.Text))
                    {
                        ResultsTextBox.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x19, 0x19, 0x19));
                    }
                };
            }
                isInitialized = true;
                
                Logger.Info("Lumina initialization completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Initialization failed: {ex.Message}", ex);
                UpdateStatus("Failed", System.Windows.Media.Colors.Red);
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
                UpdateStatus("Error", System.Windows.Media.Colors.Red);
            }
        }

        private void StartRecording()
        {
            if (isRecording) return;
            
            Logger.Info("Starting recording...");
            audioCapture.StartRecording();
            isRecording = true;
            
            UpdateStatus("Recording", System.Windows.Media.Colors.Orange);
            
            // Show toast notification
            recordingStartTime = DateTime.Now;
            recordingTimer.Start();
            ShowToast("Recording", "00:00");
        }

        private void StopRecording()
        {
            if (!isRecording) return;
            
            Logger.Info("Stopping recording...");
            audioCapture.StopRecording();
            isRecording = false;
            
            UpdateStatus("Processing", System.Windows.Media.Colors.DeepSkyBlue);
            
            // Update toast notification
            recordingTimer.Stop();
            ShowToast("Processing", "⏳");
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
                    UpdateStatus("Transcribing", System.Windows.Media.Colors.DeepSkyBlue);
                    ShowToast("Processing", "⏳");
                });
                
                var transcription = await whisperEngine.TranscribeAsync(audioData);
                
                if (!string.IsNullOrWhiteSpace(transcription))
                {
                    // Update UI on the main thread
                    Dispatcher.Invoke(() =>
                    {
                        AppendTranscription(transcription);
                        UpdateStatus("Ready", System.Windows.Media.Colors.Green);
                        HideToast();
                        RecordingHintText.Text = "Press Ctrl+Space to start recording";
                    });
                    
                    Logger.Info($"Transcription completed: '{transcription}'");
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateStatus("No speech", System.Windows.Media.Colors.Orange);
                        HideToast();
                        RecordingHintText.Text = "No speech detected - Press Ctrl+Space to try again";

                        // Reset hint text after delay
                        Task.Delay(3000).ContinueWith(t =>
                        {
                            Dispatcher.Invoke(() => RecordingHintText.Text = "Press Ctrl+Space to start recording");
                        });
                    });
                    
                    Logger.Warning("No transcription result");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing speech: {ex.Message}", ex);
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus("Error", System.Windows.Media.Colors.Red);
                    HideToast();
                    RecordingHintText.Text = "Error processing audio - Press Ctrl+Space to try again";

                    // Reset hint text after delay
                    Task.Delay(3000).ContinueWith(t =>
                    {
                        Dispatcher.Invoke(() => RecordingHintText.Text = "Press Ctrl+Space to start recording");
                    });
                });
            }
        }

        private void OnAudioLevelChanged(object sender, float level)
        {
            // Toast notification doesn't need audio level updates
            // This could be used for other visual feedback if needed
        }

        private void OnApproachingLimit(object sender, int remainingSeconds)
        {
            // Show warning when approaching recording limit
            Dispatcher.Invoke(() =>
            {
                var remainingMinutes = remainingSeconds / 60.0;
                RecordingHintText.Text = $"Warning: {remainingMinutes:F1} minutes remaining";
                UpdateStatus("Warning", System.Windows.Media.Colors.Orange);
            });
        }

        private void UpdateStatus(string message, System.Windows.Media.Color color)
        {
            StatusText.Text = message;
            StatusIndicator.Fill = new SolidColorBrush(color);
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
            
            // Word count removed in minimal UI
        }

        private void UpdateWordCount()
        {
            // Word count removed in minimal UI design
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
                    CopyButton.Background = new SolidColorBrush(System.Windows.Media.Colors.Green);
                    
                    await Task.Delay(COPY_FEEDBACK_DURATION_MS);
                    
                    CopyButton.Content = originalContent;
                    CopyButton.Background = originalBackground;
                    
                    Logger.Info("Text copied to clipboard");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error copying to clipboard: {ex.Message}", ex);
                UpdateStatus("Copy failed", System.Windows.Media.Colors.Red);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ResultsTextBox.Clear();
            CopyButton.IsEnabled = false;
            ClearButton.IsEnabled = false;
            UpdateStatus("Cleared", System.Windows.Media.Colors.Gray);
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
            trayIcon.Text = "Lumina - Press Ctrl+Space to record";
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
            // Load the actual gradient orb icon
            try
            {
                // Try to load lumina-icon.ico from the application directory
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lumina-icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    return new System.Drawing.Icon(iconPath);
                }

                // Fallback: try to load from the project directory (for debug mode)
                iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "lumina-icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    return new System.Drawing.Icon(iconPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not load lumina-icon.ico: {ex.Message}");
            }

            // Fallback to programmatic icon if file not found
            var bitmap = new Bitmap(TRAY_ICON_SIZE, TRAY_ICON_SIZE);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.FillEllipse(new SolidBrush(System.Drawing.Color.DeepSkyBlue), 2, 2, 12, 12);
                g.FillEllipse(new SolidBrush(System.Drawing.Color.White), 5, 5, 6, 6);
            }
            return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
        }

        private void InitializeToastAnimation()
        {
            // Create pulse animation for toast indicator
            var pulseAnimation = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
            pulseAnimation.Duration = TimeSpan.FromSeconds(1);
            pulseAnimation.RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever;

            pulseAnimation.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, TimeSpan.FromSeconds(0)));
            pulseAnimation.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0.5, TimeSpan.FromSeconds(0.5)));
            pulseAnimation.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, TimeSpan.FromSeconds(1)));

            toastPulseAnimation = new System.Windows.Media.Animation.Storyboard();
            System.Windows.Media.Animation.Storyboard.SetTarget(pulseAnimation, ToastPulseTransform);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(pulseAnimation, new PropertyPath("ScaleX"));
            toastPulseAnimation.Children.Add(pulseAnimation);

            var pulseAnimationY = pulseAnimation.Clone();
            System.Windows.Media.Animation.Storyboard.SetTarget(pulseAnimationY, ToastPulseTransform);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(pulseAnimationY, new PropertyPath("ScaleY"));
            toastPulseAnimation.Children.Add(pulseAnimationY);
        }

        private void ShowToast(string text, string time)
        {
            ToastText.Text = text;
            ToastTime.Text = time;
            ToastNotification.Visibility = Visibility.Visible;

            if (text == "Recording")
            {
                ToastIndicator.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44)); // Red
                toastPulseAnimation.Begin();
                RecordingHintText.Text = "Recording... Press Ctrl+Space to stop";
            }
            else if (text == "Processing")
            {
                ToastIndicator.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x84, 0xFF)); // Blue
                toastPulseAnimation.Stop();
                RecordingHintText.Text = "Processing audio...";
            }
        }

        private void HideToast()
        {
            ToastNotification.Visibility = Visibility.Collapsed;
            toastPulseAnimation.Stop();
        }

        private void UpdateRecordingTime(object sender, EventArgs e)
        {
            if (isRecording)
            {
                var elapsed = DateTime.Now - recordingStartTime;
                var timeString = $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
                ToastTime.Text = timeString;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!isExiting)
            {
                // Minimize to tray instead of closing
                e.Cancel = true;
                this.Hide();
                
                // Show notification
                trayIcon.ShowBalloonTip(BALLOON_TIP_TIMEOUT_MS, "Lumina",
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
                trayIcon.ShowBalloonTip(BALLOON_TIP_TIMEOUT_MS, "Lumina",
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
                Logger.Info("Shutting down Lumina...");

                // Stop and dispose timers first
                if (typingTimer != null)
                {
                    typingTimer.Stop();
                    typingTimer = null;
                }

                if (recordingTimer != null)
                {
                    recordingTimer.Stop();
                    recordingTimer = null;
                }

                if (toastPulseAnimation != null)
                {
                    toastPulseAnimation.Stop();
                    toastPulseAnimation = null;
                }

                // Dispose tray icon
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                    trayIcon = null;
                }

                // Toast notification is part of main window, no separate disposal needed

                // Dispose hotkey
                if (globalHotkey != null)
                {
                    globalHotkey.Dispose();
                    globalHotkey = null;
                }

                // Dispose audio capture
                if (audioCapture != null)
                {
                    audioCapture.Dispose();
                    audioCapture = null;
                }

                // Dispose whisper engine
                if (whisperEngine != null)
                {
                    whisperEngine.Dispose();
                    whisperEngine = null;
                }

                Logger.Info("Lumina shutdown complete");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during shutdown: {ex.Message}", ex);
            }

            base.OnClosed(e);
        }
    }
}
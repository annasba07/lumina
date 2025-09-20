using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SuperWhisperWPF
{
    public partial class RecordingOverlay : Window
    {
        private DispatcherTimer animationTimer;
        private float currentAudioLevel = 0f;
        private float[] waveformHistory = new float[40]; // Number of bars
        private int historyIndex = 0;
        private float pulsePhase = 0f;

        public RecordingOverlay()
        {
            InitializeComponent();
            InitializeAnimation();
            this.Visibility = Visibility.Hidden;
        }

        private void InitializeAnimation()
        {
            // Initialize waveform bars
            for (int i = 0; i < waveformHistory.Length; i++)
            {
                var bar = new Rectangle
                {
                    Width = 6,
                    Height = 10,
                    Fill = new SolidColorBrush(Color.FromArgb(80, 0, 122, 204)),
                    RadiusX = 2,
                    RadiusY = 2
                };
                
                Canvas.SetLeft(bar, i * 10);
                Canvas.SetTop(bar, 20);
                
                WaveformCanvas.Children.Add(bar);
            }

            // Start animation timer
            animationTimer = new DispatcherTimer();
            animationTimer.Interval = TimeSpan.FromMilliseconds(50); // 20 FPS
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            pulsePhase += 0.2f;
            if (pulsePhase > Math.PI * 2) pulsePhase = 0;

            // Update glow effect
            var glowIntensity = (float)(0.7 + 0.3 * Math.Sin(pulsePhase));
            GlowScale.ScaleX = glowIntensity;
            GlowScale.ScaleY = glowIntensity;

            // Update waveform bars
            for (int i = 0; i < waveformHistory.Length; i++)
            {
                if (i < WaveformCanvas.Children.Count - 1) // Exclude the glow ellipse
                {
                    var bar = WaveformCanvas.Children[i + 1] as Rectangle;
                    if (bar != null)
                    {
                        var dataIndex = (historyIndex + i) % waveformHistory.Length;
                        var level = waveformHistory[dataIndex];
                        
                        // Animate height based on audio level
                        var targetHeight = Math.Max(4, level * 40);
                        var currentHeight = bar.Height;
                        bar.Height = currentHeight + (targetHeight - currentHeight) * 0.3; // Smooth interpolation
                        
                        // Update position to center vertically
                        Canvas.SetTop(bar, 35 - bar.Height / 2);
                        
                        // Update color based on level
                        Color barColor;
                        if (level > 0.8f)
                            barColor = Color.FromArgb(200, 255, 100, 120); // Vibrant red-pink
                        else if (level > 0.5f)
                            barColor = Color.FromArgb(200, 100, 255, 150); // Bright green
                        else if (level > 0.2f)
                            barColor = Color.FromArgb(200, 120, 200, 255); // Sky blue
                        else
                            barColor = Color.FromArgb(120, 180, 180, 255); // Soft purple
                        
                        bar.Fill = new SolidColorBrush(barColor);
                    }
                }
            }
        }

        public new void Show(string message)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
                this.Visibility = Visibility.Visible;
                this.Activate();
                
                // Position at top center of screen
                var screen = SystemParameters.WorkArea;
                this.Left = (screen.Width - this.Width) / 2;
                this.Top = screen.Top + 80;
            });
        }

        public new void Hide()
        {
            Dispatcher.Invoke(() =>
            {
                this.Visibility = Visibility.Hidden;
            });
        }

        public void UpdateAudioLevel(float level)
        {
            Dispatcher.Invoke(() =>
            {
                currentAudioLevel = Math.Max(0, Math.Min(1, level));
                
                // Add to waveform history
                waveformHistory[historyIndex] = currentAudioLevel;
                historyIndex = (historyIndex + 1) % waveformHistory.Length;
            });
        }

        public void ShowTemporary(string message, int durationMs = 2000)
        {
            Show(message);
            
            Task.Delay(durationMs).ContinueWith(t =>
            {
                Dispatcher.Invoke(() => Hide());
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            animationTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
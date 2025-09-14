using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SuperWhisperWindows
{
    public class RecordingOverlay : Form
    {
        private Label statusLabel;
        private Panel waveformPanel;
        private Timer fadeTimer;
        private Timer animationTimer;
        private float currentAudioLevel = 0f;
        private int fadeOpacity = 255;
        private float[] waveformHistory = new float[60]; // 3 seconds at 20fps
        private int historyIndex = 0;
        private float pulsePhase = 0f;
        
        public RecordingOverlay()
        {
            InitializeOverlay();
            CreateControls();
        }
        
        private void InitializeOverlay()
        {
            // Window properties
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(20, 20, 25);
            Size = new Size(360, 100);
            
            // Rounded corners and transparency
            SetStyle(ControlStyles.AllPaintingInWmPaint | 
                    ControlStyles.UserPaint | 
                    ControlStyles.DoubleBuffer, true);
            
            // Initially hidden
            Visible = false;
            WindowState = FormWindowState.Normal;
            
            // Position at top center of screen
            var screen = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                (screen.Width - Width) / 2,
                screen.Top + 50
            );
        }
        
        private void CreateControls()
        {
            // Status label with better styling
            statusLabel = new Label
            {
                Text = "🎤 Listening...",
                ForeColor = Color.FromArgb(220, 220, 220),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 13, FontStyle.Regular),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(Width, 35),
                Location = new Point(0, 8)
            };
            
            // Custom waveform panel
            waveformPanel = new Panel
            {
                Size = new Size(Width - 40, 45),
                Location = new Point(20, 45),
                BackColor = Color.Transparent
            };
            waveformPanel.Paint += WaveformPanel_Paint;
            
            // Add controls
            Controls.Add(statusLabel);
            Controls.Add(waveformPanel);
            
            // Initialize timers
            fadeTimer = new Timer
            {
                Interval = 50 // 20 FPS
            };
            fadeTimer.Tick += FadeTimer_Tick;
            
            animationTimer = new Timer
            {
                Interval = 50 // 20 FPS for smooth animation
            };
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
        }
        
        public void Show(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Show), message);
                return;
            }
            
            statusLabel.Text = message;
            fadeOpacity = 255;
            Opacity = 1.0;
            
            if (!Visible)
            {
                Visible = true;
                BringToFront();
            }
            
            // Animate appearance
            AnimateIn();
        }
        
        public void UpdateAudioLevel(float level)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<float>(UpdateAudioLevel), level);
                return;
            }
            
            currentAudioLevel = Math.Max(0, Math.Min(1, level));
            
            // Add to waveform history
            waveformHistory[historyIndex] = currentAudioLevel;
            historyIndex = (historyIndex + 1) % waveformHistory.Length;
            
            // Trigger repaint
            waveformPanel?.Invalidate();
        }
        
        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            pulsePhase += 0.2f;
            if (pulsePhase > Math.PI * 2) pulsePhase = 0;
            waveformPanel?.Invalidate();
        }
        
        private void WaveformPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            var width = waveformPanel.Width;
            var height = waveformPanel.Height;
            var centerY = height / 2;
            
            // Draw background glow
            var glowIntensity = (float)(0.3 + 0.2 * Math.Sin(pulsePhase));
            using (var glowBrush = new SolidBrush(Color.FromArgb((int)(30 * glowIntensity), 100, 200, 255)))
            {
                g.FillEllipse(glowBrush, width / 2 - 60, centerY - 15, 120, 30);
            }
            
            // Draw waveform bars
            var barWidth = (float)width / waveformHistory.Length;
            
            for (int i = 0; i < waveformHistory.Length; i++)
            {
                var dataIndex = (historyIndex + i) % waveformHistory.Length;
                var level = waveformHistory[dataIndex];
                
                if (level > 0.01f)
                {
                    var barHeight = level * (height - 10);
                    var x = i * barWidth;
                    var y = centerY - barHeight / 2;
                    
                    // Color based on level and age
                    var age = (float)i / waveformHistory.Length;
                    var alpha = (int)(100 + 155 * age);
                    
                    Color barColor;
                    if (level > 0.7f)
                        barColor = Color.FromArgb(alpha, 255, 120, 120); // Red
                    else if (level > 0.3f)
                        barColor = Color.FromArgb(alpha, 120, 255, 120); // Green  
                    else
                        barColor = Color.FromArgb(alpha, 100, 180, 255); // Blue
                    
                    using (var brush = new SolidBrush(barColor))
                    {
                        g.FillRectangle(brush, x, y, Math.Max(2, barWidth - 1), barHeight);
                    }
                }
            }
            
            // Draw center line
            using (var pen = new Pen(Color.FromArgb(80, 255, 255, 255), 1))
            {
                g.DrawLine(pen, 0, centerY, width, centerY);
            }
        }
        
        public new void Hide()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(Hide));
                return;
            }
            
            // Fade out animation
            AnimateOut();
        }
        
        private void AnimateIn()
        {
            // Simple scale animation
            var targetSize = new Size(300, 80);
            var startSize = new Size(200, 60);
            
            Size = startSize;
            
            var timer = new Timer { Interval = 16 }; // ~60 FPS
            var steps = 10;
            var currentStep = 0;
            
            timer.Tick += (s, e) =>
            {
                currentStep++;
                var progress = (float)currentStep / steps;
                
                var width = (int)(startSize.Width + (targetSize.Width - startSize.Width) * progress);
                var height = (int)(startSize.Height + (targetSize.Height - startSize.Height) * progress);
                
                Size = new Size(width, height);
                
                // Recenter
                var screen = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(
                    (screen.Width - Width) / 2,
                    screen.Top + 50
                );
                
                if (currentStep >= steps)
                {
                    timer.Stop();
                    timer.Dispose();
                }
            };
            
            timer.Start();
        }
        
        private void AnimateOut()
        {
            fadeTimer.Start();
        }
        
        private void FadeTimer_Tick(object sender, EventArgs e)
        {
            fadeOpacity -= 15; // Fade speed
            
            if (fadeOpacity <= 0)
            {
                fadeOpacity = 0;
                Opacity = 0;
                Visible = false;
                fadeTimer.Stop();
            }
            else
            {
                Opacity = fadeOpacity / 255.0;
            }
        }
        
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            // Draw rounded rectangle background
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            var radius = 10;
            
            using (var path = CreateRoundedRectanglePath(rect, radius))
            {
                using (var brush = new SolidBrush(Color.FromArgb(230, 20, 20, 30)))
                {
                    g.FillPath(brush, path);
                }
                
                // Border with subtle glow
                using (var pen = new Pen(Color.FromArgb(120, 100, 180, 255), 2))
                {
                    g.DrawPath(pen, path);
                }
            }
        }
        
        private System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            
            return path;
        }
        
        protected override bool ShowWithoutActivation => true;
        
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }
        
        // Auto-hide after delay when showing status messages
        public void ShowTemporary(string message, int durationMs = 2000)
        {
            Show(message);
            
            Task.Delay(durationMs).ContinueWith(t =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke(new Action(Hide));
                }
            });
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                fadeTimer?.Dispose();
                animationTimer?.Dispose();
                statusLabel?.Dispose();
                waveformPanel?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
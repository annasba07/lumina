using System;
using System.Drawing;
using System.Windows.Forms;

namespace SuperWhisperWindows
{
    public class ResultsWindow : Form
    {
        private TextBox textBox;
        private Button copyButton;
        private Button clearButton;
        private Label instructionLabel;
        
        public ResultsWindow()
        {
            InitializeWindow();
            CreateControls();
        }
        
        private void InitializeWindow()
        {
            Text = "SuperWhisper - Transcription Results";
            Size = new Size(500, 350);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ShowInTaskbar = true;
            Icon = SystemIcons.Information;
            
            // Dark theme
            BackColor = Color.FromArgb(40, 40, 40);
            ForeColor = Color.White;
        }
        
        private void CreateControls()
        {
            // Instruction label
            instructionLabel = new Label
            {
                Text = "Transcribed text appears here. Click Copy to copy to clipboard.",
                Location = new Point(10, 10),
                Size = new Size(Width - 20, 25),
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            
            // Text box for results
            textBox = new TextBox
            {
                Location = new Point(10, 40),
                Size = new Size(Width - 20, Height - 120),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 11),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true
            };
            
            // Copy button
            copyButton = new Button
            {
                Text = "📋 Copy",
                Location = new Point(10, Height - 70),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Enabled = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            copyButton.FlatAppearance.BorderSize = 0;
            copyButton.Click += CopyButton_Click;
            
            // Clear button
            clearButton = new Button
            {
                Text = "🗑️ Clear",
                Location = new Point(120, Height - 70),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(80, 80, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Enabled = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            clearButton.FlatAppearance.BorderSize = 0;
            clearButton.Click += ClearButton_Click;
            
            // Add controls
            Controls.Add(instructionLabel);
            Controls.Add(textBox);
            Controls.Add(copyButton);
            Controls.Add(clearButton);
        }
        
        public void AddTranscription(string text, int processingTimeMs)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, int>(AddTranscription), text, processingTimeMs);
                return;
            }
            
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var formattedText = $"[{timestamp}] ({processingTimeMs}ms) {text}";
            
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                textBox.Text += Environment.NewLine + Environment.NewLine + formattedText;
            }
            else
            {
                textBox.Text = formattedText;
            }
            
            // Scroll to bottom
            textBox.SelectionStart = textBox.Text.Length;
            textBox.ScrollToCaret();
            
            // Enable buttons
            copyButton.Enabled = true;
            clearButton.Enabled = true;
            
            // Show window if hidden
            if (!Visible)
            {
                Show();
                BringToFront();
            }
            
            // Flash taskbar to get attention
            FlashWindow();
        }
        
        private void CopyButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                Clipboard.SetText(textBox.Text);
                
                // Temporary feedback
                var originalText = copyButton.Text;
                copyButton.Text = "✅ Copied!";
                copyButton.BackColor = Color.FromArgb(0, 150, 0);
                
                var timer = new Timer { Interval = 1500 };
                timer.Tick += (s, args) =>
                {
                    copyButton.Text = originalText;
                    copyButton.BackColor = Color.FromArgb(0, 120, 215);
                    timer.Stop();
                    timer.Dispose();
                };
                timer.Start();
            }
        }
        
        private void ClearButton_Click(object sender, EventArgs e)
        {
            textBox.Clear();
            copyButton.Enabled = false;
            clearButton.Enabled = false;
        }
        
        private void FlashWindow()
        {
            // Simple window flash using ShowInTaskbar toggle
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            
            Activate();
            TopMost = true;
            TopMost = false;
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Hide instead of close
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                base.OnFormClosing(e);
            }
        }
    }
}
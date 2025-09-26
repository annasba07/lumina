using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SuperWhisperWPF
{
    public class GlobalHotkey : IDisposable
    {
        // Win32 API declarations
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        // Virtual key codes
        public const uint VK_SPACE = 0x20;
        public const uint VK_R = 0x52;  // R key for recording
        public const uint VK_F1 = 0x70;
        public const uint VK_F2 = 0x71;
        public const uint VK_F3 = 0x72;
        public const uint VK_F4 = 0x73;
        public const uint VK_F5 = 0x74;
        public const uint VK_F6 = 0x75;
        public const uint VK_F7 = 0x76;
        public const uint VK_F8 = 0x77;
        public const uint VK_F9 = 0x78;
        public const uint VK_F10 = 0x79;
        public const uint VK_F11 = 0x7A;
        public const uint VK_F12 = 0x7B;
        private const uint VK_CONTROL = 0x11;
        
        // Modifier key flags
        private const uint MOD_NONE = 0x0000;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        // Windows message
        private const int WM_HOTKEY = 0x0312;
        
        private readonly int hotkeyId;
        private readonly Action callback;
        private readonly HiddenForm form;
        private bool isRegistered = false;

        public event EventHandler HotkeyPressed;

        public GlobalHotkey(object parentWindow, uint modifier = MOD_CONTROL, uint key = VK_SPACE)
        {
            this.hotkeyId = this.GetHashCode();
            
            // Create hidden form to receive messages - this is the key to reliable hotkeys!
            form = new HiddenForm(OnHotkeyPressed);
            
            // Register the hotkey
            RegisterHotkeyInternal(modifier, key);
        }

        private void RegisterHotkeyInternal(uint modifier, uint key)
        {
            if (isRegistered) return;

            // Add MOD_NOREPEAT to prevent key repeat events
            modifier |= MOD_NOREPEAT;

            // Try to unregister first in case it's already registered
            UnregisterHotKey(form.Handle, hotkeyId);

            if (RegisterHotKey(form.Handle, hotkeyId, modifier, key))
            {
                isRegistered = true;
                Logger.Info($"Successfully registered hotkey using Windows Forms message loop (ID: {hotkeyId})");
            }
            else
            {
                var error = GetLastError();
                var hotkeyName = GetHotkeyName(modifier, key);
                var errorMessage = GetHotkeyErrorMessage(error, hotkeyName);

                Logger.Error($"Failed to register hotkey {hotkeyName}. {errorMessage}");

                // Show user-friendly error message
                System.Windows.Forms.MessageBox.Show(
                    $"Unable to register the hotkey {hotkeyName}.\n\n{errorMessage}\n\nPlease check if another application is using this hotkey or try configuring a different hotkey combination.",
                    "SuperWhisper - Hotkey Registration Failed",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning
                );
            }
        }

        private string GetHotkeyName(uint modifier, uint key)
        {
            var parts = new System.Collections.Generic.List<string>();

            // Remove MOD_NOREPEAT flag for display
            modifier &= ~MOD_NOREPEAT;

            if ((modifier & MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((modifier & MOD_ALT) != 0) parts.Add("Alt");
            if ((modifier & MOD_SHIFT) != 0) parts.Add("Shift");
            if ((modifier & MOD_WIN) != 0) parts.Add("Win");

            var keyName = key switch
            {
                VK_SPACE => "Space",
                >= VK_F1 and <= VK_F12 => $"F{key - VK_F1 + 1}",
                _ => $"Key{key:X2}"
            };

            parts.Add(keyName);
            return string.Join("+", parts);
        }

        private string GetHotkeyErrorMessage(uint errorCode, string hotkeyName)
        {
            return errorCode switch
            {
                1409 => $"The hotkey {hotkeyName} is already registered by another application.",
                87 => "Invalid hotkey parameters. Please check your configuration.",
                1400 => "Invalid window handle. Please restart the application.",
                _ => $"Error code {errorCode}: The hotkey might be in use by another application or Windows."
            };
        }

        private void OnHotkeyPressed()
        {
            Logger.Debug($"Hotkey pressed (ID: {hotkeyId}) - triggering event");
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (isRegistered)
            {
                UnregisterHotKey(form.Handle, hotkeyId);
                isRegistered = false;
                Logger.Info("Global hotkey unregistered");
            }
            
            form?.Dispose();
        }

        // Hidden form to receive Windows messages - this is the proven approach!
        private class HiddenForm : Form
        {
            private readonly Action hotkeyCallback;

            public HiddenForm(Action hotkeyCallback)
            {
                this.hotkeyCallback = hotkeyCallback;
                
                // Make form invisible and non-interactable
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
                Visible = false;
                Size = new System.Drawing.Size(0, 0);
                
                // Ensure handle is created
                var handle = Handle;
                Logger.Info("Hidden Windows Forms window created for hotkey handling");
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    Logger.Debug($"WM_HOTKEY message received: wParam={m.WParam.ToInt32()}");
                    hotkeyCallback?.Invoke();
                }
                
                base.WndProc(ref m);
            }

            protected override void SetVisibleCore(bool value)
            {
                // Prevent form from becoming visible
                base.SetVisibleCore(false);
            }
        }
    }

    // Extension methods for easier hotkey combinations
    public static class HotkeyExtensions
    {
        public static GlobalHotkey CreateCtrlSpace(object parentWindow, Action callback)
        {
            var hotkey = new GlobalHotkey(parentWindow, GlobalHotkey.MOD_CONTROL, GlobalHotkey.VK_SPACE);
            hotkey.HotkeyPressed += (s, e) => callback();
            return hotkey;
        }
        
        public static GlobalHotkey CreateAltSpace(object parentWindow, Action callback)
        {
            var hotkey = new GlobalHotkey(parentWindow, GlobalHotkey.MOD_ALT, GlobalHotkey.VK_SPACE);
            hotkey.HotkeyPressed += (s, e) => callback();
            return hotkey;
        }
        
        public static GlobalHotkey CreateWinSpace(object parentWindow, Action callback)
        {
            var hotkey = new GlobalHotkey(parentWindow, GlobalHotkey.MOD_WIN, GlobalHotkey.VK_SPACE);
            hotkey.HotkeyPressed += (s, e) => callback();
            return hotkey;
        }

        public static GlobalHotkey CreateAltShiftR(object parentWindow, Action callback)
        {
            var hotkey = new GlobalHotkey(parentWindow, GlobalHotkey.MOD_ALT | GlobalHotkey.MOD_SHIFT, GlobalHotkey.VK_R);
            hotkey.HotkeyPressed += (s, e) => callback();
            return hotkey;
        }
    }
}
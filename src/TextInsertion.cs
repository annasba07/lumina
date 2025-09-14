using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SuperWhisperWindows
{
    public class TextInsertion
    {
        // Windows API declarations
        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        
        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
        
        // Input structures
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint Type;
            public INPUTUNION Union;
        }
        
        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT Mouse;
            [FieldOffset(0)]
            public KEYBDINPUT Keyboard;
            [FieldOffset(0)]
            public HARDWAREINPUT Hardware;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }
        
        // Constants
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        
        public void InsertText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            
            // Use different methods based on reliability preference
            if (UseClipboard)
            {
                InsertViaClipboard(text);
            }
            else
            {
                InsertViaKeystrokes(text);
            }
        }
        
        // Property to control insertion method
        public bool UseClipboard { get; set; } = true;
        
        private void InsertViaClipboard(string text)
        {
            try
            {
                // Store original clipboard content
                var originalClipboard = "";
                if (Clipboard.ContainsText())
                {
                    originalClipboard = Clipboard.GetText();
                }
                
                // Set text to clipboard
                Clipboard.SetText(text);
                
                // Small delay to ensure clipboard is set
                Thread.Sleep(10);
                
                // Paste the text (Ctrl+V)
                SendCtrlV();
                
                // Small delay before restoring clipboard
                Thread.Sleep(50);
                
                // Restore original clipboard content
                if (!string.IsNullOrEmpty(originalClipboard))
                {
                    Clipboard.SetText(originalClipboard);
                }
                else
                {
                    Clipboard.Clear();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Clipboard insertion failed: {ex.Message}");
                // Fallback to keystrokes
                InsertViaKeystrokes(text);
            }
        }
        
        private void SendCtrlV()
        {
            var inputs = new INPUT[4];
            
            // Ctrl down
            inputs[0] = CreateKeyboardInput(0x11, false); // VK_CONTROL
            
            // V down
            inputs[1] = CreateKeyboardInput(0x56, false); // VK_V
            
            // V up
            inputs[2] = CreateKeyboardInput(0x56, true);  // VK_V
            
            // Ctrl up
            inputs[3] = CreateKeyboardInput(0x11, true);  // VK_CONTROL
            
            SendInput(4, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
        
        private void InsertViaKeystrokes(string text)
        {
            // Convert text to Unicode input events
            var inputs = new INPUT[text.Length * 2]; // Each character needs key down and key up
            
            for (int i = 0; i < text.Length; i++)
            {
                var character = text[i];
                
                // Key down
                inputs[i * 2] = CreateUnicodeInput(character, false);
                
                // Key up
                inputs[i * 2 + 1] = CreateUnicodeInput(character, true);
            }
            
            // Send all inputs
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
        
        private INPUT CreateKeyboardInput(ushort virtualKey, bool keyUp)
        {
            return new INPUT
            {
                Type = INPUT_KEYBOARD,
                Union = new INPUTUNION
                {
                    Keyboard = new KEYBDINPUT
                    {
                        wVk = virtualKey,
                        wScan = 0,
                        dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }
        
        private INPUT CreateUnicodeInput(char character, bool keyUp)
        {
            return new INPUT
            {
                Type = INPUT_KEYBOARD,
                Union = new INPUTUNION
                {
                    Keyboard = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = character,
                        dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0),
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }
        
        public void InsertTextAsync(string text)
        {
            // Insert text on a separate thread to avoid UI blocking
            Thread.Sleep(10); // Small delay to ensure the calling hotkey is released
            InsertText(text);
        }
    }
    
    // Alternative simpler method using SendKeys (less reliable but simpler)
    public class SimpleTextInsertion
    {
        public void InsertText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            
            // Small delay to ensure hotkey is released
            Thread.Sleep(10);
            
            // Use SendKeys for simple text insertion
            SendKeys.SendWait(text);
        }
    }
}
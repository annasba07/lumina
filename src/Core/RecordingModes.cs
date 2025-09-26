using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SuperWhisperWPF.Core;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// Defines different recording modes for various use cases (inspired by SuperWhisper).
    /// Each mode optimizes settings for specific scenarios.
    /// </summary>
    public class RecordingMode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; } // Segoe MDL2 icon

        // Model settings
        public string ModelSize { get; set; } = "base"; // tiny, base, small
        public string Language { get; set; } = "en";
        public float Temperature { get; set; } = 0.0f;

        // Processing options
        public bool AutoPunctuation { get; set; } = true;
        public bool SmartFormatting { get; set; } = false;
        public bool RemoveFillers { get; set; } = false; // Remove "um", "uh", etc.
        public bool AddTimestamps { get; set; } = false;
        public bool SpeakerLabels { get; set; } = false;

        // Output formatting
        public string OutputTemplate { get; set; } = "{text}";
        public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

        // Context awareness
        public List<string> TriggerApplications { get; set; } = new();
        public bool CaptureWindowTitle { get; set; } = false;
        public bool IncludeClipboard { get; set; } = false;

        // Post-processing
        public string PostProcessScript { get; set; }
        public Dictionary<string, string> Replacements { get; set; } = new();

        // Hotkey override
        public string CustomHotkey { get; set; }
    }

    /// <summary>
    /// Manages recording modes and provides default configurations.
    /// </summary>
    public class RecordingModeManager
    {
        private readonly string modesDirectory;
        private Dictionary<string, RecordingMode> modes;
        private string activeModeld = "quick";

        public RecordingMode ActiveMode => modes.GetValueOrDefault(activeModeld) ?? GetDefaultMode();

        public RecordingModeManager()
        {
            modesDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Constants.App.SETTINGS_FOLDER_NAME,
                "modes"
            );

            Directory.CreateDirectory(modesDirectory);
            LoadModes();
        }

        /// <summary>
        /// Loads all recording modes from disk or creates defaults.
        /// </summary>
        private void LoadModes()
        {
            modes = new Dictionary<string, RecordingMode>();

            // Load custom modes from disk
            foreach (var file in Directory.GetFiles(modesDirectory, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var mode = JsonSerializer.Deserialize<RecordingMode>(json);
                    if (mode != null)
                    {
                        modes[mode.Id] = mode;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to load mode {file}: {ex.Message}", ex);
                }
            }

            // Ensure default modes exist
            EnsureDefaultModes();
        }

        /// <summary>
        /// Creates default recording modes for common use cases.
        /// </summary>
        private void EnsureDefaultModes()
        {
            // Quick Note Mode
            if (!modes.ContainsKey("quick"))
            {
                modes["quick"] = new RecordingMode
                {
                    Id = "quick",
                    Name = "Quick Note",
                    Description = "Fast transcription for quick notes",
                    Icon = "\uE70F", // Lightning icon
                    ModelSize = "tiny",
                    AutoPunctuation = true,
                    SmartFormatting = false,
                    OutputTemplate = "{text}"
                };
            }

            // Meeting Mode
            if (!modes.ContainsKey("meeting"))
            {
                modes["meeting"] = new RecordingMode
                {
                    Id = "meeting",
                    Name = "Meeting",
                    Description = "Optimized for meetings with timestamps",
                    Icon = "\uE716", // People icon
                    ModelSize = "base",
                    AutoPunctuation = true,
                    SmartFormatting = true,
                    AddTimestamps = true,
                    SpeakerLabels = true,
                    RemoveFillers = true,
                    OutputTemplate = "[{timestamp}] {speaker}: {text}",
                    CaptureWindowTitle = true
                };
            }

            // Dictation Mode
            if (!modes.ContainsKey("dictation"))
            {
                modes["dictation"] = new RecordingMode
                {
                    Id = "dictation",
                    Name = "Dictation",
                    Description = "Professional dictation with formatting",
                    Icon = "\uE720", // Microphone icon
                    ModelSize = "base",
                    AutoPunctuation = true,
                    SmartFormatting = true,
                    RemoveFillers = true,
                    OutputTemplate = "{text}",
                    Replacements = new Dictionary<string, string>
                    {
                        { "period", "." },
                        { "comma", "," },
                        { "new paragraph", "\n\n" },
                        { "new line", "\n" }
                    }
                };
            }

            // Code Mode
            if (!modes.ContainsKey("code"))
            {
                modes["code"] = new RecordingMode
                {
                    Id = "code",
                    Name = "Code",
                    Description = "Programming-focused transcription",
                    Icon = "\uE943", // Code icon
                    ModelSize = "base",
                    AutoPunctuation = false,
                    SmartFormatting = false,
                    OutputTemplate = "{text}",
                    TriggerApplications = new List<string>
                    {
                        "Code.exe", "devenv.exe", "notepad++.exe", "sublime_text.exe"
                    },
                    Replacements = new Dictionary<string, string>
                    {
                        { "equals", "=" },
                        { "plus", "+" },
                        { "minus", "-" },
                        { "star", "*" },
                        { "slash", "/" },
                        { "open paren", "(" },
                        { "close paren", ")" },
                        { "open bracket", "[" },
                        { "close bracket", "]" },
                        { "open brace", "{" },
                        { "close brace", "}" }
                    }
                };
            }

            // Medical Mode
            if (!modes.ContainsKey("medical"))
            {
                modes["medical"] = new RecordingMode
                {
                    Id = "medical",
                    Name = "Medical",
                    Description = "Medical dictation with terminology",
                    Icon = "\uE95E", // Medical icon
                    ModelSize = "small", // Best accuracy for medical terms
                    Temperature = 0.1f, // Lower temperature for accuracy
                    AutoPunctuation = true,
                    SmartFormatting = true,
                    AddTimestamps = true,
                    OutputTemplate = "[{timestamp}] {text}",
                    // Medical-specific replacements would go here
                };
            }

            // Email Mode
            if (!modes.ContainsKey("email"))
            {
                modes["email"] = new RecordingMode
                {
                    Id = "email",
                    Name = "Email",
                    Description = "Email composition mode",
                    Icon = "\uE715", // Mail icon
                    ModelSize = "base",
                    AutoPunctuation = true,
                    SmartFormatting = true,
                    RemoveFillers = true,
                    TriggerApplications = new List<string> { "Outlook.exe", "Thunderbird.exe" },
                    IncludeClipboard = true, // For email context
                    OutputTemplate = "{text}"
                };
            }
        }

        /// <summary>
        /// Switches to a different recording mode.
        /// </summary>
        public void SetActiveMode(string modeId)
        {
            if (modes.ContainsKey(modeId))
            {
                activeModeld = modeId;
                Logger.Info($"Switched to recording mode: {modeId}");
                SaveSettings();
            }
        }

        /// <summary>
        /// Creates or updates a custom recording mode.
        /// </summary>
        public async Task SaveModeAsync(RecordingMode mode)
        {
            modes[mode.Id] = mode;

            var filePath = Path.Combine(modesDirectory, $"{mode.Id}.json");
            var json = JsonSerializer.Serialize(mode, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
            Logger.Info($"Saved recording mode: {mode.Id}");
        }

        /// <summary>
        /// Deletes a custom recording mode.
        /// </summary>
        public void DeleteMode(string modeId)
        {
            // Don't allow deleting default modes
            var defaultModes = new[] { "quick", "meeting", "dictation", "code", "medical", "email" };
            if (defaultModes.Contains(modeId))
            {
                Logger.Warning($"Cannot delete default mode: {modeId}");
                return;
            }

            if (modes.Remove(modeId))
            {
                var filePath = Path.Combine(modesDirectory, $"{modeId}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                Logger.Info($"Deleted recording mode: {modeId}");
            }
        }

        /// <summary>
        /// Gets all available recording modes.
        /// </summary>
        public IEnumerable<RecordingMode> GetAllModes()
        {
            return modes.Values.OrderBy(m => m.Name);
        }

        /// <summary>
        /// Detects and suggests the best mode based on context.
        /// </summary>
        public RecordingMode SuggestModeForContext()
        {
            // Get active window
            var activeWindow = GetActiveWindowProcess();

            // Check each mode's trigger applications
            foreach (var mode in modes.Values)
            {
                if (mode.TriggerApplications?.Contains(activeWindow) == true)
                {
                    Logger.Info($"Auto-selected mode '{mode.Name}' for {activeWindow}");
                    return mode;
                }
            }

            // Default to active mode
            return ActiveMode;
        }

        private string GetActiveWindowProcess()
        {
            try
            {
                var handle = Win32.GetForegroundWindow();
                Win32.GetWindowThreadProcessId(handle, out uint processId);
                var process = System.Diagnostics.Process.GetProcessById((int)processId);
                return process.ProcessName + ".exe";
            }
            catch
            {
                return string.Empty;
            }
        }

        private RecordingMode GetDefaultMode()
        {
            return modes.GetValueOrDefault("quick") ?? new RecordingMode
            {
                Id = "quick",
                Name = "Quick Note",
                ModelSize = "base"
            };
        }

        private void SaveSettings()
        {
            try
            {
                var settings = AppSettings.Instance;
                settings.ActiveRecordingMode = activeModeld;
                settings.Save();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save mode settings: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// P/Invoke declarations for window detection.
    /// </summary>
    internal static class Win32
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}
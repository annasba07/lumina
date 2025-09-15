using System;
using System.IO;
using System.Text.Json;

namespace SuperWhisperWPF
{
    public class AppSettings
    {
        // Model settings
        public string ModelFileName { get; set; } = "ggml-base.en.bin";
        public string[] ModelSearchPaths { get; set; }

        // Audio settings
        public int SampleRate { get; set; } = 16000;
        public int Channels { get; set; } = 1;
        public int BufferMilliseconds { get; set; } = 50;
        public int MaxRecordingSeconds { get; set; } = 300;

        // Hotkey settings
        public string HotkeyModifier { get; set; } = "Control";
        public string HotkeyKey { get; set; } = "Space";

        // UI settings
        public bool MinimizeToTray { get; set; } = true;
        public bool ShowBalloonNotifications { get; set; } = true;
        public int TranscriptionTimeout { get; set; } = 30000; // 30 seconds

        // Whisper settings
        public string Language { get; set; } = "en";
        public float Temperature { get; set; } = 0.0f;

        private static AppSettings _instance;
        private static readonly object _lock = new object();
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SuperWhisper",
            "settings.json"
        );

        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = Load();
                        }
                    }
                }
                return _instance;
            }
        }

        private AppSettings()
        {
            // Initialize default model search paths
            ModelSearchPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModelFileName),
                Path.Combine(Environment.CurrentDirectory, ModelFileName),
                Path.Combine(Environment.CurrentDirectory, "bin\\Release\\net8.0-windows", ModelFileName),
                Path.Combine(Environment.CurrentDirectory, "bin\\Debug\\net8.0-windows", ModelFileName),
                Path.Combine(Environment.CurrentDirectory, "..\\src\\bin\\Debug\\net8.0-windows", ModelFileName)
            };
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        WriteIndented = true
                    });
                    Logger.Info($"Loaded settings from {SettingsPath}");
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to load settings: {ex.Message}. Using defaults.");
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                });

                File.WriteAllText(SettingsPath, json);
                Logger.Info($"Saved settings to {SettingsPath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save settings: {ex.Message}", ex);
            }
        }

        public string FindModelPath()
        {
            foreach (var path in ModelSearchPaths)
            {
                if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);
                    Logger.Info($"Found model file: {path} (Size: {fileInfo.Length / (1024 * 1024):F1} MB)");
                    return path;
                }
            }

            Logger.Error($"Model file '{ModelFileName}' not found in any expected location");
            return null;
        }
    }
}
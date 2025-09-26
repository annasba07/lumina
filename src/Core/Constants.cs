using System;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// Centralized constants for the entire application.
    /// Groups all magic numbers, strings, and configuration values in one place.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Audio recording and processing constants
        /// </summary>
        public static class Audio
        {
            public const int SAMPLE_RATE = 16000;
            public const int CHANNELS = 1;
            public const int BITS_PER_SAMPLE = 16;
            public const int BUFFER_MILLISECONDS = 50;
            public const int MAX_RECORDING_SECONDS = 7200; // 2 hours
            public const int WARNING_SECONDS = 300; // 5 minutes warning
            public const int BYTES_PER_SECOND = SAMPLE_RATE * CHANNELS * (BITS_PER_SAMPLE / 8);
        }

        /// <summary>
        /// UI timing and animation constants
        /// </summary>
        public static class UI
        {
            public const int TYPING_TIMER_DELAY_MS = 500;
            public const int BALLOON_TIP_TIMEOUT_MS = 2000;
            public const int OVERLAY_SUCCESS_DURATION_MS = 2000;
            public const int OVERLAY_ERROR_DURATION_MS = 3000;
            public const int COPY_FEEDBACK_DURATION_MS = 1500;
            public const int TRAY_ICON_SIZE = 16;
            public const int RECORDING_TIMER_INTERVAL_MS = 100;
            public const double WINDOW_CORNER_RADIUS = 12.0;
            public const double BUTTON_CORNER_RADIUS = 18.0;
            public const double PILL_CORNER_RADIUS = 20.0;
            public const int TITLE_BAR_HEIGHT = 48;
        }

        /// <summary>
        /// Window dimensions
        /// </summary>
        public static class Window
        {
            public const int DEFAULT_WIDTH = 440;
            public const int DEFAULT_HEIGHT = 600;
            public const int MIN_WIDTH = 380;
            public const int MIN_HEIGHT = 500;
        }

        /// <summary>
        /// Application metadata
        /// </summary>
        public static class App
        {
            public const string NAME = "Lumina";
            public const string VERSION = "1.0.0";
            public const string AUTHOR = "annasba07";
            public const string GITHUB_URL = "https://github.com/annasba07/lumina";
            public const string UPDATE_URL = "https://github.com/annasba07/lumina/releases/latest/download/";
            public const string LOG_FOLDER_NAME = "SuperWhisper";
            public const string SETTINGS_FOLDER_NAME = "Lumina";
        }

        /// <summary>
        /// File names and extensions
        /// </summary>
        public static class Files
        {
            public const string MODEL_FILE_NAME = "ggml-base.en.bin";
            public const string ICON_FILE_NAME = "lumina-icon.ico";
            public const string LOG_FILE_PREFIX = "superwhisper_wpf_";
            public const string LOG_FILE_EXTENSION = ".log";
            public const string SETTINGS_FILE_NAME = "settings.json";
        }

        /// <summary>
        /// Whisper model configuration
        /// </summary>
        public static class Whisper
        {
            public const string DEFAULT_LANGUAGE = "en";
            public const float DEFAULT_TEMPERATURE = 0.0f;
            public const int MODEL_SIZE_MB = 141;
        }

        /// <summary>
        /// Status messages
        /// </summary>
        public static class Status
        {
            public const string READY = "Ready";
            public const string RECORDING = "Recording";
            public const string PROCESSING = "Processing";
            public const string TRANSCRIBING = "Transcribing";
            public const string ERROR = "Error";
            public const string FAILED = "Failed";
            public const string INITIALIZING = "Initializing";
        }

        /// <summary>
        /// Toast messages
        /// </summary>
        public static class Toast
        {
            public const string RECORDING_STARTED = "Recording";
            public const string PROCESSING_AUDIO = "Processing";
            public const string TRANSCRIPTION_COMPLETE = "Transcription complete";
            public const string COPIED_TO_CLIPBOARD = "Copied to clipboard";
            public const string TEXT_CLEARED = "Text cleared";
            public const string ERROR_OCCURRED = "An error occurred";
            public const string APPROACHING_LIMIT = "Approaching recording limit";
        }

        /// <summary>
        /// Icons (Segoe MDL2 Assets Unicode points)
        /// </summary>
        public static class Icons
        {
            public const string MINIMIZE = "\uE921";
            public const string CLOSE = "\uE8BB";
            public const string COPY = "\uE8C8";
            public const string CLEAR = "\uE74D";
            public const string MICROPHONE = "\uE720";
            public const string SETTINGS = "\uE713";
            public const string SAVE = "\uE74E";
            public const string EXPORT = "\uEDE1";
        }

        /// <summary>
        /// Colors (as hex strings for easy reference)
        /// </summary>
        public static class Colors
        {
            public const string PRIMARY_BLUE = "#3B82F6";
            public const string PRIMARY_BLUE_DARK = "#2563EB";
            public const string BACKGROUND_WHITE = "#FFFFFF";
            public const string BACKGROUND_LIGHT = "#F9FAFB";
            public const string BACKGROUND_GRAY = "#F3F4F6";
            public const string BORDER_LIGHT = "#E5E7EB";
            public const string TEXT_PRIMARY = "#111827";
            public const string TEXT_SECONDARY = "#6B7280";
            public const string TEXT_TERTIARY = "#374151";
            public const string SUCCESS_GREEN = "#10B981";
            public const string SUCCESS_BG = "#F0FDF4";
            public const string WARNING_ORANGE = "#F59E0B";
            public const string WARNING_BG = "#FEF3C7";
            public const string ERROR_RED = "#EF4444";
            public const string TOAST_BG = "#1F2937";
        }

        /// <summary>
        /// Regex patterns for data sanitization
        /// </summary>
        public static class Patterns
        {
            public const string EMAIL_PATTERN = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b";
            public const string PHONE_PATTERN = @"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b";
            public const string CREDIT_CARD_PATTERN = @"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b";
            public const string SSN_PATTERN = @"\b\d{3}-\d{2}-\d{4}\b";
        }

        /// <summary>
        /// Keyboard shortcuts
        /// </summary>
        public static class Hotkeys
        {
            public const string RECORD_TOGGLE = "Ctrl+Space";
            public const string COPY = "Ctrl+C";
            public const string CLEAR = "Ctrl+L";
            public const string SETTINGS = "Ctrl+,";
            public const string EXPORT = "Ctrl+E";
        }
    }
}
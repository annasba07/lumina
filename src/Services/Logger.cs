using System;
using System.IO;
using System.Threading;
using SuperWhisperWPF.Security;

namespace SuperWhisperWPF
{
    public static class Logger
    {
        private static readonly object lockObject = new object();
        private static readonly string logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SuperWhisper", "logs", $"superwhisper_wpf_{DateTime.Now:yyyy-MM-dd}.log");
        
        static Logger()
        {
            try
            {
                var logDir = Path.GetDirectoryName(logFilePath);
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                
                // Write startup header
                WriteToFile($"\n=== SuperWhisper WPF Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logger initialization failed: {ex.Message}");
            }
        }
        
        public static void Info(string message)
        {
            Log("INFO", message);
        }
        
        public static void Warning(string message)
        {
            Log("WARN", message);
        }
        
        public static void Error(string message, Exception ex = null)
        {
            var fullMessage = ex != null ? $"{message}: {ex}" : message;
            Log("ERROR", fullMessage);
        }
        
        public static void Debug(string message)
        {
            Log("DEBUG", message);
        }
        
        private static void Log(string level, string message)
        {
            // Sanitize message to remove sensitive information
            var sanitizedMessage = DataProtection.SanitizeForLogging(message);

            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var threadId = Thread.CurrentThread.ManagedThreadId;
            var logEntry = $"[{timestamp}] [{level}] [T{threadId}] {sanitizedMessage}";

            // Write to console
            Console.WriteLine(logEntry);

            // Write to file
            WriteToFile(logEntry);
        }
        
        private static void WriteToFile(string message)
        {
            lock (lockObject)
            {
                try
                {
                    File.AppendAllText(logFilePath, message + Environment.NewLine);
                }
                catch
                {
                    // Silently fail to avoid recursive logging issues
                }
            }
        }
        
        public static void LogSystemInfo()
        {
            Info("=== System Information ===");
            Info($"OS: {Environment.OSVersion}");
            Info($"Architecture: {Environment.Is64BitProcess} bit process on {Environment.Is64BitOperatingSystem} bit OS");
            Info($"Working Directory: {Environment.CurrentDirectory}");
            Info($".NET Version: {Environment.Version}");
            Info($"Log File: {logFilePath}");
        }
        
        public static void ShowLogLocation()
        {
            Info($"Log file location: {logFilePath}");
            try
            {
                // Try to open log directory in explorer
                var logDir = Path.GetDirectoryName(logFilePath);
                System.Diagnostics.Process.Start("explorer.exe", logDir);
            }
            catch (Exception ex)
            {
                Warning($"Could not open log directory: {ex.Message}");
            }
        }
    }
}
using System;
using System.IO;
using System.Threading;

namespace SuperWhisperWindows
{
    public static class Logger
    {
        private static readonly object lockObject = new object();
        private static readonly string logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SuperWhisper", "logs", $"superwhisper_{DateTime.Now:yyyy-MM-dd}.log");
        
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
                WriteToFile($"\n=== SuperWhisper Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
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
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var threadId = Thread.CurrentThread.ManagedThreadId;
            var logEntry = $"[{timestamp}] [{level}] [T{threadId}] {message}";
            
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
            
            // Check critical files
            var outputPath = Path.Combine(Environment.CurrentDirectory, "bin", "Release", "net8.0-windows");
            var whisperDll = Path.Combine(outputPath, "whisper.dll");
            var modelFile = Path.Combine(outputPath, "ggml-base.en.bin");
            
            Info($"Output Path: {outputPath}");
            Info($"whisper.dll exists: {File.Exists(whisperDll)} (Size: {GetFileSize(whisperDll)})");
            Info($"Model file exists: {File.Exists(modelFile)} (Size: {GetFileSize(modelFile)})");
        }
        
        private static string GetFileSize(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var size = new FileInfo(path).Length;
                    if (size > 1024 * 1024)
                        return $"{size / (1024 * 1024):F1} MB";
                    else if (size > 1024)
                        return $"{size / 1024:F1} KB";
                    else
                        return $"{size} bytes";
                }
                return "N/A";
            }
            catch
            {
                return "Error";
            }
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
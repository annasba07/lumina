using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using SuperWhisperWPF.Security;
using SuperWhisperWPF.Core;

namespace SuperWhisperWPF
{
    public static class Logger
    {
        private static readonly ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
        private static readonly SemaphoreSlim logSemaphore = new SemaphoreSlim(1, 1);
        private static readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private static readonly Task logWriterTask;
        private static readonly string logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Constants.App.LOG_FOLDER_NAME, "logs", $"{Constants.Files.LOG_FILE_PREFIX}{DateTime.Now:yyyy-MM-dd}{Constants.Files.LOG_FILE_EXTENSION}");
        
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
                logQueue.Enqueue($"\n=== {Constants.App.NAME} Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");

                // Start background log writer task
                logWriterTask = Task.Run(ProcessLogQueueAsync);
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

            // Write to console immediately
            Console.WriteLine(logEntry);

            // Queue for async file writing
            logQueue.Enqueue(logEntry);
        }
        
        private static async Task ProcessLogQueueAsync()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (logQueue.TryDequeue(out string message))
                    {
                        await WriteToFileAsync(message);
                    }
                    else
                    {
                        // Wait a bit if queue is empty
                        await Task.Delay(100, cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Continue processing even if individual writes fail
                }
            }

            // Flush remaining messages on shutdown
            while (logQueue.TryDequeue(out string message))
            {
                try
                {
                    await WriteToFileAsync(message);
                }
                catch { }
            }
        }

        private static async Task WriteToFileAsync(string message)
        {
            await logSemaphore.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(logFilePath, message + Environment.NewLine);
            }
            catch
            {
                // Silently fail to avoid recursive logging issues
            }
            finally
            {
                logSemaphore.Release();
            }
        }

        public static void Shutdown()
        {
            Info("Logger shutting down...");
            cancellationTokenSource.Cancel();

            // Wait for log writer to finish (max 2 seconds)
            try
            {
                logWriterTask?.Wait(2000);
            }
            catch { }

            logSemaphore?.Dispose();
            cancellationTokenSource?.Dispose();
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
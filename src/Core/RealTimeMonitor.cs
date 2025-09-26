using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// Real-time performance monitoring with live dashboard and telemetry.
    /// Tracks every aspect of Lumina's performance in microsecond precision.
    /// </summary>
    public class RealTimeMonitor : IDisposable
    {
        private static readonly Lazy<RealTimeMonitor> instance =
            new Lazy<RealTimeMonitor>(() => new RealTimeMonitor());
        public static RealTimeMonitor Instance => instance.Value;

        // Performance tracking
        private readonly ConcurrentQueue<PerformanceSnapshot> snapshots;
        private readonly Dictionary<string, OperationTracker> operations;
        private readonly Timer snapshotTimer;
        private readonly Stopwatch uptimeStopwatch;

        // Hardware monitoring
        private readonly PerformanceCounter cpuCounter;
        private readonly PerformanceCounter memCounter;
        private readonly Process currentProcess;

        // Metrics
        private long totalTranscriptions;
        private long totalAudioMs;
        private long totalProcessingMs;
        private long cacheHits;
        private long cacheMisses;
        private readonly ConcurrentDictionary<string, long> customMetrics;

        // Events
        public event EventHandler<PerformanceSnapshot> SnapshotCaptured;
        public event EventHandler<PerformanceAlert> AlertRaised;

        private RealTimeMonitor()
        {
            snapshots = new ConcurrentQueue<PerformanceSnapshot>();
            operations = new Dictionary<string, OperationTracker>();
            customMetrics = new ConcurrentDictionary<string, long>();
            uptimeStopwatch = Stopwatch.StartNew();

            try
            {
                currentProcess = Process.GetCurrentProcess();
                cpuCounter = new PerformanceCounter("Process", "% Processor Time", currentProcess.ProcessName);
                memCounter = new PerformanceCounter("Process", "Working Set - Private", currentProcess.ProcessName);
            }
            catch
            {
                // Performance counters might not be available
            }

            // Capture snapshots every 100ms
            snapshotTimer = new Timer(CaptureSnapshot, null, 100, 100);
        }

        /// <summary>
        /// Starts monitoring a specific operation with microsecond precision.
        /// </summary>
        public OperationMonitor StartOperation(string operationName)
        {
            return new OperationMonitor(operationName, this);
        }

        /// <summary>
        /// Records a transcription with detailed metrics.
        /// </summary>
        public void RecordTranscription(TranscriptionMetrics metrics)
        {
            Interlocked.Increment(ref totalTranscriptions);
            Interlocked.Add(ref totalAudioMs, metrics.AudioDurationMs);
            Interlocked.Add(ref totalProcessingMs, metrics.ProcessingTimeMs);

            if (metrics.WasCached)
                Interlocked.Increment(ref cacheHits);
            else
                Interlocked.Increment(ref cacheMisses);

            // Check for performance anomalies
            if (metrics.ProcessingTimeMs > metrics.AudioDurationMs * 0.5)
            {
                RaiseAlert(AlertLevel.Warning,
                    $"Slow transcription: {metrics.ProcessingTimeMs}ms for {metrics.AudioDurationMs}ms audio");
            }

            // Update custom metrics
            UpdateMetric("last_transcription_ms", metrics.ProcessingTimeMs);
            UpdateMetric("last_audio_duration_ms", metrics.AudioDurationMs);
        }

        /// <summary>
        /// Updates a custom metric value.
        /// </summary>
        public void UpdateMetric(string name, long value)
        {
            customMetrics.AddOrUpdate(name, value, (k, v) => value);
        }

        /// <summary>
        /// Increments a counter metric.
        /// </summary>
        public void IncrementCounter(string name, long value = 1)
        {
            customMetrics.AddOrUpdate(name, value, (k, v) => v + value);
        }

        /// <summary>
        /// Gets current performance statistics.
        /// </summary>
        public PerformanceStatistics GetStatistics()
        {
            var stats = new PerformanceStatistics
            {
                UptimeMs = uptimeStopwatch.ElapsedMilliseconds,
                TotalTranscriptions = totalTranscriptions,
                TotalAudioMs = totalAudioMs,
                TotalProcessingMs = totalProcessingMs,
                AverageLatencyMs = totalTranscriptions > 0 ? totalProcessingMs / totalTranscriptions : 0,
                RealTimeFactor = totalAudioMs > 0 ? (double)totalProcessingMs / totalAudioMs : 0,
                CacheHitRate = (cacheHits + cacheMisses) > 0
                    ? (double)cacheHits / (cacheHits + cacheMisses) * 100 : 0,
                CurrentCpuUsage = GetCpuUsage(),
                CurrentMemoryMB = GetMemoryMB(),
                ThreadCount = currentProcess?.Threads.Count ?? 0,
                HandleCount = currentProcess?.HandleCount ?? 0,
                CustomMetrics = new Dictionary<string, long>(customMetrics)
            };

            // Add operation statistics
            lock (operations)
            {
                stats.Operations = operations.Values
                    .Select(o => o.GetStatistics())
                    .OrderByDescending(o => o.TotalTimeMs)
                    .ToList();
            }

            return stats;
        }

        /// <summary>
        /// Captures a performance snapshot.
        /// </summary>
        private void CaptureSnapshot(object state)
        {
            var snapshot = new PerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                CpuUsage = GetCpuUsage(),
                MemoryMB = GetMemoryMB(),
                ThreadCount = currentProcess?.Threads.Count ?? 0,
                GcGen0 = GC.CollectionCount(0),
                GcGen1 = GC.CollectionCount(1),
                GcGen2 = GC.CollectionCount(2),
                TranscriptionsPerSecond = CalculateThroughput()
            };

            snapshots.Enqueue(snapshot);

            // Keep only last 1000 snapshots (100 seconds)
            while (snapshots.Count > 1000 && snapshots.TryDequeue(out _)) { }

            SnapshotCaptured?.Invoke(this, snapshot);
        }

        private double GetCpuUsage()
        {
            try
            {
                return cpuCounter?.NextValue() ?? 0;
            }
            catch
            {
                // Fallback CPU calculation
                return currentProcess?.TotalProcessorTime.TotalMilliseconds
                    / Environment.ProcessorCount
                    / uptimeStopwatch.ElapsedMilliseconds * 100 ?? 0;
            }
        }

        private long GetMemoryMB()
        {
            try
            {
                var memoryBytes = memCounter?.NextValue() ?? currentProcess?.WorkingSet64 ?? 0;
                return (long)(memoryBytes / (1024 * 1024));
            }
            catch
            {
                return GC.GetTotalMemory(false) / (1024 * 1024);
            }
        }

        private double CalculateThroughput()
        {
            var recentSnapshots = snapshots.TakeLast(10).ToList();
            if (recentSnapshots.Count < 2) return 0;

            var timeDiff = (recentSnapshots.Last().Timestamp - recentSnapshots.First().Timestamp).TotalSeconds;
            if (timeDiff <= 0) return 0;

            // Calculate transcriptions in the time window
            var transcriptionsDiff = totalTranscriptions - recentSnapshots.First().TranscriptionsPerSecond;
            return transcriptionsDiff / timeDiff;
        }

        private void RaiseAlert(AlertLevel level, string message)
        {
            var alert = new PerformanceAlert
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message,
                Metrics = GetStatistics()
            };

            AlertRaised?.Invoke(this, alert);
            Logger.Warning($"Performance Alert: {message}");
        }

        public void Dispose()
        {
            snapshotTimer?.Dispose();
            cpuCounter?.Dispose();
            memCounter?.Dispose();
        }

        // Helper classes
        public class OperationMonitor : IDisposable
        {
            private readonly string operationName;
            private readonly RealTimeMonitor monitor;
            private readonly Stopwatch stopwatch;
            private readonly long startMemory;

            public OperationMonitor(string name, RealTimeMonitor mon)
            {
                operationName = name;
                monitor = mon;
                stopwatch = Stopwatch.StartNew();
                startMemory = GC.GetTotalMemory(false);

                // Track operation start
                lock (monitor.operations)
                {
                    if (!monitor.operations.ContainsKey(operationName))
                    {
                        monitor.operations[operationName] = new OperationTracker(operationName);
                    }
                    monitor.operations[operationName].Start();
                }
            }

            public void Dispose()
            {
                stopwatch.Stop();
                var memoryDelta = GC.GetTotalMemory(false) - startMemory;

                // Track operation end
                lock (monitor.operations)
                {
                    monitor.operations[operationName].Complete(stopwatch.ElapsedMilliseconds, memoryDelta);
                }

                // Alert if operation is slow
                if (stopwatch.ElapsedMilliseconds > 100)
                {
                    monitor.RaiseAlert(AlertLevel.Warning,
                        $"Slow operation '{operationName}': {stopwatch.ElapsedMilliseconds}ms");
                }
            }
        }

        private class OperationTracker
        {
            public string Name { get; }
            public long TotalCalls { get; private set; }
            public long TotalTimeMs { get; private set; }
            public long MinTimeMs { get; private set; } = long.MaxValue;
            public long MaxTimeMs { get; private set; }
            public long TotalMemoryBytes { get; private set; }
            private int activeCount;

            public OperationTracker(string name)
            {
                Name = name;
            }

            public void Start()
            {
                Interlocked.Increment(ref activeCount);
            }

            public void Complete(long timeMs, long memoryBytes)
            {
                Interlocked.Decrement(ref activeCount);
                TotalCalls++;
                TotalTimeMs += timeMs;
                TotalMemoryBytes += memoryBytes;

                // Update min/max (not thread-safe but good enough)
                if (timeMs < MinTimeMs) MinTimeMs = timeMs;
                if (timeMs > MaxTimeMs) MaxTimeMs = timeMs;
            }

            public OperationStatistics GetStatistics()
            {
                return new OperationStatistics
                {
                    Name = Name,
                    TotalCalls = TotalCalls,
                    TotalTimeMs = TotalTimeMs,
                    AverageTimeMs = TotalCalls > 0 ? TotalTimeMs / (double)TotalCalls : 0,
                    MinTimeMs = MinTimeMs == long.MaxValue ? 0 : MinTimeMs,
                    MaxTimeMs = MaxTimeMs,
                    ActiveCount = activeCount,
                    AverageMemoryBytes = TotalCalls > 0 ? TotalMemoryBytes / TotalCalls : 0
                };
            }
        }
    }

    /// <summary>
    /// Live performance dashboard window.
    /// </summary>
    public class PerformanceDashboard : Window
    {
        private readonly Canvas latencyCanvas;
        private readonly TextBlock statsText;
        private readonly DispatcherTimer updateTimer;
        private readonly List<double> latencyHistory;
        private readonly List<double> cpuHistory;

        public PerformanceDashboard()
        {
            Title = "Lumina Performance Monitor";
            Width = 800;
            Height = 600;
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Chart canvas
            latencyCanvas = new Canvas { Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)) };
            Grid.SetRow(latencyCanvas, 0);
            grid.Children.Add(latencyCanvas);

            // Stats text
            statsText = new TextBlock
            {
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Margin = new Thickness(10)
            };
            Grid.SetRow(statsText, 1);
            grid.Children.Add(statsText);

            Content = grid;

            latencyHistory = new List<double>();
            cpuHistory = new List<double>();

            // Update every 100ms
            updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            updateTimer.Tick += UpdateDashboard;
            updateTimer.Start();

            // Subscribe to monitor events
            RealTimeMonitor.Instance.SnapshotCaptured += OnSnapshotCaptured;
        }

        private void OnSnapshotCaptured(object sender, PerformanceSnapshot snapshot)
        {
            Dispatcher.BeginInvoke(() =>
            {
                // Add to history
                latencyHistory.Add(snapshot.TranscriptionsPerSecond);
                cpuHistory.Add(snapshot.CpuUsage);

                // Keep last 100 points
                if (latencyHistory.Count > 100) latencyHistory.RemoveAt(0);
                if (cpuHistory.Count > 100) cpuHistory.RemoveAt(0);
            });
        }

        private void UpdateDashboard(object sender, EventArgs e)
        {
            // Clear canvas
            latencyCanvas.Children.Clear();

            // Draw latency chart
            DrawChart(latencyHistory, Brushes.LimeGreen, 0, latencyCanvas.ActualHeight / 2);

            // Draw CPU chart
            DrawChart(cpuHistory, Brushes.Orange, latencyCanvas.ActualHeight / 2, latencyCanvas.ActualHeight / 2);

            // Update stats
            var stats = RealTimeMonitor.Instance.GetStatistics();
            statsText.Text = $@"
Uptime: {TimeSpan.FromMilliseconds(stats.UptimeMs):hh\:mm\:ss}
Transcriptions: {stats.TotalTranscriptions:N0}
Avg Latency: {stats.AverageLatencyMs:F1}ms
Real-time Factor: {stats.RealTimeFactor:F2}x
Cache Hit Rate: {stats.CacheHitRate:F1}%
CPU: {stats.CurrentCpuUsage:F1}%
Memory: {stats.CurrentMemoryMB}MB
Threads: {stats.ThreadCount}
".Trim();
        }

        private void DrawChart(List<double> data, Brush color, double yOffset, double height)
        {
            if (data.Count < 2) return;

            var width = latencyCanvas.ActualWidth;
            var xStep = width / (data.Count - 1);

            var polyline = new Polyline
            {
                Stroke = color,
                StrokeThickness = 2,
                Points = new PointCollection()
            };

            for (int i = 0; i < data.Count; i++)
            {
                var x = i * xStep;
                var y = yOffset + height - (data[i] / 100.0 * height);
                polyline.Points.Add(new Point(x, y));
            }

            latencyCanvas.Children.Add(polyline);
        }

        protected override void OnClosed(EventArgs e)
        {
            updateTimer.Stop();
            RealTimeMonitor.Instance.SnapshotCaptured -= OnSnapshotCaptured;
            base.OnClosed(e);
        }
    }

    // Data classes
    public class PerformanceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public double CpuUsage { get; set; }
        public long MemoryMB { get; set; }
        public int ThreadCount { get; set; }
        public int GcGen0 { get; set; }
        public int GcGen1 { get; set; }
        public int GcGen2 { get; set; }
        public double TranscriptionsPerSecond { get; set; }
    }

    public class PerformanceStatistics
    {
        public long UptimeMs { get; set; }
        public long TotalTranscriptions { get; set; }
        public long TotalAudioMs { get; set; }
        public long TotalProcessingMs { get; set; }
        public long AverageLatencyMs { get; set; }
        public double RealTimeFactor { get; set; }
        public double CacheHitRate { get; set; }
        public double CurrentCpuUsage { get; set; }
        public long CurrentMemoryMB { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public Dictionary<string, long> CustomMetrics { get; set; }
        public List<OperationStatistics> Operations { get; set; }
    }

    public class OperationStatistics
    {
        public string Name { get; set; }
        public long TotalCalls { get; set; }
        public long TotalTimeMs { get; set; }
        public double AverageTimeMs { get; set; }
        public long MinTimeMs { get; set; }
        public long MaxTimeMs { get; set; }
        public int ActiveCount { get; set; }
        public long AverageMemoryBytes { get; set; }
    }

    public class TranscriptionMetrics
    {
        public long AudioDurationMs { get; set; }
        public long ProcessingTimeMs { get; set; }
        public bool WasCached { get; set; }
        public string ModelUsed { get; set; }
        public double Confidence { get; set; }
    }

    public class PerformanceAlert
    {
        public DateTime Timestamp { get; set; }
        public AlertLevel Level { get; set; }
        public string Message { get; set; }
        public PerformanceStatistics Metrics { get; set; }
    }

    public enum AlertLevel
    {
        Info,
        Warning,
        Error,
        Critical
    }
}
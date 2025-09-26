using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// Monitors and reports performance metrics for Lumina.
    /// Tracks latency, throughput, resource usage, and optimization effectiveness.
    /// </summary>
    public class PerformanceMonitor
    {
        private static readonly Lazy<PerformanceMonitor> instance =
            new Lazy<PerformanceMonitor>(() => new PerformanceMonitor());
        public static PerformanceMonitor Instance => instance.Value;

        private readonly List<PerformanceMetric> metrics = new List<PerformanceMetric>();
        private readonly Stopwatch sessionStopwatch = new Stopwatch();
        private readonly PerformanceCounter cpuCounter;
        private readonly PerformanceCounter memCounter;

        // Performance tracking
        private int totalTranscriptions;
        private long totalProcessingTimeMs;
        private long totalAudioDurationMs;

        // Events
        public event EventHandler<PerformanceReport> PerformanceReported;

        private PerformanceMonitor()
        {
            try
            {
                cpuCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
                memCounter = new PerformanceCounter("Process", "Working Set - Private", Process.GetCurrentProcess().ProcessName);
            }
            catch
            {
                // Performance counters may not be available
            }

            sessionStopwatch.Start();
        }

        /// <summary>
        /// Records a transcription performance metric.
        /// </summary>
        public void RecordTranscription(TranscriptionMetric metric)
        {
            metrics.Add(metric);
            totalTranscriptions++;
            totalProcessingTimeMs += metric.ProcessingTimeMs;
            totalAudioDurationMs += metric.AudioDurationMs;

            // Keep only last 100 metrics to avoid memory growth
            if (metrics.Count > 100)
            {
                metrics.RemoveAt(0);
            }

            // Generate report if significant event
            if (totalTranscriptions % 10 == 0)
            {
                GenerateReport();
            }
        }

        /// <summary>
        /// Starts a new transcription timing session.
        /// </summary>
        public TranscriptionTimer StartTranscription(int audioLengthMs)
        {
            return new TranscriptionTimer(audioLengthMs, this);
        }

        /// <summary>
        /// Gets current performance statistics.
        /// </summary>
        public PerformanceStats GetStats()
        {
            var recentMetrics = metrics.TakeLast(10).OfType<TranscriptionMetric>().ToList();

            return new PerformanceStats
            {
                TotalTranscriptions = totalTranscriptions,
                AverageLatencyMs = recentMetrics.Any() ? (long)recentMetrics.Average(m => m.ProcessingTimeMs) : 0,
                MinLatencyMs = recentMetrics.Any() ? recentMetrics.Min(m => m.ProcessingTimeMs) : 0,
                MaxLatencyMs = recentMetrics.Any() ? recentMetrics.Max(m => m.ProcessingTimeMs) : 0,
                RealTimeFactor = CalculateRealTimeFactor(),
                CpuUsage = GetCpuUsage(),
                MemoryUsageMB = GetMemoryUsageMB(),
                CacheHitRate = CalculateCacheHitRate(),
                SessionDuration = sessionStopwatch.Elapsed,
                IsGpuEnabled = OptimizedWhisperEngine.Instance.IsGpuEnabled
            };
        }

        /// <summary>
        /// Runs a comprehensive benchmark of the system.
        /// </summary>
        public async Task<BenchmarkResults> RunBenchmarkAsync()
        {
            var results = new BenchmarkResults();
            var testAudio = GenerateTestAudio();

            Logger.Info("Starting performance benchmark...");

            // Test 1: Cold start
            var coldStartTimer = Stopwatch.StartNew();
            var engine = OptimizedWhisperEngine.Instance;
            await engine.InitializeAsync();
            results.ColdStartMs = coldStartTimer.ElapsedMilliseconds;

            // Test 2: Warm transcription
            var warmTimer = Stopwatch.StartNew();
            var result1 = await engine.TranscribeAsync(testAudio.Short);
            results.WarmTranscriptionMs = warmTimer.ElapsedMilliseconds;

            // Test 3: Cache performance
            var cacheTimer = Stopwatch.StartNew();
            var result2 = await engine.TranscribeAsync(testAudio.Short); // Same audio
            results.CacheHitMs = cacheTimer.ElapsedMilliseconds;

            // Test 4: Streaming performance
            var streamTimer = Stopwatch.StartNew();
            var streamResult = await engine.TranscribeStreamingAsync(testAudio.Long);
            results.StreamingMs = streamTimer.ElapsedMilliseconds;

            // Test 5: Quick transcription
            var quickTimer = Stopwatch.StartNew();
            var quickResult = await engine.QuickTranscribeAsync(testAudio.Short);
            results.QuickTranscriptionMs = quickTimer.ElapsedMilliseconds;

            // Test 6: Concurrent load
            var concurrentTimer = Stopwatch.StartNew();
            var tasks = Enumerable.Range(0, 5).Select(_ =>
                engine.TranscribeAsync(testAudio.Short)
            );
            await Task.WhenAll(tasks);
            results.ConcurrentMs = concurrentTimer.ElapsedMilliseconds / 5;

            // Memory test
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            results.MemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024);

            // GPU test
            results.IsGpuEnabled = engine.IsGpuEnabled;

            Logger.Info($"Benchmark complete: Cold={results.ColdStartMs}ms, Warm={results.WarmTranscriptionMs}ms, Cache={results.CacheHitMs}ms");

            return results;
        }

        /// <summary>
        /// Generates a performance report.
        /// </summary>
        public PerformanceReport GenerateReport()
        {
            var stats = GetStats();
            var report = new PerformanceReport
            {
                Timestamp = DateTime.UtcNow,
                Stats = stats,
                Recommendations = GenerateRecommendations(stats),
                HealthScore = CalculateHealthScore(stats)
            };

            PerformanceReported?.Invoke(this, report);
            return report;
        }

        #region Private Methods

        private double CalculateRealTimeFactor()
        {
            if (totalAudioDurationMs == 0) return 0;
            return (double)totalProcessingTimeMs / totalAudioDurationMs;
        }

        private double GetCpuUsage()
        {
            try
            {
                return cpuCounter?.NextValue() ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private long GetMemoryUsageMB()
        {
            try
            {
                return (long)(memCounter?.NextValue() ?? 0) / (1024 * 1024);
            }
            catch
            {
                return GC.GetTotalMemory(false) / (1024 * 1024);
            }
        }

        private double CalculateCacheHitRate()
        {
            var engine = OptimizedWhisperEngine.Instance;
            var total = engine.CacheHits + engine.CacheMisses;
            if (total == 0) return 0;
            return (double)engine.CacheHits / total * 100;
        }

        private List<string> GenerateRecommendations(PerformanceStats stats)
        {
            var recommendations = new List<string>();

            // Latency recommendations
            if (stats.AverageLatencyMs > 500)
            {
                if (!stats.IsGpuEnabled)
                {
                    recommendations.Add("Enable GPU acceleration for 5-10x speed improvement");
                }
                recommendations.Add("Consider using the tiny model for faster processing");
            }

            // Memory recommendations
            if (stats.MemoryUsageMB > 500)
            {
                recommendations.Add("Memory usage is high. Consider using quantized models");
            }

            // Cache recommendations
            if (stats.CacheHitRate < 20 && stats.TotalTranscriptions > 50)
            {
                recommendations.Add("Low cache hit rate. You might benefit from phrase templates");
            }

            // Real-time factor
            if (stats.RealTimeFactor > 0.3)
            {
                recommendations.Add("Processing is slower than 3x real-time. Consider optimization");
            }

            return recommendations;
        }

        private int CalculateHealthScore(PerformanceStats stats)
        {
            var score = 100;

            // Deduct for high latency
            if (stats.AverageLatencyMs > 1000) score -= 30;
            else if (stats.AverageLatencyMs > 500) score -= 15;

            // Deduct for high memory
            if (stats.MemoryUsageMB > 800) score -= 20;
            else if (stats.MemoryUsageMB > 500) score -= 10;

            // Deduct for high CPU
            if (stats.CpuUsage > 80) score -= 20;
            else if (stats.CpuUsage > 50) score -= 10;

            // Bonus for GPU
            if (stats.IsGpuEnabled) score += 10;

            // Bonus for good cache hit rate
            if (stats.CacheHitRate > 50) score += 10;

            return Math.Max(0, Math.Min(100, score));
        }

        private TestAudio GenerateTestAudio()
        {
            // Generate test audio samples
            return new TestAudio
            {
                Short = new byte[16000 * 2 * 1], // 1 second
                Long = new byte[16000 * 2 * 10]  // 10 seconds
            };
        }

        #endregion

        // Helper classes
        private class TestAudio
        {
            public byte[] Short { get; set; }
            public byte[] Long { get; set; }
        }
    }

    /// <summary>
    /// Timer for measuring transcription performance.
    /// </summary>
    public class TranscriptionTimer : IDisposable
    {
        private readonly Stopwatch stopwatch;
        private readonly int audioLengthMs;
        private readonly PerformanceMonitor monitor;

        public TranscriptionTimer(int audioLengthMs, PerformanceMonitor monitor)
        {
            this.audioLengthMs = audioLengthMs;
            this.monitor = monitor;
            stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            stopwatch.Stop();
            monitor.RecordTranscription(new TranscriptionMetric
            {
                Timestamp = DateTime.UtcNow,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                AudioDurationMs = audioLengthMs,
                RealTimeFactor = (double)stopwatch.ElapsedMilliseconds / audioLengthMs
            });
        }
    }

    // Data classes
    public abstract class PerformanceMetric
    {
        public DateTime Timestamp { get; set; }
    }

    public class TranscriptionMetric : PerformanceMetric
    {
        public long ProcessingTimeMs { get; set; }
        public long AudioDurationMs { get; set; }
        public double RealTimeFactor { get; set; }
    }

    public class PerformanceStats
    {
        public int TotalTranscriptions { get; set; }
        public long AverageLatencyMs { get; set; }
        public long MinLatencyMs { get; set; }
        public long MaxLatencyMs { get; set; }
        public double RealTimeFactor { get; set; }
        public double CpuUsage { get; set; }
        public long MemoryUsageMB { get; set; }
        public double CacheHitRate { get; set; }
        public TimeSpan SessionDuration { get; set; }
        public bool IsGpuEnabled { get; set; }
    }

    public class PerformanceReport
    {
        public DateTime Timestamp { get; set; }
        public PerformanceStats Stats { get; set; }
        public List<string> Recommendations { get; set; }
        public int HealthScore { get; set; }
    }

    public class BenchmarkResults
    {
        public long ColdStartMs { get; set; }
        public long WarmTranscriptionMs { get; set; }
        public long CacheHitMs { get; set; }
        public long StreamingMs { get; set; }
        public long QuickTranscriptionMs { get; set; }
        public long ConcurrentMs { get; set; }
        public long MemoryUsageMB { get; set; }
        public bool IsGpuEnabled { get; set; }

        public override string ToString()
        {
            return $@"
=== Lumina Performance Benchmark Results ===
Cold Start: {ColdStartMs}ms
Warm Transcription: {WarmTranscriptionMs}ms
Cache Hit: {CacheHitMs}ms (Speedup: {WarmTranscriptionMs / (double)Math.Max(1, CacheHitMs):F1}x)
Streaming: {StreamingMs}ms
Quick Mode: {QuickTranscriptionMs}ms
Concurrent: {ConcurrentMs}ms avg
Memory Usage: {MemoryUsageMB}MB
GPU Enabled: {IsGpuEnabled}
============================================";
        }
    }
}
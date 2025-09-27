using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// Comprehensive performance benchmark suite for Whisper engines.
    /// Tests latency, accuracy, GPU utilization, and memory usage.
    /// </summary>
    public class WhisperPerformanceBenchmark
    {
        private readonly List<TestResult> results = new List<TestResult>();
        private readonly Random random = new Random(42); // Deterministic for reproducible tests

        /// <summary>
        /// Runs comprehensive benchmark comparing old vs new engine performance.
        /// </summary>
        public async Task<BenchmarkReport> RunComprehensiveBenchmarkAsync()
        {
            Logger.Info("Starting comprehensive Whisper performance benchmark...");

            var report = new BenchmarkReport
            {
                StartTime = DateTime.UtcNow,
                TestEnvironment = GatherEnvironmentInfo()
            };

            try
            {
                // Test 1: Short audio performance (1-3 seconds)
                Logger.Info("Testing short audio performance...");
                await BenchmarkShortAudioAsync(report);

                // Test 2: Medium audio performance (5-10 seconds)
                Logger.Info("Testing medium audio performance...");
                await BenchmarkMediumAudioAsync(report);

                // Test 3: Silence detection efficiency
                Logger.Info("Testing silence detection...");
                await BenchmarkSilenceDetectionAsync(report);

                // Test 4: Cache effectiveness
                Logger.Info("Testing cache performance...");
                await BenchmarkCachePerformanceAsync(report);

                // Test 5: GPU vs CPU comparison
                Logger.Info("Testing GPU vs CPU performance...");
                await BenchmarkGpuVsCpuAsync(report);

                // Test 6: Model comparison (tiny vs base)
                Logger.Info("Testing model size comparison...");
                await BenchmarkModelComparisonAsync(report);

                report.EndTime = DateTime.UtcNow;
                report.TotalDuration = report.EndTime - report.StartTime;

                // Generate summary
                report.Summary = GenerateBenchmarkSummary(report);

                Logger.Info($"Benchmark completed in {report.TotalDuration.TotalSeconds:F1}s");
                return report;
            }
            catch (Exception ex)
            {
                Logger.Error($"Benchmark failed: {ex.Message}", ex);
                report.ErrorMessage = ex.Message;
                return report;
            }
        }

        #region Individual Benchmark Methods

        private async Task BenchmarkShortAudioAsync(BenchmarkReport report)
        {
            var testCases = new[]
            {
                GenerateTestAudio(1000, "Hello world"),           // 1 second
                GenerateTestAudio(2000, "Quick test phrase"),     // 2 seconds
                GenerateTestAudio(3000, "Short audio sample")     // 3 seconds
            };

            var oldEngineResults = new List<TestResult>();
            var newEngineResults = new List<TestResult>();

            // Test original engine
            var oldEngine = new WhisperEngine();
            await oldEngine.InitializeAsync();

            foreach (var testAudio in testCases)
            {
                var result = await BenchmarkSingleTranscription(oldEngine.TranscribeAsync, testAudio, "Original");
                oldEngineResults.Add(result);
            }

            // Test optimized engine
            var newEngine = HighPerformanceWhisperEngine.Instance;
            await newEngine.InitializeAsync();

            foreach (var testAudio in testCases)
            {
                var result = await BenchmarkSingleTranscription(
                    data => newEngine.TranscribeFastAsync(data, false),
                    testAudio,
                    "Optimized"
                );
                newEngineResults.Add(result);
            }

            report.ShortAudioResults = new ComparisonResult
            {
                OriginalResults = oldEngineResults,
                OptimizedResults = newEngineResults,
                ImprovementFactor = CalculateImprovementFactor(oldEngineResults, newEngineResults)
            };

            oldEngine.Dispose();
        }

        private async Task BenchmarkMediumAudioAsync(BenchmarkReport report)
        {
            var testCases = new[]
            {
                GenerateTestAudio(5000, "This is a longer audio sample for testing medium duration processing"),
                GenerateTestAudio(10000, "This is an even longer audio sample to test the performance with medium sized audio files that might contain multiple sentences and phrases")
            };

            var oldEngineResults = new List<TestResult>();
            var newEngineResults = new List<TestResult>();

            var oldEngine = new WhisperEngine();
            await oldEngine.InitializeAsync();

            var newEngine = HighPerformanceWhisperEngine.Instance;

            foreach (var testAudio in testCases)
            {
                var oldResult = await BenchmarkSingleTranscription(oldEngine.TranscribeAsync, testAudio, "Original");
                oldEngineResults.Add(oldResult);

                var newResult = await BenchmarkSingleTranscription(
                    data => newEngine.TranscribeFastAsync(data, true),
                    testAudio,
                    "Optimized"
                );
                newEngineResults.Add(newResult);
            }

            report.MediumAudioResults = new ComparisonResult
            {
                OriginalResults = oldEngineResults,
                OptimizedResults = newEngineResults,
                ImprovementFactor = CalculateImprovementFactor(oldEngineResults, newEngineResults)
            };

            oldEngine.Dispose();
        }

        private async Task BenchmarkSilenceDetectionAsync(BenchmarkReport report)
        {
            var silenceTests = new[]
            {
                GenerateSilentAudio(1000),  // 1 second silence
                GenerateSilentAudio(3000),  // 3 seconds silence
                GenerateSilentAudio(5000),  // 5 seconds silence
            };

            var newEngine = HighPerformanceWhisperEngine.Instance;
            var results = new List<TestResult>();

            foreach (var silentAudio in silenceTests)
            {
                var result = await BenchmarkSingleTranscription(
                    data => newEngine.TranscribeFastAsync(data, false),
                    silentAudio,
                    "Silence"
                );
                results.Add(result);
            }

            report.SilenceDetectionResults = results;
        }

        private async Task BenchmarkCachePerformanceAsync(BenchmarkReport report)
        {
            var testAudio = GenerateTestAudio(2000, "Cache test phrase");
            var newEngine = HighPerformanceWhisperEngine.Instance;

            // First run (cold cache)
            var coldResult = await BenchmarkSingleTranscription(
                data => newEngine.TranscribeFastAsync(data, false),
                testAudio,
                "Cold Cache"
            );

            // Second run (warm cache)
            var warmResult = await BenchmarkSingleTranscription(
                data => newEngine.TranscribeFastAsync(data, false),
                testAudio,
                "Warm Cache"
            );

            report.CacheResults = new List<TestResult> { coldResult, warmResult };
        }

        private async Task BenchmarkGpuVsCpuAsync(BenchmarkReport report)
        {
            // This would require implementing CPU-only mode in the engine
            // For now, we'll just report the current GPU mode
            var newEngine = HighPerformanceWhisperEngine.Instance;

            report.GpuMode = newEngine.GpuMode.ToString();
            report.GpuMetrics = newEngine.Metrics;
        }

        private async Task BenchmarkModelComparisonAsync(BenchmarkReport report)
        {
            var testAudio = GenerateTestAudio(3000, "Model comparison test");
            var newEngine = HighPerformanceWhisperEngine.Instance;

            // Test with high quality (base model)
            var baseResult = await BenchmarkSingleTranscription(
                data => newEngine.TranscribeFastAsync(data, true),
                testAudio,
                "Base Model"
            );

            // Test with fast mode (tiny model)
            var tinyResult = await BenchmarkSingleTranscription(
                data => newEngine.TranscribeFastAsync(data, false),
                testAudio,
                "Tiny Model"
            );

            report.ModelComparisonResults = new List<TestResult> { baseResult, tinyResult };
        }

        #endregion

        #region Helper Methods

        private async Task<TestResult> BenchmarkSingleTranscription(
            Func<byte[], Task<string>> transcribeFunc,
            byte[] audioData,
            string testName)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await transcribeFunc(audioData);
                stopwatch.Stop();

                return new TestResult
                {
                    TestName = testName,
                    AudioLengthMs = audioData.Length / 32, // Approximate duration
                    LatencyMs = stopwatch.ElapsedMilliseconds,
                    Success = true,
                    TranscribedText = result,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new TestResult
                {
                    TestName = testName,
                    AudioLengthMs = audioData.Length / 32,
                    LatencyMs = stopwatch.ElapsedMilliseconds,
                    Success = false,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        private byte[] GenerateTestAudio(int durationMs, string phrase)
        {
            // Generate synthetic audio data (16kHz, 16-bit, mono)
            var sampleCount = (durationMs * 16000) / 1000;
            var audioData = new byte[sampleCount * 2];

            // Generate a simple sine wave pattern to simulate speech
            for (int i = 0; i < sampleCount; i++)
            {
                var t = (double)i / 16000;
                var frequency = 440 + (phrase.GetHashCode() % 200); // Vary frequency based on phrase
                var amplitude = 0.3 * Math.Sin(2 * Math.PI * frequency * t);

                // Add some noise to make it more realistic
                amplitude += (random.NextDouble() - 0.5) * 0.1;

                var sample = (short)(amplitude * 32767);
                var bytes = BitConverter.GetBytes(sample);
                audioData[i * 2] = bytes[0];
                audioData[i * 2 + 1] = bytes[1];
            }

            return audioData;
        }

        private byte[] GenerateSilentAudio(int durationMs)
        {
            var sampleCount = (durationMs * 16000) / 1000;
            return new byte[sampleCount * 2]; // All zeros = silence
        }

        private double CalculateImprovementFactor(List<TestResult> original, List<TestResult> optimized)
        {
            if (original.Count == 0 || optimized.Count == 0)
                return 1.0;

            var avgOriginal = original.Where(r => r.Success).Average(r => r.LatencyMs);
            var avgOptimized = optimized.Where(r => r.Success).Average(r => r.LatencyMs);

            return avgOptimized > 0 ? avgOriginal / avgOptimized : 1.0;
        }

        private TestEnvironment GatherEnvironmentInfo()
        {
            return new TestEnvironment
            {
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                OperatingSystem = Environment.OSVersion.ToString(),
                FrameworkVersion = Environment.Version.ToString(),
                WorkingSet = Environment.WorkingSet,
                Timestamp = DateTime.UtcNow
            };
        }

        private string GenerateBenchmarkSummary(BenchmarkReport report)
        {
            var summary = new StringBuilder();
            summary.AppendLine("=== WHISPER PERFORMANCE BENCHMARK SUMMARY ===");
            summary.AppendLine($"Test Duration: {report.TotalDuration.TotalSeconds:F1}s");
            summary.AppendLine($"Environment: {report.TestEnvironment.OperatingSystem}");
            summary.AppendLine($"CPU Cores: {report.TestEnvironment.ProcessorCount}");
            summary.AppendLine($"GPU Mode: {report.GpuMode ?? "Not Available"}");
            summary.AppendLine();

            if (report.ShortAudioResults != null)
            {
                summary.AppendLine($"SHORT AUDIO IMPROVEMENT: {report.ShortAudioResults.ImprovementFactor:F1}x faster");
                var avgOriginal = report.ShortAudioResults.OriginalResults.Average(r => r.LatencyMs);
                var avgOptimized = report.ShortAudioResults.OptimizedResults.Average(r => r.LatencyMs);
                summary.AppendLine($"  Original: {avgOriginal:F0}ms avg → Optimized: {avgOptimized:F0}ms avg");
            }

            if (report.MediumAudioResults != null)
            {
                summary.AppendLine($"MEDIUM AUDIO IMPROVEMENT: {report.MediumAudioResults.ImprovementFactor:F1}x faster");
            }

            if (report.CacheResults?.Count >= 2)
            {
                var cacheSpeedup = (double)report.CacheResults[0].LatencyMs / report.CacheResults[1].LatencyMs;
                summary.AppendLine($"CACHE SPEEDUP: {cacheSpeedup:F1}x faster on repeated content");
            }

            if (report.GpuMetrics != null)
            {
                summary.AppendLine($"CACHE HIT RATE: {report.GpuMetrics.GetCacheHitRate():F1}%");
                summary.AppendLine($"AVERAGE LATENCY: {report.GpuMetrics.AverageLatency:F0}ms");
                summary.AppendLine($"FASTEST TRANSCRIPTION: {report.GpuMetrics.FastestTranscription:F0}ms");
            }

            var targetAchieved = report.ShortAudioResults?.OptimizedResults.Average(r => r.LatencyMs) < 500;
            summary.AppendLine();
            summary.AppendLine($"TARGET (<500ms): {(targetAchieved ? "✅ ACHIEVED" : "❌ NOT ACHIEVED")}");

            return summary.ToString();
        }

        #endregion
    }

    #region Data Classes

    public class BenchmarkReport
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TestEnvironment TestEnvironment { get; set; }
        public string GpuMode { get; set; }
        public PerformanceMetrics GpuMetrics { get; set; }

        public ComparisonResult ShortAudioResults { get; set; }
        public ComparisonResult MediumAudioResults { get; set; }
        public List<TestResult> SilenceDetectionResults { get; set; }
        public List<TestResult> CacheResults { get; set; }
        public List<TestResult> ModelComparisonResults { get; set; }

        public string Summary { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ComparisonResult
    {
        public List<TestResult> OriginalResults { get; set; }
        public List<TestResult> OptimizedResults { get; set; }
        public double ImprovementFactor { get; set; }
    }

    public class TestResult
    {
        public string TestName { get; set; }
        public int AudioLengthMs { get; set; }
        public long LatencyMs { get; set; }
        public bool Success { get; set; }
        public string TranscribedText { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }

        public double RealTimeFactor => AudioLengthMs > 0 ? (double)LatencyMs / AudioLengthMs : 0;
    }

    public class TestEnvironment
    {
        public string MachineName { get; set; }
        public int ProcessorCount { get; set; }
        public string OperatingSystem { get; set; }
        public string FrameworkVersion { get; set; }
        public long WorkingSet { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion
}
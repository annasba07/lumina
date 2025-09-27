using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SuperWhisperWPF.Core;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// Simple test program to validate Whisper performance optimizations.
    /// Run this to test the new high-performance engine against the original.
    /// </summary>
    public class PerformanceTestProgram
    {
        public static async Task<string> RunPerformanceTestAsync()
        {
            try
            {
                Logger.Info("Starting Whisper Performance Test...");

                // Initialize the benchmark
                var benchmark = new WhisperPerformanceBenchmark();

                // Run comprehensive tests
                var report = await benchmark.RunComprehensiveBenchmarkAsync();

                // Display results
                var output = DisplayResults(report);

                // Save detailed report
                await SaveReportAsync(report);

                return output;
            }
            catch (Exception ex)
            {
                var error = $"Performance test failed: {ex.Message}";
                Logger.Error(error, ex);
                return error;
            }
        }

        private static string DisplayResults(BenchmarkReport report)
        {
            var output = report.Summary ?? "Benchmark completed but no summary available.";

            Logger.Info("=== PERFORMANCE TEST RESULTS ===");
            Logger.Info(output);

            // Display key metrics
            if (report.ShortAudioResults != null)
            {
                var avgOriginal = 0.0;
                var avgOptimized = 0.0;

                foreach (var result in report.ShortAudioResults.OriginalResults)
                {
                    avgOriginal += result.LatencyMs;
                }
                avgOriginal /= report.ShortAudioResults.OriginalResults.Count;

                foreach (var result in report.ShortAudioResults.OptimizedResults)
                {
                    avgOptimized += result.LatencyMs;
                }
                avgOptimized /= report.ShortAudioResults.OptimizedResults.Count;

                Logger.Info($"Original Engine Average: {avgOriginal:F0}ms");
                Logger.Info($"Optimized Engine Average: {avgOptimized:F0}ms");
                Logger.Info($"Performance Improvement: {avgOriginal / avgOptimized:F1}x faster");

                // Check if we achieved the target
                if (avgOptimized < 500)
                {
                    Logger.Info("✅ TARGET ACHIEVED: Latency < 500ms");
                }
                else
                {
                    Logger.Warning($"❌ TARGET MISSED: {avgOptimized:F0}ms > 500ms target");
                }
            }

            return output;
        }

        private static async Task SaveReportAsync(BenchmarkReport report)
        {
            try
            {
                var reportPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"whisper_benchmark_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                );

                await File.WriteAllTextAsync(reportPath, GenerateDetailedReport(report));
                Logger.Info($"Detailed report saved to: {reportPath}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to save report: {ex.Message}");
            }
        }

        private static string GenerateDetailedReport(BenchmarkReport report)
        {
            var output = new System.Text.StringBuilder();

            output.AppendLine("WHISPER PERFORMANCE BENCHMARK DETAILED REPORT");
            output.AppendLine("=" + new string('=', 50));
            output.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            output.AppendLine($"Test Duration: {report.TotalDuration.TotalSeconds:F2} seconds");
            output.AppendLine();

            // Environment Info
            output.AppendLine("ENVIRONMENT:");
            output.AppendLine($"  Machine: {report.TestEnvironment?.MachineName ?? "Unknown"}");
            output.AppendLine($"  OS: {report.TestEnvironment?.OperatingSystem ?? "Unknown"}");
            output.AppendLine($"  CPU Cores: {report.TestEnvironment?.ProcessorCount ?? 0}");
            output.AppendLine($"  GPU Mode: {report.GpuMode ?? "Not Available"}");
            output.AppendLine($"  Memory: {(report.TestEnvironment?.WorkingSet ?? 0) / 1024 / 1024:F0} MB");
            output.AppendLine();

            // Short Audio Results
            if (report.ShortAudioResults != null)
            {
                output.AppendLine("SHORT AUDIO TESTS (1-3 seconds):");
                output.AppendLine("Original Engine Results:");
                foreach (var result in report.ShortAudioResults.OriginalResults)
                {
                    output.AppendLine($"  {result.TestName}: {result.LatencyMs}ms (Audio: {result.AudioLengthMs}ms)");
                }

                output.AppendLine("Optimized Engine Results:");
                foreach (var result in report.ShortAudioResults.OptimizedResults)
                {
                    output.AppendLine($"  {result.TestName}: {result.LatencyMs}ms (Audio: {result.AudioLengthMs}ms)");
                }

                output.AppendLine($"Improvement Factor: {report.ShortAudioResults.ImprovementFactor:F2}x");
                output.AppendLine();
            }

            // Cache Results
            if (report.CacheResults?.Count >= 2)
            {
                output.AppendLine("CACHE PERFORMANCE:");
                output.AppendLine($"  Cold Cache: {report.CacheResults[0].LatencyMs}ms");
                output.AppendLine($"  Warm Cache: {report.CacheResults[1].LatencyMs}ms");
                output.AppendLine($"  Cache Speedup: {(double)report.CacheResults[0].LatencyMs / report.CacheResults[1].LatencyMs:F1}x");
                output.AppendLine();
            }

            // Silence Detection
            if (report.SilenceDetectionResults?.Count > 0)
            {
                output.AppendLine("SILENCE DETECTION:");
                foreach (var result in report.SilenceDetectionResults)
                {
                    output.AppendLine($"  {result.AudioLengthMs}ms silence: {result.LatencyMs}ms processing");
                }
                output.AppendLine();
            }

            // Overall metrics
            if (report.GpuMetrics != null)
            {
                output.AppendLine("OVERALL METRICS:");
                output.AppendLine($"  Total Transcriptions: {report.GpuMetrics.TotalTranscriptions}");
                output.AppendLine($"  Cache Hit Rate: {report.GpuMetrics.GetCacheHitRate():F1}%");
                output.AppendLine($"  Average Latency: {report.GpuMetrics.AverageLatency:F1}ms");
                output.AppendLine($"  Fastest: {report.GpuMetrics.FastestTranscription:F0}ms");
                output.AppendLine($"  Slowest: {report.GpuMetrics.SlowestTranscription:F0}ms");
                output.AppendLine($"  Error Count: {report.GpuMetrics.TotalErrors}");
                output.AppendLine();
            }

            // Summary
            output.AppendLine("SUMMARY:");
            output.AppendLine(report.Summary ?? "No summary available");

            if (!string.IsNullOrEmpty(report.ErrorMessage))
            {
                output.AppendLine();
                output.AppendLine("ERRORS:");
                output.AppendLine(report.ErrorMessage);
            }

            return output.ToString();
        }

        /// <summary>
        /// Quick test for immediate feedback on optimization effectiveness.
        /// </summary>
        public static async Task<string> QuickPerformanceTestAsync()
        {
            try
            {
                Logger.Info("Running quick performance test...");

                // Test the optimized engine only
                var engine = HighPerformanceWhisperEngine.Instance;
                await engine.InitializeAsync();

                // Generate test audio
                var testAudio = GenerateTestAudio(2000); // 2 seconds

                // Run multiple tests
                var latencies = new List<long>();
                for (int i = 0; i < 5; i++)
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var result = await engine.TranscribeFastAsync(testAudio, false);
                    stopwatch.Stop();

                    latencies.Add(stopwatch.ElapsedMilliseconds);
                    Logger.Info($"Test {i + 1}: {stopwatch.ElapsedMilliseconds}ms - '{result}'");
                }

                var avgLatency = latencies.Average();
                var minLatency = latencies.Min();

                var summary = $"Quick Test Results:\n" +
                             $"Average Latency: {avgLatency:F0}ms\n" +
                             $"Best Latency: {minLatency}ms\n" +
                             $"Target (<500ms): {(avgLatency < 500 ? "✅ ACHIEVED" : "❌ MISSED")}\n" +
                             $"GPU Mode: {engine.GpuMode}";

                Logger.Info(summary);
                return summary;
            }
            catch (Exception ex)
            {
                var error = $"Quick test failed: {ex.Message}";
                Logger.Error(error, ex);
                return error;
            }
        }

        private static byte[] GenerateTestAudio(int durationMs)
        {
            // Simple synthetic audio generation for testing
            var sampleCount = (durationMs * 16000) / 1000;
            var audioData = new byte[sampleCount * 2];

            var random = new Random(42);
            for (int i = 0; i < sampleCount; i++)
            {
                var amplitude = 0.3 * Math.Sin(2 * Math.PI * 440 * i / 16000.0);
                amplitude += (random.NextDouble() - 0.5) * 0.1;

                var sample = (short)(amplitude * 32767);
                var bytes = BitConverter.GetBytes(sample);
                audioData[i * 2] = bytes[0];
                audioData[i * 2 + 1] = bytes[1];
            }

            return audioData;
        }
    }
}
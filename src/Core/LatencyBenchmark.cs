using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SuperWhisperWPF.Core;

namespace SuperWhisperWPF
{
    /// <summary>
    /// Comprehensive benchmark to validate <200ms latency target.
    /// Tests all optimization approaches and provides detailed analysis.
    /// </summary>
    public class LatencyBenchmark
    {
        private readonly List<BenchmarkResult> results = new List<BenchmarkResult>();

        public class BenchmarkResult
        {
            public string EngineName { get; set; }
            public string TestCase { get; set; }
            public double LatencyMs { get; set; }
            public double WER { get; set; }
            public bool MetTarget { get; set; }
            public string Transcript { get; set; }
            public string ExpectedText { get; set; }
            public Dictionary<string, object> Metadata { get; set; }
        }

        /// <summary>
        /// Run comprehensive benchmark suite.
        /// </summary>
        public async Task RunFullBenchmarkAsync()
        {
            Logger.Info(@"
==================================================
   ULTRA-LOW LATENCY BENCHMARK SUITE
   Target: <200ms end-to-end latency
   Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
==================================================
");

            // Prepare test data
            var testCases = PrepareTestCases();

            // Test different engine configurations
            var engines = new List<(string name, Func<Task<ITranscriptionEngine>> factory)>
            {
                ("Baseline (Whisper.NET)", async () => {
                    var engine = new WhisperEngine();
                    await engine.InitializeAsync();
                    return new WhisperEngineAdapter(engine);
                }),

                ("Optimized (VAD + Whisper.NET)", async () => {
                    var engine = new OptimizedWhisperEngine();
                    await engine.InitializeAsync();
                    return new OptimizedEngineAdapter(engine);
                }),

                ("FasterWhisper (Native)", async () => {
                    if (!NativeWrapperBuilder.IsNativeWrapperAvailable())
                    {
                        await NativeWrapperBuilder.BuildNativeWrapperAsync();
                    }
                    var engine = new FasterWhisperEngine();
                    await engine.InitializeAsync();
                    return new FasterWhisperAdapter(engine);
                }),

                ("Ultra Pipeline (Full Optimization)", async () => {
                    var pipeline = new UltraLowLatencyPipeline();
                    await pipeline.InitializeAsync();
                    return new PipelineAdapter(pipeline);
                })
            };

            // Run benchmarks
            foreach (var (engineName, factory) in engines)
            {
                try
                {
                    Logger.Info($"\nBenchmarking: {engineName}");
                    Logger.Info(new string('-', 40));

                    using var engine = await factory();
                    await BenchmarkEngineAsync(engine, engineName, testCases);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to benchmark {engineName}: {ex.Message}");
                }
            }

            // Generate report
            GenerateReport();
        }

        /// <summary>
        /// Benchmark a single engine with all test cases.
        /// </summary>
        private async Task BenchmarkEngineAsync(
            ITranscriptionEngine engine,
            string engineName,
            List<TestCase> testCases)
        {
            // Warmup
            Logger.Info("Warming up engine...");
            for (int i = 0; i < 3; i++)
            {
                await engine.TranscribeAsync(testCases[0].AudioData);
            }

            // Actual benchmark
            foreach (var testCase in testCases)
            {
                var latencies = new List<double>();

                // Run multiple iterations for statistical significance
                const int iterations = 10;
                for (int i = 0; i < iterations; i++)
                {
                    var sw = Stopwatch.StartNew();
                    var transcript = await engine.TranscribeAsync(testCase.AudioData);
                    sw.Stop();

                    latencies.Add(sw.ElapsedMilliseconds);

                    if (i == 0) // Calculate WER only once
                    {
                        var wer = CalculateWER(transcript, testCase.ExpectedText);

                        results.Add(new BenchmarkResult
                        {
                            EngineName = engineName,
                            TestCase = testCase.Name,
                            LatencyMs = sw.ElapsedMilliseconds,
                            WER = wer,
                            MetTarget = sw.ElapsedMilliseconds < 200,
                            Transcript = transcript,
                            ExpectedText = testCase.ExpectedText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["AudioDurationMs"] = testCase.DurationMs,
                                ["AudioSizeBytes"] = testCase.AudioData.Length,
                                ["Complexity"] = testCase.Complexity
                            }
                        });
                    }
                }

                // Log statistics
                var avgLatency = latencies.Average();
                var minLatency = latencies.Min();
                var maxLatency = latencies.Max();
                var p95Latency = latencies.OrderBy(x => x).Skip((int)(iterations * 0.95)).First();

                var status = avgLatency < 200 ? "PASS" : "FAIL";
                var color = avgLatency < 200 ? ConsoleColor.Green :
                           avgLatency < 300 ? ConsoleColor.Yellow :
                           ConsoleColor.Red;

                LogWithColor(
                    $"  {testCase.Name}: Avg={avgLatency:F1}ms, Min={minLatency:F1}ms, " +
                    $"Max={maxLatency:F1}ms, P95={p95Latency:F1}ms [{status}]",
                    color
                );
            }
        }

        /// <summary>
        /// Generate comprehensive benchmark report.
        /// </summary>
        private void GenerateReport()
        {
            Logger.Info(@"
==================================================
   BENCHMARK RESULTS SUMMARY
==================================================
");

            // Group results by engine
            var engineGroups = results.GroupBy(r => r.EngineName);

            foreach (var group in engineGroups)
            {
                var engineResults = group.ToList();
                var avgLatency = engineResults.Average(r => r.LatencyMs);
                var avgWER = engineResults.Average(r => r.WER);
                var successRate = 100.0 * engineResults.Count(r => r.MetTarget) / engineResults.Count;

                Logger.Info($@"
Engine: {group.Key}
  Average Latency: {avgLatency:F1}ms
  Average WER: {avgWER:F2}%
  Success Rate (<200ms): {successRate:F1}%
  Best Case: {engineResults.Min(r => r.LatencyMs):F1}ms
  Worst Case: {engineResults.Max(r => r.LatencyMs):F1}ms
");

                // Detailed breakdown
                foreach (var result in engineResults.OrderBy(r => r.LatencyMs))
                {
                    var status = result.MetTarget ? "✓" : "✗";
                    Logger.Info($"    {status} {result.TestCase}: {result.LatencyMs:F1}ms (WER: {result.WER:F1}%)");
                }
            }

            // Overall winner
            var winner = engineGroups
                .OrderBy(g => g.Average(r => r.LatencyMs))
                .First();

            Logger.Info($@"
==================================================
   WINNER: {winner.Key}
   Average Latency: {winner.Average(r => r.LatencyMs):F1}ms
   Speedup vs Baseline: {results.Where(r => r.EngineName.Contains("Baseline")).Average(r => r.LatencyMs) / winner.Average(r => r.LatencyMs):F1}x
==================================================
");

            // Save detailed CSV report
            SaveCSVReport();
        }

        /// <summary>
        /// Prepare test cases with varying complexity.
        /// </summary>
        private List<TestCase> PrepareTestCases()
        {
            return new List<TestCase>
            {
                new TestCase
                {
                    Name = "Short Command",
                    AudioData = GenerateTestAudio(300), // 300ms
                    ExpectedText = "open file",
                    DurationMs = 300,
                    Complexity = "Simple"
                },
                new TestCase
                {
                    Name = "Medium Sentence",
                    AudioData = GenerateTestAudio(500), // 500ms
                    ExpectedText = "the quick brown fox jumps over the lazy dog",
                    DurationMs = 500,
                    Complexity = "Medium"
                },
                new TestCase
                {
                    Name = "Numbers and Symbols",
                    AudioData = GenerateTestAudio(400),
                    ExpectedText = "send $100 to account 12345",
                    DurationMs = 400,
                    Complexity = "Complex"
                },
                new TestCase
                {
                    Name = "Technical Terms",
                    AudioData = GenerateTestAudio(450),
                    ExpectedText = "initialize kubernetes deployment with nginx",
                    DurationMs = 450,
                    Complexity = "Technical"
                }
            };
        }

        /// <summary>
        /// Generate test audio data (placeholder - would use real audio in production).
        /// </summary>
        private byte[] GenerateTestAudio(int durationMs)
        {
            // Generate sine wave at 440Hz as placeholder
            var sampleRate = 16000;
            var samples = (sampleRate * durationMs) / 1000;
            var data = new byte[samples * 2]; // 16-bit samples

            for (int i = 0; i < samples; i++)
            {
                var t = (double)i / sampleRate;
                var value = (short)(Math.Sin(2 * Math.PI * 440 * t) * 5000);
                BitConverter.GetBytes(value).CopyTo(data, i * 2);
            }

            return data;
        }

        /// <summary>
        /// Calculate Word Error Rate (WER).
        /// </summary>
        private double CalculateWER(string hypothesis, string reference)
        {
            var hypWords = hypothesis.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var refWords = reference.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Simple WER calculation (Levenshtein distance)
            var distance = LevenshteinDistance(hypWords, refWords);
            return 100.0 * distance / Math.Max(1, refWords.Length);
        }

        private int LevenshteinDistance(string[] s1, string[] s2)
        {
            var m = s1.Length;
            var n = s2.Length;
            var d = new int[m + 1, n + 1];

            for (int i = 0; i <= m; i++) d[i, 0] = i;
            for (int j = 0; j <= n; j++) d[0, j] = j;

            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(
                        d[i - 1, j] + 1,    // deletion
                        d[i, j - 1] + 1),   // insertion
                        d[i - 1, j - 1] + cost); // substitution
                }
            }

            return d[m, n];
        }

        private void SaveCSVReport()
        {
            var csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                $"benchmark_results_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            var lines = new List<string>
            {
                "Engine,TestCase,LatencyMs,WER,MetTarget,Transcript,Expected"
            };

            foreach (var result in results)
            {
                lines.Add($"{result.EngineName},{result.TestCase},{result.LatencyMs:F1}," +
                         $"{result.WER:F2},{result.MetTarget},{result.Transcript},{result.ExpectedText}");
            }

            File.WriteAllLines(csvPath, lines);
            Logger.Info($"Detailed results saved to: {csvPath}");
        }

        private void LogWithColor(string message, ConsoleColor color)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }

        #region Test Support Classes

        private class TestCase
        {
            public string Name { get; set; }
            public byte[] AudioData { get; set; }
            public string ExpectedText { get; set; }
            public int DurationMs { get; set; }
            public string Complexity { get; set; }
        }

        private interface ITranscriptionEngine : IDisposable
        {
            Task<string> TranscribeAsync(byte[] audioData);
        }

        private class WhisperEngineAdapter : ITranscriptionEngine
        {
            private readonly WhisperEngine engine;
            public WhisperEngineAdapter(WhisperEngine e) => engine = e;
            public Task<string> TranscribeAsync(byte[] audioData) => engine.TranscribeAsync(audioData);
            public void Dispose() => engine.Dispose();
        }

        private class OptimizedEngineAdapter : ITranscriptionEngine
        {
            private readonly OptimizedWhisperEngine engine;
            public OptimizedEngineAdapter(OptimizedWhisperEngine e) => engine = e;
            public Task<string> TranscribeAsync(byte[] audioData) => engine.TranscribeAsync(audioData);
            public void Dispose() => engine.Dispose();
        }

        private class FasterWhisperAdapter : ITranscriptionEngine
        {
            private readonly FasterWhisperEngine engine;
            public FasterWhisperAdapter(FasterWhisperEngine e) => engine = e;
            public Task<string> TranscribeAsync(byte[] audioData) => engine.TranscribeAsync(audioData);
            public void Dispose() => engine.Dispose();
        }

        private class PipelineAdapter : ITranscriptionEngine
        {
            private readonly UltraLowLatencyPipeline pipeline;
            public PipelineAdapter(UltraLowLatencyPipeline p) => pipeline = p;

            public async Task<string> TranscribeAsync(byte[] audioData)
            {
                await pipeline.SubmitAudioAsync(audioData);
                await Task.Delay(250); // Wait for processing

                var results = pipeline.GetAllResults();
                return string.Join(" ", results.Select(r => r.Text));
            }

            public void Dispose() => pipeline.Dispose();
        }

        #endregion
    }
}
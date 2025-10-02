using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// A/B testing framework to compare multiple transcription engines in parallel.
    /// Tests same audio across different approaches to find optimal solution.
    /// </summary>
    public class EngineComparison
    {
        public class ComparisonResult
        {
            public string EngineName { get; set; }
            public string Result { get; set; }
            public long LatencyMs { get; set; }
            public bool Success { get; set; }
            public string Error { get; set; }
            public Dictionary<string, object> Metadata { get; set; } = new();
        }

        /// <summary>
        /// Test multiple engines with the same audio and compare results
        /// </summary>
        public static async Task<List<ComparisonResult>> CompareEnginesAsync(byte[] audioData)
        {
            Logger.Info($"Starting engine comparison with {audioData.Length} bytes of audio");

            var results = new List<ComparisonResult>();
            var tasks = new List<Task<ComparisonResult>>();

            // Test 1: Current Whisper.NET (CPU)
            tasks.Add(TestEngineAsync("Whisper.NET CPU (Current)", async () =>
            {
                var engine = OptimizedWhisperEngine.Instance;
                if (!engine.IsInitialized)
                {
                    await engine.InitializeAsync();
                }
                return await engine.TranscribeAsync(audioData);
            }));

            // Test 2: Deepgram Cloud API
            tasks.Add(TestEngineAsync("Deepgram Cloud API", async () =>
            {
                var apiKey = Environment.GetEnvironmentVariable("DEEPGRAM_API_KEY");
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new Exception("DEEPGRAM_API_KEY not set");
                }

                var engine = new DeepgramEngine();
                await engine.InitializeAsync();
                return await engine.TranscribeAsync(audioData);
            }));

            // Test 3: ONNX Runtime (GPU attempt)
            tasks.Add(TestEngineAsync("ONNX Runtime (GPU)", async () =>
            {
                var engine = new OnnxWhisperEngine();
                if (!await engine.InitializeAsync())
                {
                    throw new Exception("ONNX initialization failed");
                }
                return await engine.TranscribeAsync(audioData);
            }));

            // Test 4: Tiny Model (CPU optimized)
            tasks.Add(TestEngineAsync("Tiny Model CPU", async () =>
            {
                var settings = AppSettings.Instance;
                var originalSetting = settings.UseTinyModelForSpeed;

                try
                {
                    settings.UseTinyModelForSpeed = true;
                    var engine = new WhisperEngine();
                    await engine.InitializeAsync();
                    return await engine.TranscribeAsync(audioData);
                }
                finally
                {
                    settings.UseTinyModelForSpeed = originalSetting;
                }
            }));

            // Run all tests in parallel
            var completedResults = await Task.WhenAll(tasks);
            results.AddRange(completedResults);

            // Log comparison results
            Logger.Info("=== ENGINE COMPARISON RESULTS ===");
            foreach (var result in results)
            {
                var status = result.Success ? "✅" : "❌";
                Logger.Info($"{status} {result.EngineName}: {result.LatencyMs}ms - '{result.Result}'");
                if (!result.Success)
                {
                    Logger.Error($"   Error: {result.Error}");
                }
            }

            // Find best performer
            var successful = results.FindAll(r => r.Success);
            if (successful.Count > 0)
            {
                successful.Sort((a, b) => a.LatencyMs.CompareTo(b.LatencyMs));
                var fastest = successful[0];
                Logger.Info($"🏆 WINNER: {fastest.EngineName} ({fastest.LatencyMs}ms)");
            }

            return results;
        }

        private static async Task<ComparisonResult> TestEngineAsync(string engineName, Func<Task<string>> testFunc)
        {
            var result = new ComparisonResult
            {
                EngineName = engineName,
                Success = false
            };

            var sw = Stopwatch.StartNew();
            try
            {
                Logger.Debug($"Testing {engineName}...");
                result.Result = await testFunc();
                result.Success = true;
                result.LatencyMs = sw.ElapsedMilliseconds;

                // Add metadata
                result.Metadata["throughput"] = result.LatencyMs > 0 ?
                    Math.Round(1000.0 / result.LatencyMs, 2) : 0;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                result.LatencyMs = sw.ElapsedMilliseconds;
                Logger.Warning($"{engineName} failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Run continuous A/B testing with real audio samples
        /// </summary>
        public static async Task RunContinuousComparisonAsync(List<byte[]> audioSamples)
        {
            Logger.Info($"Starting continuous comparison with {audioSamples.Count} samples");

            var aggregateResults = new Dictionary<string, List<long>>();

            foreach (var (audio, index) in audioSamples.Select((a, i) => (a, i)))
            {
                Logger.Info($"\n--- Sample {index + 1}/{audioSamples.Count} ---");
                var results = await CompareEnginesAsync(audio);

                foreach (var result in results)
                {
                    if (result.Success)
                    {
                        if (!aggregateResults.ContainsKey(result.EngineName))
                        {
                            aggregateResults[result.EngineName] = new List<long>();
                        }
                        aggregateResults[result.EngineName].Add(result.LatencyMs);
                    }
                }

                await Task.Delay(100); // Brief pause between samples
            }

            // Report aggregate statistics
            Logger.Info("\n=== AGGREGATE STATISTICS ===");
            foreach (var (engine, latencies) in aggregateResults)
            {
                if (latencies.Count > 0)
                {
                    var avg = latencies.Average();
                    var min = latencies.Min();
                    var max = latencies.Max();
                    Logger.Info($"{engine}:");
                    Logger.Info($"  Average: {avg:F0}ms");
                    Logger.Info($"  Min: {min}ms");
                    Logger.Info($"  Max: {max}ms");
                    Logger.Info($"  Samples: {latencies.Count}");
                }
            }
        }
    }
}

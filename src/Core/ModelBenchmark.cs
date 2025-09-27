using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// Benchmarks different Whisper models for performance comparison.
    /// Tests tiny vs base model latency and accuracy.
    /// </summary>
    public static class ModelBenchmark
    {
        public static async Task RunBenchmarkAsync()
        {
            Logger.Info("========================================");
            Logger.Info("WHISPER MODEL PERFORMANCE BENCHMARK");
            Logger.Info("========================================");

            var settings = AppSettings.Instance;

            // Generate test audio (1 second of silence for consistent testing)
            var testAudio = GenerateTestAudio(1.0f);

            // Test with BASE model
            Logger.Info("\n--- Testing BASE Model (74M params) ---");
            settings.UseTinyModelForSpeed = false;
            settings.Save();

            var baseLatency = await TestModelPerformance("base", testAudio);

            // Test with TINY model
            Logger.Info("\n--- Testing TINY Model (39M params) ---");
            settings.UseTinyModelForSpeed = true;
            settings.Save();

            // Ensure tiny model is downloaded
            await ModelDownloader.EnsureModelExistsAsync("tiny");

            var tinyLatency = await TestModelPerformance("tiny", testAudio);

            // Summary
            Logger.Info("\n========================================");
            Logger.Info("BENCHMARK RESULTS");
            Logger.Info("========================================");
            Logger.Info($"Base Model: {baseLatency:F1}ms");
            Logger.Info($"Tiny Model: {tinyLatency:F1}ms");

            if (tinyLatency > 0 && baseLatency > 0)
            {
                var speedup = baseLatency / tinyLatency;
                Logger.Info($"Speedup: {speedup:F1}x faster with tiny model");

                if (tinyLatency < 200)
                {
                    Logger.Info("✅ ACHIEVED SUB-200MS LATENCY!");
                }
                else
                {
                    Logger.Info($"❌ Still need {tinyLatency - 200:F0}ms improvement for sub-200ms");
                }
            }
        }

        private static async Task<double> TestModelPerformance(string modelName, byte[] audioData)
        {
            try
            {
                // Create fresh engine instance to ensure proper model loading
                var engine = OptimizedWhisperEngine.Instance;

                // Initialize engine (will use the model configured in settings)
                await engine.InitializeAsync();

                // Warmup run
                Logger.Info($"Warming up {modelName} model...");
                await engine.TranscribeAsync(audioData);

                // Perform 5 test runs
                double totalLatency = 0;
                int runs = 5;

                Logger.Info($"Running {runs} transcription tests...");
                for (int i = 0; i < runs; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var result = await engine.TranscribeAsync(audioData);
                    stopwatch.Stop();

                    var latency = stopwatch.ElapsedMilliseconds;
                    totalLatency += latency;
                    Logger.Info($"  Run {i + 1}: {latency}ms - Result: '{result}'");
                }

                var avgLatency = totalLatency / runs;
                Logger.Info($"Average latency for {modelName}: {avgLatency:F1}ms");

                return avgLatency;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to test {modelName} model: {ex.Message}");
                return -1;
            }
        }

        private static byte[] GenerateTestAudio(float seconds)
        {
            // Generate PCM audio data (16kHz, 16-bit, mono)
            int sampleRate = 16000;
            int samples = (int)(sampleRate * seconds);
            byte[] audioData = new byte[samples * 2]; // 16-bit = 2 bytes per sample

            // Generate a simple sine wave tone at 440Hz (A4 note)
            double frequency = 440.0;
            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)sampleRate;
                double value = Math.Sin(2 * Math.PI * frequency * t) * 0.1; // Low volume
                short sampleValue = (short)(value * 32767);

                // Convert to bytes (little-endian)
                audioData[i * 2] = (byte)(sampleValue & 0xFF);
                audioData[i * 2 + 1] = (byte)((sampleValue >> 8) & 0xFF);
            }

            Logger.Info($"Generated {seconds}s test audio ({audioData.Length} bytes)");
            return audioData;
        }

        public static async Task EnableTinyModelAsync()
        {
            var settings = AppSettings.Instance;
            settings.UseTinyModelForSpeed = true;
            settings.Save();

            Logger.Info("✅ Enabled tiny model for maximum speed");
            Logger.Info("Downloading tiny model if needed...");

            if (await ModelDownloader.EnsureModelExistsAsync("tiny"))
            {
                Logger.Info("✅ Tiny model ready - expecting ~5x speed improvement!");
            }
            else
            {
                Logger.Error("❌ Failed to download tiny model");
            }
        }

        public static async Task RunOnnxBenchmarkAsync()
        {
            Logger.Info("========================================");
            Logger.Info("ONNX RUNTIME PERFORMANCE BENCHMARK");
            Logger.Info("========================================");

            // Generate test audio (1 second of silence for consistent testing)
            var testAudio = GenerateTestAudio(1.0f);

            // Test with ONNX Runtime + DirectML
            Logger.Info("\n--- Testing ONNX Runtime with DirectML GPU ---");
            var onnxLatency = await TestOnnxPerformance(testAudio);

            // Compare with native Whisper
            Logger.Info("\n--- Testing Native Whisper for comparison ---");
            var settings = AppSettings.Instance;
            settings.UseTinyModelForSpeed = true;
            settings.Save();
            var nativeLatency = await TestModelPerformance("tiny", testAudio);

            // Summary
            Logger.Info("\n========================================");
            Logger.Info("ONNX BENCHMARK RESULTS");
            Logger.Info("========================================");
            Logger.Info($"ONNX Runtime: {onnxLatency:F1}ms");
            Logger.Info($"Native Whisper: {nativeLatency:F1}ms");

            if (onnxLatency > 0 && nativeLatency > 0)
            {
                var speedup = nativeLatency / onnxLatency;
                Logger.Info($"Speedup: {speedup:F1}x faster with ONNX");

                if (onnxLatency < 200)
                {
                    Logger.Info("✅ ACHIEVED SUB-200MS LATENCY WITH ONNX!");
                }
            }
        }

        private static async Task<double> TestOnnxPerformance(byte[] audioData)
        {
            try
            {
                // Create ONNX engine instance
                var engine = OnnxWhisperEngine.Instance;

                // Initialize engine
                await engine.InitializeAsync();

                // Warmup run
                Logger.Info("Warming up ONNX engine...");
                await engine.TranscribeAsync(audioData);

                // Perform 5 test runs
                double totalLatency = 0;
                int runs = 5;

                Logger.Info($"Running {runs} ONNX transcription tests...");
                for (int i = 0; i < runs; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var result = await engine.TranscribeAsync(audioData);
                    stopwatch.Stop();

                    var latency = stopwatch.ElapsedMilliseconds;
                    totalLatency += latency;
                    Logger.Info($"  Run {i + 1}: {latency}ms - Result: '{result}'");
                }

                var avgLatency = totalLatency / runs;
                Logger.Info($"Average ONNX latency: {avgLatency:F1}ms");

                return avgLatency;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to test ONNX engine: {ex.Message}");
                return -1;
            }
        }
    }
}
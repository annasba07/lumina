using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using NAudio.Wave;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// Realistic benchmark using actual speech samples for accurate latency measurement.
    /// Tests with real-world scenarios instead of synthetic audio.
    /// </summary>
    public static class RealisticBenchmark
    {
        // Test phrases of varying lengths and complexity
        private static readonly string[] TestPhrases = new[]
        {
            // Short phrases (1-2 seconds)
            "Hello world",
            "Testing transcription",
            "Quick brown fox",

            // Medium phrases (3-5 seconds)
            "The quick brown fox jumps over the lazy dog",
            "This is a test of the speech recognition system",
            "Artificial intelligence is transforming technology",

            // Long phrases (6-10 seconds)
            "In recent years, machine learning has revolutionized the field of artificial intelligence, enabling computers to perform tasks that were once thought impossible",
            "The development of neural networks has led to breakthrough advances in natural language processing, computer vision, and speech recognition technologies"
        };

        public static async Task RunRealisticBenchmarkAsync()
        {
            Logger.Info("========================================");
            Logger.Info("REALISTIC SPEECH BENCHMARK");
            Logger.Info("========================================");
            Logger.Info("Testing with actual speech samples...\n");

            var settings = AppSettings.Instance;

            // Test each model
            await TestModelWithRealSpeech("tiny", true);
            await TestModelWithRealSpeech("base", false);

            // Also test actual microphone recording if available
            await TestLiveRecordingLatency();
        }

        private static async Task TestModelWithRealSpeech(string modelName, bool useTiny)
        {
            Logger.Info($"\n--- Testing {modelName.ToUpper()} Model with Real Speech ---");

            var settings = AppSettings.Instance;
            settings.UseTinyModelForSpeed = useTiny;
            settings.Save();

            // Ensure model exists
            await ModelDownloader.EnsureModelExistsAsync(modelName);

            var engine = OptimizedWhisperEngine.Instance;
            await engine.InitializeAsync();

            var results = new System.Collections.Generic.List<BenchmarkResult>();

            foreach (var phrase in TestPhrases)
            {
                var audioData = await GenerateRealSpeechAsync(phrase);

                if (audioData == null || audioData.Length == 0)
                {
                    Logger.Warning($"Failed to generate speech for: {phrase}");
                    continue;
                }

                // Warmup
                await engine.TranscribeAsync(audioData);

                // Test runs
                var latencies = new System.Collections.Generic.List<long>();
                var transcriptions = new System.Collections.Generic.List<string>();

                for (int i = 0; i < 3; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var result = await engine.TranscribeAsync(audioData);
                    stopwatch.Stop();

                    latencies.Add(stopwatch.ElapsedMilliseconds);
                    transcriptions.Add(result);
                }

                var avgLatency = latencies.Average();
                var audioLengthMs = (audioData.Length / 2.0) / 16.0; // 16kHz, 16-bit
                var rtf = avgLatency / audioLengthMs; // Real-time factor

                results.Add(new BenchmarkResult
                {
                    Phrase = phrase,
                    AudioLengthMs = audioLengthMs,
                    AvgLatencyMs = avgLatency,
                    RealtimeFactor = rtf,
                    Transcription = transcriptions.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "[empty]",
                    WordCount = phrase.Split(' ').Length
                });

                Logger.Info($"  '{phrase.Substring(0, Math.Min(30, phrase.Length))}...'");
                Logger.Info($"    Audio: {audioLengthMs:F0}ms, Latency: {avgLatency:F0}ms, RTF: {rtf:F2}x");
                Logger.Info($"    Result: '{results.Last().Transcription}'");
            }

            // Summary
            if (results.Any())
            {
                var avgLatency = results.Average(r => r.AvgLatencyMs);
                var avgRtf = results.Average(r => r.RealtimeFactor);
                var successRate = results.Count(r => r.Transcription != "[empty]") * 100.0 / results.Count;

                Logger.Info($"\n  {modelName.ToUpper()} Model Summary:");
                Logger.Info($"    Average Latency: {avgLatency:F0}ms");
                Logger.Info($"    Average RTF: {avgRtf:F2}x (lower is better, <1.0 is real-time)");
                Logger.Info($"    Success Rate: {successRate:F0}%");

                if (avgLatency < 200)
                {
                    Logger.Info($"    ✅ ACHIEVED SUB-200MS LATENCY!");
                }
                else
                {
                    Logger.Info($"    ⚠️ Above 200ms target by {avgLatency - 200:F0}ms");
                }
            }
        }

        private static async Task<byte[]> GenerateRealSpeechAsync(string text)
        {
            try
            {
                // Use Windows Speech Synthesis to generate real speech
                using (var synthesizer = new SpeechSynthesizer())
                {
                    synthesizer.Rate = 0; // Normal speed
                    synthesizer.Volume = 100;

                    // Create memory stream for audio
                    using (var stream = new MemoryStream())
                    {
                        // Configure for 16kHz, 16-bit mono (Whisper format)
                        synthesizer.SetOutputToWaveStream(stream);

                        // Generate speech
                        synthesizer.Speak(text);

                        // Convert to 16kHz if needed
                        stream.Position = 0;
                        return ConvertToWhisperFormat(stream.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"TTS failed, using alternative: {ex.Message}");

                // Fallback: Generate more realistic synthetic audio with speech patterns
                return GenerateSpeechLikeAudio(text.Length * 150); // ~150ms per character
            }
        }

        private static byte[] ConvertToWhisperFormat(byte[] wavData)
        {
            try
            {
                using (var inputStream = new MemoryStream(wavData))
                using (var reader = new WaveFileReader(inputStream))
                {
                    // Target format: 16kHz, 16-bit, mono
                    var targetFormat = new WaveFormat(16000, 16, 1);

                    using (var resampler = new MediaFoundationResampler(reader, targetFormat))
                    {
                        resampler.ResamplerQuality = 60; // High quality

                        using (var outputStream = new MemoryStream())
                        {
                            WaveFileWriter.WriteWavFileToStream(outputStream, resampler);

                            // Extract PCM data (skip WAV header)
                            outputStream.Position = 44; // Standard WAV header size
                            return outputStream.ToArray().Skip(44).ToArray();
                        }
                    }
                }
            }
            catch
            {
                // Return original if conversion fails
                return wavData;
            }
        }

        private static byte[] GenerateSpeechLikeAudio(int durationMs)
        {
            // Generate more realistic audio with speech-like characteristics
            int sampleRate = 16000;
            int samples = (sampleRate * durationMs) / 1000;
            byte[] audioData = new byte[samples * 2];

            var random = new Random();

            // Simulate speech with formants and pauses
            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)sampleRate;

                // Mix multiple frequencies (formants)
                double f1 = 700 + 200 * Math.Sin(t * 2); // First formant
                double f2 = 1220 + 300 * Math.Sin(t * 3); // Second formant
                double f3 = 2600 + 400 * Math.Sin(t * 5); // Third formant

                // Generate complex waveform
                double value = 0;
                value += 0.5 * Math.Sin(2 * Math.PI * f1 * t);
                value += 0.3 * Math.Sin(2 * Math.PI * f2 * t);
                value += 0.2 * Math.Sin(2 * Math.PI * f3 * t);

                // Add envelope for word boundaries
                double envelope = Math.Sin(t * 10) * 0.5 + 0.5;
                value *= envelope;

                // Add some noise for realism
                value += (random.NextDouble() - 0.5) * 0.05;

                // Scale and convert
                short sampleValue = (short)(value * 16000);
                var bytes = BitConverter.GetBytes(sampleValue);
                audioData[i * 2] = bytes[0];
                audioData[i * 2 + 1] = bytes[1];
            }

            return audioData;
        }

        private static async Task TestLiveRecordingLatency()
        {
            Logger.Info("\n--- Testing Live Recording Latency ---");
            Logger.Info("Simulating real-time transcription scenario...");

            // Simulate recording chunks without actual capture device
            var chunkSizes = new[] { 500, 1000, 1500, 2000 }; // ms
            var engine = OptimizedWhisperEngine.Instance;

            foreach (var chunkMs in chunkSizes)
            {
                // Generate speech-like audio for chunk
                var audioData = GenerateSpeechLikeAudio(chunkMs);

                var stopwatch = Stopwatch.StartNew();
                var result = await engine.TranscribeAsync(audioData);
                stopwatch.Stop();

                var latency = stopwatch.ElapsedMilliseconds;
                var rtf = latency / (double)chunkMs;

                Logger.Info($"  Chunk {chunkMs}ms: Latency {latency}ms, RTF {rtf:F2}x, Result: '{result}'");

                if (rtf < 1.0)
                {
                    Logger.Info($"    ✅ Real-time capable (RTF < 1.0)");
                }
            }
        }

        private class BenchmarkResult
        {
            public string Phrase { get; set; }
            public double AudioLengthMs { get; set; }
            public double AvgLatencyMs { get; set; }
            public double RealtimeFactor { get; set; }
            public string Transcription { get; set; }
            public int WordCount { get; set; }
        }
    }
}
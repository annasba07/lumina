using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Whisper.net;

namespace SuperWhisperWindows
{
    public class WhisperEngine : IDisposable
    {
        private WhisperFactory whisperFactory;
        private WhisperProcessor processor;
        private bool isInitialized = false;
        private readonly object lockObject = new object();

        public async Task<bool> InitializeAsync()
        {
            Logger.Info("Starting WhisperEngine initialization with Whisper.net...");
            
            lock (lockObject)
            {
                if (isInitialized) 
                {
                    Logger.Info("WhisperEngine already initialized");
                    return true;
                }

                try
                {
                    // Check for model file in multiple locations
                    var possiblePaths = new[]
                    {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ggml-base.en.bin"),
                        Path.Combine(Environment.CurrentDirectory, "ggml-base.en.bin"),
                        Path.Combine(Environment.CurrentDirectory, "bin\\Release\\net8.0-windows", "ggml-base.en.bin"),
                        Path.Combine(Environment.CurrentDirectory, "bin\\Debug\\net8.0-windows", "ggml-base.en.bin")
                    };
                    
                    string modelPath = null;
                    foreach (var path in possiblePaths)
                    {
                        Logger.Debug($"Checking model path: {path}");
                        if (File.Exists(path))
                        {
                            modelPath = path;
                            var fileInfo = new FileInfo(path);
                            Logger.Info($"Found model file: {path} (Size: {fileInfo.Length / (1024 * 1024):F1} MB)");
                            break;
                        }
                    }
                    
                    if (modelPath == null)
                    {
                        Logger.Error("Model file 'ggml-base.en.bin' not found in any expected location:");
                        foreach (var path in possiblePaths)
                        {
                            Logger.Error($"  - {path}");
                        }
                        throw new FileNotFoundException("Model file not found in any expected location");
                    }

                    Logger.Info("Creating Whisper.net factory and processor...");
                    
                    // Create WhisperFactory from model path
                    whisperFactory = WhisperFactory.FromPath(modelPath);
                    Logger.Info("✅ WhisperFactory created successfully");
                    
                    // Create processor with simple configuration
                    processor = whisperFactory.CreateBuilder()
                        .WithLanguage("en")
                        .WithPrompt("") // No initial prompt
                        .WithTemperature(0.0f) // Deterministic output
                        .Build();
                        
                    Logger.Info("✅ WhisperProcessor created successfully");

                    isInitialized = true;
                    Logger.Info("WhisperEngine initialization completed successfully with Whisper.net");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Whisper.net initialization failed: {ex.Message}", ex);
                    isInitialized = false;
                    return false;
                }
            }
        }

        public async Task<string> TranscribeAsync(byte[] audioData)
        {
            Logger.Debug($"TranscribeAsync called with {audioData.Length} bytes of audio data");
            
            if (!isInitialized)
            {
                Logger.Warning("WhisperEngine not initialized, attempting to initialize...");
                if (!await InitializeAsync())
                {
                    Logger.Error("Failed to initialize WhisperEngine for transcription");
                    throw new Exception("Whisper engine not initialized");
                }
            }

            try
            {
                Logger.Debug("Starting Whisper.net transcription process...");
                
                // Convert byte array to MemoryStream for Whisper.net
                Logger.Debug($"Converting {audioData.Length} bytes to audio stream...");
                using var audioStream = ConvertToWaveStream(audioData);
                
                // Log audio analysis
                var audioMax = CalculateMaxAudioLevel(audioData);
                var duration = audioData.Length / 2.0 / 16000.0; // 16-bit samples at 16kHz
                Logger.Info($"Audio Analysis: Duration={duration:F1}s, MaxLevel={audioMax:F4}");

                // Perform transcription with Whisper.net
                Logger.Info("Starting Whisper.net transcription...");
                var startTime = DateTime.UtcNow;
                
                var text = new StringBuilder();
                await foreach (var segment in processor.ProcessAsync(audioStream))
                {
                    Logger.Info($"Segment: '{segment.Text?.Trim()}'");
                    if (!string.IsNullOrWhiteSpace(segment.Text))
                    {
                        text.Append(segment.Text.Trim());
                        text.Append(" ");
                    }
                }
                
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                var finalText = text.ToString().Trim();
                
                Logger.Info($"Whisper.net transcription completed in {processingTime:F1}ms");
                Logger.Info($"Result: '{finalText}' ({finalText.Length} characters)");
                
                return finalText;
            }
            catch (Exception ex)
            {
                Logger.Error($"Whisper.net transcription error: {ex.Message}", ex);
                return "";
            }
        }

        private Stream ConvertToWaveStream(byte[] audioData)
        {
            // Whisper.net expects a WAV stream with proper headers
            // Our audioData is 16-bit PCM, 16kHz, mono
            const int sampleRate = 16000;
            const int channels = 1;
            const int bitsPerSample = 16;
            const int byteRate = sampleRate * channels * bitsPerSample / 8;
            const int blockAlign = channels * bitsPerSample / 8;

            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);

            // WAV header
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + audioData.Length); // ChunkSize
            writer.Write("WAVE".ToCharArray());

            // Format subchunk
            writer.Write("fmt ".ToCharArray());
            writer.Write(16); // Subchunk1Size (PCM = 16)
            writer.Write((ushort)1); // AudioFormat (PCM = 1)
            writer.Write((ushort)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((ushort)blockAlign);
            writer.Write((ushort)bitsPerSample);

            // Data subchunk
            writer.Write("data".ToCharArray());
            writer.Write(audioData.Length);
            writer.Write(audioData);

            stream.Position = 0;
            return stream;
        }

        private float CalculateMaxAudioLevel(byte[] audioData)
        {
            float max = 0f;
            for (int i = 0; i < audioData.Length; i += 2)
            {
                if (i + 1 < audioData.Length)
                {
                    var sample = Math.Abs(BitConverter.ToInt16(audioData, i));
                    max = Math.Max(max, sample);
                }
            }
            return max / 32768f; // Normalize to 0-1 range
        }


        public void Dispose()
        {
            lock (lockObject)
            {
                if (processor != null)
                {
                    Logger.Info("Disposing WhisperProcessor...");
                    processor.Dispose();
                    processor = null;
                }
                
                if (whisperFactory != null)
                {
                    Logger.Info("Disposing WhisperFactory...");
                    whisperFactory.Dispose();
                    whisperFactory = null;
                }
                
                isInitialized = false;
                Logger.Info("WhisperEngine disposed");
            }
        }
    }
}
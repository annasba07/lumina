using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Whisper.net;

namespace SuperWhisperWPF
{
    /// <summary>
    /// Manages the Whisper.NET speech recognition engine for transcribing audio to text.
    /// Handles model initialization, audio processing, and transcription operations.
    /// </summary>
    public class WhisperEngine : IDisposable
    {
        private WhisperFactory whisperFactory;
        private WhisperProcessor processor;
        private bool isInitialized = false;
        private readonly object lockObject = new object();

        /// <summary>
        /// Initializes the Whisper engine with the specified model asynchronously.
        /// Loads the ggml-base.en.bin model and prepares the processor for transcription.
        /// </summary>
        /// <returns>True if initialization succeeded, false otherwise.</returns>
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
                    var settings = AppSettings.Instance;
                    var modelPath = settings.FindModelPath();

                    if (modelPath == null)
                    {
                        throw new FileNotFoundException($"Model file '{settings.ModelFileName}' not found in any expected location");
                    }

                    Logger.Info("Creating Whisper.net factory and processor...");
                    
                    // Create WhisperFactory from model path
                    whisperFactory = WhisperFactory.FromPath(modelPath);
                    Logger.Info("✅ WhisperFactory created successfully");
                    
                    // Create processor with speed optimizations
                    processor = whisperFactory.CreateBuilder()
                        .WithLanguage(settings.Language)
                        .WithPrompt("") // No initial prompt
                        .WithTemperature(0.0f) // Use greedy decoding for speed
                        .WithSpeedUp2x() // Enable 2x speedup
                        .WithThreads(Environment.ProcessorCount) // Use all CPU cores
                        .WithNoContext() // Disable context for speed
                        .WithSingleSegment() // Process as single segment
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

        /// <summary>
        /// Transcribes audio data to text using the Whisper model.
        /// </summary>
        /// <param name="audioData">Raw PCM audio data (16kHz, 16-bit, mono).</param>
        /// <returns>Transcribed text string, or empty string if transcription fails.</returns>
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

        /// <summary>
        /// Converts raw PCM audio bytes to a properly formatted WAV stream.
        /// </summary>
        /// <param name="audioData">Raw PCM audio bytes.</param>
        /// <returns>WAV-formatted memory stream ready for Whisper processing.</returns>
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

        /// <summary>
        /// Calculates the maximum audio level in the provided audio data.
        /// </summary>
        /// <param name="audioData">Raw audio bytes to analyze.</param>
        /// <returns>Normalized maximum audio level (0.0 to 1.0).</returns>
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


        /// <summary>
        /// Releases all resources used by the WhisperEngine.
        /// Disposes of the Whisper processor and factory instances.
        /// </summary>
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
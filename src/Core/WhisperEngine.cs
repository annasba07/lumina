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

        // VAD (Voice Activity Detection) parameters for performance optimization
        private readonly float silenceThreshold = 0.01f;  // Matches SuperWhisper
        private readonly float minimumSpeechRatio = 0.1f; // 10% speech required

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
                    // Use tiny model if enabled for speed (~5x faster)
                    var modelPath = settings.UseTinyModelForSpeed ?
                        settings.FindTinyModelPath() :
                        settings.FindModelPath();

                    if (modelPath == null)
                    {
                        throw new FileNotFoundException($"Model file '{settings.ModelFileName}' not found in any expected location");
                    }

                    var modelName = settings.UseTinyModelForSpeed ? "ggml-tiny.en.bin" : "ggml-base.en.bin";
                    Logger.Info($"Creating Whisper.net factory with model: {modelName}");
                    
                    // Create WhisperFactory from model path
                    whisperFactory = WhisperFactory.FromPath(modelPath);
                    Logger.Info("✅ WhisperFactory created successfully");
                    
                    // Create processor with valid optimizations
                    processor = whisperFactory.CreateBuilder()
                        .WithLanguage(settings.Language)
                        .WithPrompt("") // No initial prompt
                        .WithTemperature(0.0f) // Use greedy decoding for speed
                        .WithThreads(Math.Min(4, Environment.ProcessorCount)) // Limit thread contention
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
            var overallStart = DateTime.UtcNow;
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

            // VAD preprocessing - skip transcription for silence (matching SuperWhisper)
            var vadStart = DateTime.UtcNow;
            if (IsAudioSilent(audioData))
            {
                var vadTime = (DateTime.UtcNow - vadStart).TotalMilliseconds;
                Logger.Info($"VAD: Skipped silent audio in {vadTime:F1}ms, saved transcription time");
                return "[BLANK_AUDIO]"; // Match SuperWhisper's behavior
            }
            var vadTime2 = (DateTime.UtcNow - vadStart).TotalMilliseconds;
            Logger.Info($"⏱️ VAD Check: {vadTime2:F1}ms");

            try
            {
                Logger.Debug("Starting Whisper.net transcription process...");

                // Convert byte array to float array for Whisper.net
                var conversionStart = DateTime.UtcNow;
                Logger.Debug($"Converting {audioData.Length} bytes to float array...");
                var floatArray = ConvertToFloatArray(audioData);
                var conversionTime = (DateTime.UtcNow - conversionStart).TotalMilliseconds;
                Logger.Info($"⏱️ Audio Conversion: {conversionTime:F1}ms");

                // Log audio analysis
                var analysisStart = DateTime.UtcNow;
                var audioMax = CalculateMaxAudioLevel(audioData);
                var duration = audioData.Length / 2.0 / 16000.0; // 16-bit samples at 16kHz
                var analysisTime = (DateTime.UtcNow - analysisStart).TotalMilliseconds;
                Logger.Info($"Audio Analysis: Duration={duration:F1}s, MaxLevel={audioMax:F4} (took {analysisTime:F1}ms)");

                // Perform transcription with Whisper.net
                Logger.Info("Starting Whisper.net transcription...");
                var inferenceStart = DateTime.UtcNow;

                var text = new StringBuilder();
                var segmentCount = 0;
                var firstSegmentTime = 0.0;

                await foreach (var segment in processor.ProcessAsync(floatArray))
                {
                    segmentCount++;
                    if (segmentCount == 1)
                    {
                        firstSegmentTime = (DateTime.UtcNow - inferenceStart).TotalMilliseconds;
                        Logger.Info($"⏱️ First Segment: {firstSegmentTime:F1}ms");
                    }

                    Logger.Debug($"Segment {segmentCount}: '{segment.Text?.Trim()}'");
                    if (!string.IsNullOrWhiteSpace(segment.Text))
                    {
                        text.Append(segment.Text.Trim());
                        text.Append(" ");
                    }
                }

                var inferenceTime = (DateTime.UtcNow - inferenceStart).TotalMilliseconds;
                var finalText = text.ToString().Trim();

                var totalTime = (DateTime.UtcNow - overallStart).TotalMilliseconds;

                Logger.Info($"⏱️ PROFILING BREAKDOWN:");
                Logger.Info($"  - VAD Check:        {vadTime2:F1}ms");
                Logger.Info($"  - Audio Conversion: {conversionTime:F1}ms");
                Logger.Info($"  - Audio Analysis:   {analysisTime:F1}ms");
                Logger.Info($"  - Model Inference:  {inferenceTime:F1}ms");
                Logger.Info($"  - Total Segments:   {segmentCount}");
                Logger.Info($"  - TOTAL TIME:       {totalTime:F1}ms");
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
        /// Converts raw PCM audio bytes to float array for Whisper processing.
        /// </summary>
        /// <param name="audioData">Raw PCM audio bytes (16-bit, 16kHz, mono).</param>
        /// <returns>Float array normalized to [-1.0, 1.0] range.</returns>
        private float[] ConvertToFloatArray(byte[] audioData)
        {
            // Convert 16-bit PCM to float array
            var sampleCount = audioData.Length / 2; // 16-bit = 2 bytes per sample
            var floatArray = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                // Read 16-bit sample (little-endian)
                var sample = BitConverter.ToInt16(audioData, i * 2);
                // Normalize to [-1.0, 1.0] range
                floatArray[i] = sample / 32768.0f;
            }

            return floatArray;
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
        /// Voice Activity Detection (VAD) to filter silence and improve performance.
        /// Matches SuperWhisper's behavior of returning "[BLANK_AUDIO]" for silence.
        /// This can reduce latency by 90%+ for silent/noise audio.
        /// </summary>
        private bool IsAudioSilent(byte[] audioData)
        {
            if (audioData == null || audioData.Length < 320) // Less than 10ms of audio
                return true;

            var samples = audioData.Length / 2;
            var speechSamples = 0;

            // Sample every 10th sample for performance (statistically accurate)
            for (int i = 0; i < audioData.Length - 1; i += 20)
            {
                var sample = Math.Abs(BitConverter.ToInt16(audioData, i)) / 32768f;
                if (sample > silenceThreshold)
                    speechSamples++;
            }

            // Calculate speech ratio based on sampled data
            var speechRatio = speechSamples * 10f / samples;
            var isSilent = speechRatio < minimumSpeechRatio;

            if (isSilent)
            {
                Logger.Debug($"VAD: Audio detected as silence (speech ratio: {speechRatio:F2})");
            }

            return isSilent;
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
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using SuperWhisperWPF.Core;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// Implements real-time streaming audio processing for ultra-low latency transcription.
    /// Inspired by SuperWhisper's architecture for sub-second response times.
    /// </summary>
    public class StreamingAudioProcessor : IDisposable
    {
        // Chunk settings for streaming
        private const int CHUNK_SIZE_MS = 250; // Process every 250ms for low latency
        private const int MIN_SPEECH_MS = 100; // Minimum speech duration to process
        private const float VAD_THRESHOLD = 0.01f; // Voice activity detection threshold

        private readonly ConcurrentQueue<byte[]> audioChunks;
        private readonly SemaphoreSlim processingSemaphore;
        private CancellationTokenSource cancellationTokenSource;
        private Task processingTask;
        private bool isProcessing;

        // Events
        public event EventHandler<string> PartialTranscription;
        public event EventHandler<string> FinalTranscription;
        public event EventHandler<float> VoiceActivityDetected;

        public StreamingAudioProcessor()
        {
            audioChunks = new ConcurrentQueue<byte[]>();
            processingSemaphore = new SemaphoreSlim(1, 1);
            cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Starts the streaming processor with continuous chunk processing.
        /// </summary>
        public void StartStreaming()
        {
            if (isProcessing) return;

            isProcessing = true;
            cancellationTokenSource = new CancellationTokenSource();

            // Start background processing task
            processingTask = Task.Run(ProcessAudioStreamAsync);
            Logger.Info("Streaming audio processor started");
        }

        /// <summary>
        /// Adds audio data to the processing queue with VAD.
        /// </summary>
        public void AddAudioChunk(byte[] audioData)
        {
            if (!isProcessing) return;

            // Quick VAD check
            float activity = CalculateVoiceActivity(audioData);
            VoiceActivityDetected?.Invoke(this, activity);

            if (activity > VAD_THRESHOLD)
            {
                audioChunks.Enqueue(audioData);
                Logger.Debug($"Audio chunk queued: {audioData.Length} bytes, activity: {activity:F3}");
            }
        }

        /// <summary>
        /// Processes audio chunks in real-time as they arrive.
        /// </summary>
        private async Task ProcessAudioStreamAsync()
        {
            var buffer = new MemoryStream();
            var lastProcessTime = DateTime.Now;
            var silenceCount = 0;

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (audioChunks.TryDequeue(out byte[] chunk))
                    {
                        // Add to buffer
                        await buffer.WriteAsync(chunk, 0, chunk.Length);
                        silenceCount = 0;

                        // Process if we have enough data or timeout
                        var elapsed = (DateTime.Now - lastProcessTime).TotalMilliseconds;
                        if (buffer.Length >= GetChunkSizeBytes() || elapsed >= CHUNK_SIZE_MS)
                        {
                            await ProcessBufferedAudio(buffer);
                            buffer.SetLength(0); // Clear buffer
                            lastProcessTime = DateTime.Now;
                        }
                    }
                    else
                    {
                        // No audio - check for end of speech
                        silenceCount++;
                        if (silenceCount > 10 && buffer.Length > 0) // ~100ms silence
                        {
                            await ProcessBufferedAudio(buffer, isFinal: true);
                            buffer.SetLength(0);
                            silenceCount = 0;
                        }

                        await Task.Delay(10); // Small delay when queue is empty
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Streaming processing error: {ex.Message}", ex);
                }
            }

            buffer.Dispose();
        }

        /// <summary>
        /// Processes buffered audio and triggers transcription.
        /// </summary>
        private async Task ProcessBufferedAudio(MemoryStream buffer, bool isFinal = false)
        {
            if (buffer.Length == 0) return;

            await processingSemaphore.WaitAsync();
            try
            {
                var audioData = buffer.ToArray();

                // Simulate fast transcription (replace with actual Whisper call)
                var transcription = await QuickTranscribeAsync(audioData);

                if (!string.IsNullOrEmpty(transcription))
                {
                    if (isFinal)
                    {
                        FinalTranscription?.Invoke(this, transcription);
                        Logger.Info($"Final transcription: {transcription}");
                    }
                    else
                    {
                        PartialTranscription?.Invoke(this, transcription);
                        Logger.Debug($"Partial transcription: {transcription}");
                    }
                }
            }
            finally
            {
                processingSemaphore.Release();
            }
        }

        /// <summary>
        /// Fast transcription for streaming (would use optimized Whisper model).
        /// </summary>
        private async Task<string> QuickTranscribeAsync(byte[] audioData)
        {
            // TODO: Replace with actual Whisper streaming API
            // For now, simulate processing delay
            await Task.Delay(50);

            // In production, this would:
            // 1. Use tiny.en model for speed
            // 2. Process smaller chunks
            // 3. Use GPU acceleration
            // 4. Cache common phrases

            return $"[Streaming: {audioData.Length} bytes]";
        }

        /// <summary>
        /// Calculates voice activity level in audio data.
        /// </summary>
        private float CalculateVoiceActivity(byte[] audioData)
        {
            if (audioData == null || audioData.Length < 2)
                return 0;

            float sum = 0;
            int sampleCount = audioData.Length / 2;

            for (int i = 0; i < audioData.Length - 1; i += 2)
            {
                short sample = BitConverter.ToInt16(audioData, i);
                sum += Math.Abs(sample / 32768f);
            }

            return sum / sampleCount;
        }

        /// <summary>
        /// Gets the chunk size in bytes based on audio format.
        /// </summary>
        private int GetChunkSizeBytes()
        {
            // 16kHz, 16-bit mono = 32 bytes/ms
            return (Constants.Audio.BYTES_PER_SECOND * CHUNK_SIZE_MS) / 1000;
        }

        /// <summary>
        /// Stops streaming and processes remaining audio.
        /// </summary>
        public async Task StopStreamingAsync()
        {
            if (!isProcessing) return;

            isProcessing = false;
            cancellationTokenSource.Cancel();

            if (processingTask != null)
            {
                await processingTask;
            }

            // Process any remaining chunks
            while (audioChunks.TryDequeue(out byte[] chunk))
            {
                // Final processing
            }

            Logger.Info("Streaming audio processor stopped");
        }

        public void Dispose()
        {
            StopStreamingAsync().Wait(1000);
            processingSemaphore?.Dispose();
            cancellationTokenSource?.Dispose();
        }
    }
}
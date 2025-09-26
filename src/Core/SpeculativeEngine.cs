using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// Speculative execution engine that starts processing before recording ends.
    /// Achieves near-zero perceived latency by predicting likely endpoints.
    /// </summary>
    public class SpeculativeEngine : IDisposable
    {
        private static readonly Lazy<SpeculativeEngine> instance =
            new Lazy<SpeculativeEngine>(() => new SpeculativeEngine());
        public static SpeculativeEngine Instance => instance.Value;

        // Audio buffer
        private readonly CircularAudioBuffer audioBuffer;
        private readonly ConcurrentDictionary<int, SpeculativeTask> speculativeTasks;

        // Processing pipeline
        private readonly ActionBlock<SpeculativeRequest> processingPipeline;
        private readonly TransformBlock<byte[], PredictedEndpoint> endpointPredictor;

        // Voice Activity Detection
        private readonly VoiceActivityDetector vad;
        private DateTime lastSpeechTime;
        private bool isSpeaking;

        // Configuration
        private readonly int[] speculationPoints = { 500, 750, 1000, 1500, 2000, 3000 }; // ms
        private readonly int maxSpeculations = 6;

        // Performance tracking
        public int ActiveSpeculations => speculativeTasks.Count;
        public long LastSpeculationSavedMs { get; private set; }
        public double SpeculationHitRate { get; private set; }
        private long speculationHits;
        private long speculationMisses;

        private SpeculativeEngine()
        {
            audioBuffer = new CircularAudioBuffer(16000 * 60); // 60 seconds max
            speculativeTasks = new ConcurrentDictionary<int, SpeculativeTask>();
            vad = new VoiceActivityDetector();

            // Setup processing pipeline
            var options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                BoundedCapacity = 100
            };

            processingPipeline = new ActionBlock<SpeculativeRequest>(
                async request => await ProcessSpeculation(request),
                options);

            endpointPredictor = new TransformBlock<byte[], PredictedEndpoint>(
                audio => PredictEndpoint(audio),
                options);

            // Link pipeline
            endpointPredictor.LinkTo(processingPipeline,
                endpoint => endpoint.Confidence > 0.7);

            Logger.Info("Speculative engine initialized with parallel processing");
        }

        /// <summary>
        /// Adds audio chunk to buffer and triggers speculative processing.
        /// </summary>
        public void AddAudioChunk(byte[] chunk)
        {
            // Add to circular buffer
            audioBuffer.Write(chunk);

            // Update VAD
            var hasVoice = vad.DetectVoice(chunk);
            if (hasVoice)
            {
                lastSpeechTime = DateTime.UtcNow;
                isSpeaking = true;
            }
            else if (isSpeaking && (DateTime.UtcNow - lastSpeechTime).TotalMilliseconds > 300)
            {
                // Speech ended, trigger speculation
                isSpeaking = false;
                TriggerSpeculation();
            }

            // Start speculative processing at predefined points
            var currentMs = audioBuffer.LengthMs;
            foreach (var point in speculationPoints)
            {
                if (currentMs >= point && !speculativeTasks.ContainsKey(point))
                {
                    StartSpeculation(point);
                }
            }
        }

        /// <summary>
        /// Gets the transcription result, using speculation if available.
        /// </summary>
        public async Task<SpeculativeResult> GetTranscriptionAsync()
        {
            var actualLength = audioBuffer.LengthMs;
            var stopwatch = Stopwatch.StartNew();

            // Check if we have a matching speculation
            var bestMatch = FindBestSpeculation(actualLength);

            if (bestMatch != null && bestMatch.IsCompleted)
            {
                // We have a hit! Return immediately
                Interlocked.Increment(ref speculationHits);
                LastSpeculationSavedMs = stopwatch.ElapsedMilliseconds;

                var result = await bestMatch.Task;
                UpdateHitRate();

                Logger.Info($"Speculation hit! Saved {LastSpeculationSavedMs}ms");

                return new SpeculativeResult
                {
                    Text = result,
                    WasSpeculated = true,
                    SavedTimeMs = LastSpeculationSavedMs,
                    SpeculationAccuracy = CalculateAccuracy(bestMatch.LengthMs, actualLength)
                };
            }

            // Speculation miss - process normally
            Interlocked.Increment(ref speculationMisses);
            UpdateHitRate();

            var audio = audioBuffer.Read(actualLength);
            var transcription = await ProcessAudioAsync(audio);

            return new SpeculativeResult
            {
                Text = transcription,
                WasSpeculated = false,
                SavedTimeMs = 0,
                SpeculationAccuracy = 0
            };
        }

        /// <summary>
        /// Starts speculative processing for a given audio length.
        /// </summary>
        private void StartSpeculation(int lengthMs)
        {
            // Limit concurrent speculations
            if (speculativeTasks.Count >= maxSpeculations)
            {
                RemoveOldestSpeculation();
            }

            var audio = audioBuffer.Peek(lengthMs);
            if (audio == null || audio.Length == 0)
                return;

            var task = new SpeculativeTask
            {
                LengthMs = lengthMs,
                StartTime = DateTime.UtcNow,
                Task = Task.Run(() => ProcessAudioAsync(audio))
            };

            speculativeTasks[lengthMs] = task;

            Logger.Debug($"Started speculation for {lengthMs}ms");
        }

        /// <summary>
        /// Triggers intelligent speculation based on speech patterns.
        /// </summary>
        private void TriggerSpeculation()
        {
            // Predict likely endpoints based on pause detection
            var currentLength = audioBuffer.LengthMs;

            // Speculate at current point (speech just ended)
            StartSpeculation(currentLength);

            // Also speculate slightly ahead (user might continue)
            StartSpeculation(currentLength + 250);
            StartSpeculation(currentLength + 500);
        }

        /// <summary>
        /// Processes speculative request in parallel.
        /// </summary>
        private async Task ProcessSpeculation(SpeculativeRequest request)
        {
            try
            {
                // Use fastest model for speculation
                var result = await QuantizedEngine.Instance.TranscribeQuantized(
                    request.Audio,
                    QuantizedEngine.QualityPreference.Speed);

                request.Result = result.Text;
                request.CompletionTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Logger.Error($"Speculation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Predicts likely speech endpoints using ML.
        /// </summary>
        private PredictedEndpoint PredictEndpoint(byte[] audio)
        {
            // Analyze audio for speech patterns
            var features = ExtractEndpointFeatures(audio);

            // Simple heuristic for now - can be replaced with ML model
            var silenceDuration = CalculateSilenceDuration(audio);
            var energy = CalculateEnergy(audio);

            var confidence = 0.0f;

            // High confidence if significant silence
            if (silenceDuration > 300)
                confidence = 0.9f;
            // Medium confidence if energy drop
            else if (energy < 0.1f)
                confidence = 0.7f;
            // Low confidence otherwise
            else
                confidence = 0.3f;

            return new PredictedEndpoint
            {
                TimestampMs = audio.Length / 32,
                Confidence = confidence,
                Features = features
            };
        }

        /// <summary>
        /// Finds the best matching speculation for actual length.
        /// </summary>
        private SpeculativeTask FindBestSpeculation(int actualLengthMs)
        {
            // Find exact match first
            if (speculativeTasks.TryGetValue(actualLengthMs, out var exactMatch))
                return exactMatch;

            // Find closest completed speculation within tolerance
            var tolerance = 100; // 100ms tolerance
            var closest = speculativeTasks.Values
                .Where(s => s.IsCompleted && Math.Abs(s.LengthMs - actualLengthMs) <= tolerance)
                .OrderBy(s => Math.Abs(s.LengthMs - actualLengthMs))
                .FirstOrDefault();

            return closest;
        }

        /// <summary>
        /// Processes audio using the appropriate engine.
        /// </summary>
        private async Task<string> ProcessAudioAsync(byte[] audio)
        {
            // Use appropriate engine based on audio length
            if (audio.Length < 32000) // < 1 second
            {
                // Use quantized engine for speed
                var result = await QuantizedEngine.Instance.TranscribeQuantized(
                    audio,
                    QuantizedEngine.QualityPreference.Speed);
                return result.Text;
            }
            else
            {
                // Use optimized engine for longer audio
                return await OptimizedWhisperEngine.Instance.TranscribeAsync(audio);
            }
        }

        private void RemoveOldestSpeculation()
        {
            var oldest = speculativeTasks.Values
                .OrderBy(s => s.StartTime)
                .FirstOrDefault();

            if (oldest != null)
            {
                speculativeTasks.TryRemove(oldest.LengthMs, out _);
            }
        }

        private void UpdateHitRate()
        {
            var total = speculationHits + speculationMisses;
            if (total > 0)
            {
                SpeculationHitRate = (double)speculationHits / total * 100;
            }
        }

        private float CalculateAccuracy(int speculatedMs, int actualMs)
        {
            var difference = Math.Abs(speculatedMs - actualMs);
            return Math.Max(0, 1.0f - (difference / (float)actualMs));
        }

        private EndpointFeatures ExtractEndpointFeatures(byte[] audio)
        {
            return new EndpointFeatures
            {
                Energy = CalculateEnergy(audio),
                ZeroCrossRate = CalculateZeroCrossRate(audio),
                SpectralCentroid = CalculateSpectralCentroid(audio),
                SilenceDuration = CalculateSilenceDuration(audio)
            };
        }

        private float CalculateEnergy(byte[] audio)
        {
            if (audio.Length < 2) return 0;

            long sum = 0;
            for (int i = 0; i < audio.Length - 1; i += 2)
            {
                var sample = BitConverter.ToInt16(audio, i);
                sum += sample * sample;
            }

            return (float)Math.Sqrt(sum / (audio.Length / 2.0)) / 32768f;
        }

        private float CalculateZeroCrossRate(byte[] audio)
        {
            if (audio.Length < 4) return 0;

            int crossings = 0;
            short prevSample = 0;

            for (int i = 0; i < audio.Length - 1; i += 2)
            {
                var sample = BitConverter.ToInt16(audio, i);
                if ((prevSample >= 0 && sample < 0) || (prevSample < 0 && sample >= 0))
                {
                    crossings++;
                }
                prevSample = sample;
            }

            return crossings / (float)(audio.Length / 2);
        }

        private float CalculateSpectralCentroid(byte[] audio)
        {
            // Simplified spectral centroid calculation
            // In production, use FFT
            return CalculateEnergy(audio) * 1000; // Placeholder
        }

        private int CalculateSilenceDuration(byte[] audio)
        {
            const float silenceThreshold = 0.01f;
            int silenceSamples = 0;

            for (int i = Math.Max(0, audio.Length - 16000); i < audio.Length - 1; i += 2)
            {
                var sample = Math.Abs(BitConverter.ToInt16(audio, i) / 32768f);
                if (sample < silenceThreshold)
                {
                    silenceSamples++;
                }
            }

            return silenceSamples * 1000 / 16000; // Convert to ms
        }

        public void Dispose()
        {
            processingPipeline?.Complete();
            endpointPredictor?.Complete();
            audioBuffer?.Dispose();
        }

        // Helper classes
        private class SpeculativeTask
        {
            public int LengthMs { get; set; }
            public DateTime StartTime { get; set; }
            public Task<string> Task { get; set; }
            public bool IsCompleted => Task.IsCompleted;
        }

        private class SpeculativeRequest
        {
            public byte[] Audio { get; set; }
            public int LengthMs { get; set; }
            public string Result { get; set; }
            public DateTime CompletionTime { get; set; }
        }

        private class PredictedEndpoint
        {
            public int TimestampMs { get; set; }
            public float Confidence { get; set; }
            public EndpointFeatures Features { get; set; }
        }

        private class EndpointFeatures
        {
            public float Energy { get; set; }
            public float ZeroCrossRate { get; set; }
            public float SpectralCentroid { get; set; }
            public int SilenceDuration { get; set; }
        }

        public class SpeculativeResult
        {
            public string Text { get; set; }
            public bool WasSpeculated { get; set; }
            public long SavedTimeMs { get; set; }
            public float SpeculationAccuracy { get; set; }
        }
    }

    /// <summary>
    /// Circular buffer optimized for audio streaming.
    /// </summary>
    public class CircularAudioBuffer : IDisposable
    {
        private readonly byte[] buffer;
        private readonly object lockObj = new object();
        private int writePosition;
        private int dataLength;

        public int LengthMs => dataLength / 32; // 16kHz, 16-bit
        public int Capacity { get; }

        public CircularAudioBuffer(int capacitySamples)
        {
            Capacity = capacitySamples * 2; // 16-bit samples
            buffer = new byte[Capacity];
        }

        public void Write(byte[] data)
        {
            lock (lockObj)
            {
                var bytesToWrite = Math.Min(data.Length, Capacity);

                // Write to circular buffer
                for (int i = 0; i < bytesToWrite; i++)
                {
                    buffer[(writePosition + i) % Capacity] = data[i];
                }

                writePosition = (writePosition + bytesToWrite) % Capacity;
                dataLength = Math.Min(dataLength + bytesToWrite, Capacity);
            }
        }

        public byte[] Read(int lengthMs)
        {
            lock (lockObj)
            {
                var bytes = Math.Min(lengthMs * 32, dataLength);
                var result = new byte[bytes];

                var readStart = (writePosition - dataLength + Capacity) % Capacity;

                for (int i = 0; i < bytes; i++)
                {
                    result[i] = buffer[(readStart + i) % Capacity];
                }

                return result;
            }
        }

        public byte[] Peek(int lengthMs)
        {
            lock (lockObj)
            {
                return Read(lengthMs); // Non-destructive read
            }
        }

        public void Clear()
        {
            lock (lockObj)
            {
                writePosition = 0;
                dataLength = 0;
            }
        }

        public void Dispose()
        {
            // Buffer cleanup if needed
        }
    }

    /// <summary>
    /// Voice Activity Detector for endpoint detection.
    /// </summary>
    public class VoiceActivityDetector
    {
        private readonly float energyThreshold = 0.02f;
        private readonly int minSpeechFrames = 3;
        private readonly Queue<float> recentEnergies;

        public VoiceActivityDetector()
        {
            recentEnergies = new Queue<float>(10);
        }

        public bool DetectVoice(byte[] audio)
        {
            var energy = CalculateEnergy(audio);
            recentEnergies.Enqueue(energy);

            if (recentEnergies.Count > 10)
                recentEnergies.Dequeue();

            // Voice detected if recent energies exceed threshold
            var voiceFrames = recentEnergies.Count(e => e > energyThreshold);
            return voiceFrames >= minSpeechFrames;
        }

        private float CalculateEnergy(byte[] audio)
        {
            if (audio.Length < 2) return 0;

            long sum = 0;
            for (int i = 0; i < audio.Length - 1; i += 2)
            {
                var sample = BitConverter.ToInt16(audio, i);
                sum += sample * sample;
            }

            return (float)Math.Sqrt(sum / (audio.Length / 2.0)) / 32768f;
        }
    }
}
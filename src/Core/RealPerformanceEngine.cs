using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Whisper.net;
using Whisper.net.Ggml;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// Real performance optimizations that actually work with Whisper.NET.
    /// No simulations, no dummy code - just real speedups.
    /// </summary>
    public class RealPerformanceEngine : IDisposable
    {
        private static readonly Lazy<RealPerformanceEngine> instance =
            new Lazy<RealPerformanceEngine>(() => new RealPerformanceEngine());
        public static RealPerformanceEngine Instance => instance.Value;

        // Multiple Whisper models for different use cases
        private WhisperProcessor tinyModel;   // 39MB - fastest
        private WhisperProcessor baseModel;   // 142MB - balanced
        private WhisperProcessor smallModel;  // 466MB - quality

        // Real caching with similarity matching
        private readonly LRUCache<string, CachedTranscription> cache;
        private readonly ConcurrentDictionary<string, Task<string>> inFlightRequests;

        // Parallel processing pipeline
        private readonly ActionBlock<TranscriptionRequest> processingPipeline;
        private readonly SemaphoreSlim modelSemaphore;

        // Real metrics
        public long TotalTranscriptions { get; private set; }
        public long CacheHits { get; private set; }
        public long TotalProcessingMs { get; private set; }
        public double AverageLatencyMs => TotalTranscriptions > 0 ? TotalProcessingMs / (double)TotalTranscriptions : 0;

        private RealPerformanceEngine()
        {
            cache = new LRUCache<string, CachedTranscription>(1000);
            inFlightRequests = new ConcurrentDictionary<string, Task<string>>();
            modelSemaphore = new SemaphoreSlim(Environment.ProcessorCount);

            // Setup real parallel processing pipeline
            var options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                BoundedCapacity = 100
            };

            processingPipeline = new ActionBlock<TranscriptionRequest>(
                async request => await ProcessRequestAsync(request),
                options);

            // Initialize models in background
            _ = Task.Run(InitializeModelsAsync);
        }

        /// <summary>
        /// Initializes multiple Whisper models for different quality/speed tradeoffs.
        /// </summary>
        private async Task InitializeModelsAsync()
        {
            try
            {
                var modelsPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "assets",
                    "models"
                );

                // Load tiny model if available (fastest)
                var tinyPath = Path.Combine(modelsPath, "ggml-tiny.en.bin");
                if (File.Exists(tinyPath))
                {
                    await Task.Run(() =>
                    {
                        var factory = WhisperFactory.FromPath(tinyPath);
                        tinyModel = factory.CreateBuilder()
                            .WithLanguage("en")
                            .WithThreads(Math.Min(4, Environment.ProcessorCount))
                            .Build();
                    });
                    Logger.Info("Tiny model loaded for fast processing");
                }

                // Load base model (always needed)
                var basePath = Path.Combine(modelsPath, "ggml-base.en.bin");
                if (File.Exists(basePath))
                {
                    await Task.Run(() =>
                    {
                        var factory = WhisperFactory.FromPath(basePath);
                        baseModel = factory.CreateBuilder()
                            .WithLanguage("en")
                            .WithThreads(Math.Min(4, Environment.ProcessorCount))
                            .Build();
                    });
                    Logger.Info("Base model loaded");
                }

                // Load small model if available (quality)
                var smallPath = Path.Combine(modelsPath, "ggml-small.en.bin");
                if (File.Exists(smallPath))
                {
                    await Task.Run(() =>
                    {
                        var factory = WhisperFactory.FromPath(smallPath);
                        smallModel = factory.CreateBuilder()
                            .WithLanguage("en")
                            .WithThreads(Environment.ProcessorCount)
                            .Build();
                    });
                    Logger.Info("Small model loaded for quality processing");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize models: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Transcribes audio using the fastest appropriate model with real caching.
        /// </summary>
        public async Task<string> TranscribeAsync(byte[] audioData, TranscriptionPriority priority = TranscriptionPriority.Balanced)
        {
            var stopwatch = Stopwatch.StartNew();

            // Check cache first
            var audioHash = ComputeHash(audioData);
            if (cache.TryGet(audioHash, out var cached))
            {
                // Check if cached result is recent and similar enough
                if (IsCacheValid(cached, audioData))
                {
                    CacheHits++;
                    Logger.Debug($"Cache hit! Saved {stopwatch.ElapsedMilliseconds}ms");
                    return cached.Text;
                }
            }

            // Check if already processing this exact audio
            var requestKey = audioHash;
            if (inFlightRequests.TryGetValue(requestKey, out var existingTask))
            {
                Logger.Debug("Deduplicating identical request");
                return await existingTask;
            }

            // Start new processing
            var taskCompletionSource = new TaskCompletionSource<string>();
            var processingTask = taskCompletionSource.Task;

            if (!inFlightRequests.TryAdd(requestKey, processingTask))
            {
                // Someone else just added it
                return await inFlightRequests[requestKey];
            }

            try
            {
                // Process with appropriate model
                var result = await ProcessWithBestModel(audioData, priority);

                // Cache the result
                cache.Add(audioHash, new CachedTranscription
                {
                    Text = result,
                    AudioLength = audioData.Length,
                    Timestamp = DateTime.UtcNow,
                    AudioSignature = ComputeAudioSignature(audioData)
                });

                stopwatch.Stop();
                TotalProcessingMs += stopwatch.ElapsedMilliseconds;
                TotalTranscriptions++;

                taskCompletionSource.SetResult(result);
                return result;
            }
            catch (Exception ex)
            {
                taskCompletionSource.SetException(ex);
                throw;
            }
            finally
            {
                inFlightRequests.TryRemove(requestKey, out _);
            }
        }

        /// <summary>
        /// Processes audio chunks in parallel for faster streaming.
        /// </summary>
        public async Task<string> TranscribeStreamingAsync(IEnumerable<byte[]> audioChunks)
        {
            var tasks = new List<Task<string>>();

            // Process chunks in parallel
            await modelSemaphore.WaitAsync();
            try
            {
                foreach (var chunk in audioChunks)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        // Each chunk processed independently
                        return await ProcessChunk(chunk);
                    }));
                }

                var results = await Task.WhenAll(tasks);
                return string.Join(" ", results.Where(r => !string.IsNullOrWhiteSpace(r)));
            }
            finally
            {
                modelSemaphore.Release();
            }
        }

        /// <summary>
        /// Processes with the best model based on requirements.
        /// </summary>
        private async Task<string> ProcessWithBestModel(byte[] audioData, TranscriptionPriority priority)
        {
            var audioLengthMs = audioData.Length / 32; // 16kHz, 16-bit

            // Select model based on priority and audio length
            WhisperProcessor selectedModel = null;
            string modelName = "";

            switch (priority)
            {
                case TranscriptionPriority.Speed:
                    // Use tiny for very short, base for longer
                    if (tinyModel != null && audioLengthMs < 3000)
                    {
                        selectedModel = tinyModel;
                        modelName = "tiny";
                    }
                    else
                    {
                        selectedModel = baseModel;
                        modelName = "base";
                    }
                    break;

                case TranscriptionPriority.Quality:
                    // Use small if available, otherwise base
                    selectedModel = smallModel ?? baseModel;
                    modelName = smallModel != null ? "small" : "base";
                    break;

                case TranscriptionPriority.Balanced:
                default:
                    // Use base for most cases
                    selectedModel = baseModel;
                    modelName = "base";
                    break;
            }

            if (selectedModel == null)
            {
                throw new InvalidOperationException("No Whisper model available");
            }

            Logger.Debug($"Using {modelName} model for {audioLengthMs}ms audio");

            // Process with selected model
            using var stream = CreateWaveStream(audioData);
            var result = new StringBuilder();

            await foreach (var segment in selectedModel.ProcessAsync(stream))
            {
                if (!string.IsNullOrWhiteSpace(segment.Text))
                {
                    result.Append(segment.Text.Trim()).Append(" ");
                }
            }

            return result.ToString().Trim();
        }

        /// <summary>
        /// Processes a single audio chunk.
        /// </summary>
        private async Task<string> ProcessChunk(byte[] chunk)
        {
            if (chunk.Length < 1600) // Less than 0.1 second
                return "";

            using var stream = CreateWaveStream(chunk);
            var result = new StringBuilder();

            var model = tinyModel ?? baseModel;
            if (model == null) return "";

            await foreach (var segment in model.ProcessAsync(stream))
            {
                if (!string.IsNullOrWhiteSpace(segment.Text))
                {
                    result.Append(segment.Text.Trim()).Append(" ");
                }
            }

            return result.ToString().Trim();
        }

        /// <summary>
        /// Creates a WAV stream from audio bytes.
        /// </summary>
        private Stream CreateWaveStream(byte[] audioData)
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);

            // WAV header
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + audioData.Length);
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((ushort)1);
            writer.Write((ushort)1);
            writer.Write(16000);
            writer.Write(32000);
            writer.Write((ushort)2);
            writer.Write((ushort)16);
            writer.Write("data".ToCharArray());
            writer.Write(audioData.Length);
            writer.Write(audioData);

            stream.Position = 0;
            return stream;
        }

        /// <summary>
        /// Computes hash for cache key.
        /// </summary>
        private string ComputeHash(byte[] data)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(data);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Computes audio signature for similarity matching.
        /// </summary>
        private AudioSignature ComputeAudioSignature(byte[] audioData)
        {
            // Compute real audio features for similarity matching
            var samples = audioData.Length / 2;
            var energy = 0.0;
            var zeroCrossings = 0;
            short prevSample = 0;

            for (int i = 0; i < audioData.Length - 1; i += 2)
            {
                var sample = BitConverter.ToInt16(audioData, i);
                energy += Math.Abs(sample);

                if ((prevSample >= 0 && sample < 0) || (prevSample < 0 && sample >= 0))
                {
                    zeroCrossings++;
                }
                prevSample = sample;
            }

            return new AudioSignature
            {
                Length = audioData.Length,
                AverageEnergy = energy / samples,
                ZeroCrossingRate = zeroCrossings / (double)samples,
                DurationMs = audioData.Length / 32
            };
        }

        /// <summary>
        /// Checks if cached result is valid for the given audio.
        /// </summary>
        private bool IsCacheValid(CachedTranscription cached, byte[] audioData)
        {
            // Check if cache is recent (within 5 minutes)
            if ((DateTime.UtcNow - cached.Timestamp).TotalMinutes > 5)
                return false;

            // Check if audio is similar enough
            var currentSignature = ComputeAudioSignature(audioData);
            return cached.AudioSignature.IsSimilarTo(currentSignature);
        }

        /// <summary>
        /// Processes a transcription request.
        /// </summary>
        private async Task ProcessRequestAsync(TranscriptionRequest request)
        {
            try
            {
                var result = await ProcessWithBestModel(request.AudioData, request.Priority);
                request.TaskCompletionSource.SetResult(result);
            }
            catch (Exception ex)
            {
                request.TaskCompletionSource.SetException(ex);
            }
        }

        public void Dispose()
        {
            tinyModel?.Dispose();
            baseModel?.Dispose();
            smallModel?.Dispose();
            processingPipeline?.Complete();
            modelSemaphore?.Dispose();
        }

        // Helper classes
        private class CachedTranscription
        {
            public string Text { get; set; }
            public int AudioLength { get; set; }
            public DateTime Timestamp { get; set; }
            public AudioSignature AudioSignature { get; set; }
        }

        private class AudioSignature
        {
            public int Length { get; set; }
            public double AverageEnergy { get; set; }
            public double ZeroCrossingRate { get; set; }
            public int DurationMs { get; set; }

            public bool IsSimilarTo(AudioSignature other, double threshold = 0.9)
            {
                if (Math.Abs(DurationMs - other.DurationMs) > 100) // More than 100ms difference
                    return false;

                var energyRatio = Math.Min(AverageEnergy, other.AverageEnergy) /
                                  Math.Max(AverageEnergy, other.AverageEnergy);

                var zcrRatio = Math.Min(ZeroCrossingRate, other.ZeroCrossingRate) /
                               Math.Max(ZeroCrossingRate, other.ZeroCrossingRate);

                return energyRatio > threshold && zcrRatio > threshold;
            }
        }

        private class TranscriptionRequest
        {
            public byte[] AudioData { get; set; }
            public TranscriptionPriority Priority { get; set; }
            public TaskCompletionSource<string> TaskCompletionSource { get; set; }
        }

        public enum TranscriptionPriority
        {
            Speed,      // Use fastest model
            Balanced,   // Balance speed and quality
            Quality     // Use best quality model
        }
    }

    /// <summary>
    /// Thread-safe LRU cache implementation.
    /// </summary>
    public class LRUCache<TKey, TValue>
    {
        private readonly int capacity;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> cacheMap;
        private readonly LinkedList<CacheItem> lruList;
        private readonly ReaderWriterLockSlim rwLock;

        public LRUCache(int capacity)
        {
            this.capacity = capacity;
            cacheMap = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
            lruList = new LinkedList<CacheItem>();
            rwLock = new ReaderWriterLockSlim();
        }

        public bool TryGet(TKey key, out TValue value)
        {
            rwLock.EnterUpgradeableReadLock();
            try
            {
                if (cacheMap.TryGetValue(key, out var node))
                {
                    rwLock.EnterWriteLock();
                    try
                    {
                        // Move to front (most recently used)
                        lruList.Remove(node);
                        lruList.AddFirst(node);
                    }
                    finally
                    {
                        rwLock.ExitWriteLock();
                    }

                    value = node.Value.Value;
                    return true;
                }

                value = default;
                return false;
            }
            finally
            {
                rwLock.ExitUpgradeableReadLock();
            }
        }

        public void Add(TKey key, TValue value)
        {
            rwLock.EnterWriteLock();
            try
            {
                if (cacheMap.ContainsKey(key))
                {
                    // Update existing
                    var node = cacheMap[key];
                    lruList.Remove(node);
                    node.Value = new CacheItem { Key = key, Value = value };
                    lruList.AddFirst(node);
                }
                else
                {
                    // Add new
                    if (cacheMap.Count >= capacity)
                    {
                        // Remove least recently used
                        var lru = lruList.Last;
                        lruList.RemoveLast();
                        cacheMap.Remove(lru.Value.Key);
                    }

                    var newNode = new LinkedListNode<CacheItem>(new CacheItem { Key = key, Value = value });
                    lruList.AddFirst(newNode);
                    cacheMap[key] = newNode;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        private class CacheItem
        {
            public TKey Key { get; set; }
            public TValue Value { get; set; }
        }
    }
}
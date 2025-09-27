using System;
using System.Threading.Tasks;
using SuperWhisperWPF.Core;

namespace SuperWhisperWPF
{
    /// <summary>
    /// Adaptive Whisper engine that tries research-optimized OptimizedWhisperEngine first,
    /// then falls back to regular WhisperEngine if initialization fails.
    /// Provides transparent performance optimization with safety guarantees.
    /// </summary>
    public class AdaptiveWhisperEngine : IDisposable
    {
        private DeepgramEngine deepgramEngine;
        private DeepgramStreamingEngine streamingEngine;
        private OptimizedWhisperEngine optimizedEngine;
        private WhisperEngine fallbackEngine;
        private bool isDeepgramEnabled = false;
        private bool isStreamingEnabled = false;
        private bool isOptimizedEnabled = false;
        private bool isInitialized = false;
        private readonly object lockObject = new object();

        public async Task<bool> InitializeAsync()
        {
            lock (lockObject)
            {
                if (isInitialized)
                {
                    return true;
                }
            }

            Logger.Info("AdaptiveWhisperEngine: Starting initialization...");

            // Try Deepgram first if API key is available (for testing sub-300ms latency)
            if (Environment.GetEnvironmentVariable("DEEPGRAM_API_KEY") != null)
            {
                try
                {
                    Logger.Info("AdaptiveWhisperEngine: Attempting DeepgramEngine initialization for sub-300ms latency test...");
                    deepgramEngine = new DeepgramEngine();

                    var deepgramSuccess = await deepgramEngine.InitializeAsync();
                    if (deepgramSuccess)
                    {
                        Logger.Info($"✅ DeepgramEngine initialized successfully - expecting sub-300ms latency!");
                        isDeepgramEnabled = true;
                        isInitialized = true;
                        return true;
                    }
                    else
                    {
                        Logger.Warning("DeepgramEngine initialization failed, trying local engines");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"DeepgramEngine failed: {ex.Message}, trying local engines");
                }
            }

            try
            {
                // Try OptimizedWhisperEngine second
                Logger.Info("AdaptiveWhisperEngine: Attempting OptimizedWhisperEngine initialization...");
                optimizedEngine = OptimizedWhisperEngine.Instance;

                var success = await optimizedEngine.InitializeAsync();
                if (success)
                {
                    Logger.Info($"✅ OptimizedWhisperEngine initialized successfully with GPU: {optimizedEngine.HasGpuAcceleration}");
                    isOptimizedEnabled = true;
                    isInitialized = true;
                    return true;
                }
                else
                {
                    Logger.Warning("OptimizedWhisperEngine initialization failed, falling back to regular engine");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"OptimizedWhisperEngine failed with exception: {ex.Message}, falling back to regular engine");
            }

            // Fallback to regular WhisperEngine
            try
            {
                Logger.Info("AdaptiveWhisperEngine: Initializing fallback WhisperEngine...");
                fallbackEngine = new WhisperEngine();
                var fallbackSuccess = await fallbackEngine.InitializeAsync();

                if (fallbackSuccess)
                {
                    Logger.Info("✅ Fallback WhisperEngine initialized successfully");
                    isOptimizedEnabled = false;
                    isInitialized = true;
                    return true;
                }
                else
                {
                    Logger.Error("❌ Both OptimizedWhisperEngine and fallback WhisperEngine failed to initialize");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"❌ Fallback WhisperEngine initialization failed: {ex.Message}");
                return false;
            }
        }

        public async Task<string> TranscribeAsync(byte[] audioData)
        {
            if (!isInitialized)
            {
                Logger.Warning("AdaptiveWhisperEngine not initialized, attempting to initialize...");
                if (!await InitializeAsync())
                {
                    throw new Exception("AdaptiveWhisperEngine initialization failed");
                }
            }

            try
            {
                if (isDeepgramEnabled && deepgramEngine != null)
                {
                    Logger.Debug("AdaptiveWhisperEngine: Using DeepgramEngine for cloud-based sub-300ms transcription");
                    return await deepgramEngine.TranscribeAsync(audioData);
                }
                else if (isOptimizedEnabled && optimizedEngine != null)
                {
                    Logger.Debug("AdaptiveWhisperEngine: Using OptimizedWhisperEngine for transcription");
                    return await optimizedEngine.TranscribeAsync(audioData);
                }
                else if (fallbackEngine != null)
                {
                    Logger.Debug("AdaptiveWhisperEngine: Using fallback WhisperEngine for transcription");
                    return await fallbackEngine.TranscribeAsync(audioData);
                }
                else
                {
                    throw new Exception("No engine available for transcription");
                }
            }
            catch (Exception ex)
            {
                // If optimized engine fails during transcription, try fallback
                if (isOptimizedEnabled && fallbackEngine == null)
                {
                    Logger.Warning($"OptimizedWhisperEngine failed during transcription: {ex.Message}, initializing fallback");
                    try
                    {
                        fallbackEngine = new WhisperEngine();
                        if (await fallbackEngine.InitializeAsync())
                        {
                            Logger.Info("Fallback WhisperEngine initialized successfully after optimized failure");
                            isOptimizedEnabled = false;
                            return await fallbackEngine.TranscribeAsync(audioData);
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        Logger.Error($"Fallback initialization failed: {fallbackEx.Message}");
                    }
                }

                Logger.Error($"AdaptiveWhisperEngine transcription failed: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            lock (lockObject)
            {
                if (deepgramEngine != null)
                {
                    Logger.Info("Disposing DeepgramEngine...");
                    deepgramEngine.Dispose();
                    deepgramEngine = null;
                }

                if (optimizedEngine != null)
                {
                    Logger.Info("Disposing OptimizedWhisperEngine...");
                    optimizedEngine.Dispose();
                    optimizedEngine = null;
                }

                if (fallbackEngine != null)
                {
                    Logger.Info("Disposing fallback WhisperEngine...");
                    fallbackEngine.Dispose();
                    fallbackEngine = null;
                }

                isInitialized = false;
                isDeepgramEnabled = false;
                isOptimizedEnabled = false;
                Logger.Info("AdaptiveWhisperEngine disposed");
            }
        }

        /// <summary>
        /// Gets the current engine type being used
        /// </summary>
        public string CurrentEngineType => isDeepgramEnabled ? "Deepgram (Cloud)" : isOptimizedEnabled ? "Optimized" : "Regular";

        /// <summary>
        /// Gets the GPU mode if using optimized engine
        /// </summary>
        public string GpuMode => isOptimizedEnabled && optimizedEngine != null && optimizedEngine.HasGpuAcceleration ? "CUDA" : "None";

        /// <summary>
        /// Gets performance metrics from the active engine
        /// </summary>
        public string PerformanceStats =>
            isDeepgramEnabled && deepgramEngine != null ?
                $"Deepgram - Avg: {deepgramEngine.AverageLatency:F1}ms, Total: {deepgramEngine.TotalTranscriptions}" :
            isOptimizedEnabled && optimizedEngine != null ?
                $"Local - Avg: {optimizedEngine.AverageLatency:F1}ms, Total: {optimizedEngine.TotalTranscriptions}" :
                "N/A";
    }
}
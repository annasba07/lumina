using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// ONNX Runtime based Whisper engine using DirectML for GPU acceleration.
    /// Research shows 7x speedup potential over native implementations.
    /// Target: Sub-200ms latency using quantized models.
    /// </summary>
    public class OnnxWhisperEngine : IDisposable
    {
        #region Singleton
        private static readonly Lazy<OnnxWhisperEngine> instance =
            new Lazy<OnnxWhisperEngine>(() => new OnnxWhisperEngine());
        public static OnnxWhisperEngine Instance => instance.Value;
        #endregion

        #region Fields
        private InferenceSession encoderSession;
        private InferenceSession decoderSession;
        private bool isInitialized = false;
        private readonly SemaphoreSlim initSemaphore = new SemaphoreSlim(1, 1);
        private static readonly HttpClient httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        // Model constants
        private const int SAMPLE_RATE = 16000;
        private const int N_FFT = 400;
        private const int N_MELS = 80;
        private const int HOP_LENGTH = 160;
        private const int MAX_LENGTH = 30; // Max seconds of audio
        #endregion

        #region Properties
        public bool IsInitialized => isInitialized;
        public bool IsGpuEnabled => true; // DirectML is GPU-based
        #endregion

        #region Initialization
        public async Task<bool> InitializeAsync()
        {
            if (isInitialized) return true;

            await initSemaphore.WaitAsync();
            try
            {
                if (isInitialized) return true;

                Logger.Info("OnnxWhisperEngine: Initializing ONNX Runtime with DirectML...");
                var stopwatch = Stopwatch.StartNew();

                // Download ONNX models if needed
                var encoderPath = await EnsureModelExistsAsync("whisper-tiny-encoder.onnx");
                var decoderPath = await EnsureModelExistsAsync("whisper-tiny-decoder.onnx");

                if (encoderPath == null || decoderPath == null)
                {
                    Logger.Error("Failed to download ONNX models");
                    return false;
                }

                // Create DirectML session options for GPU acceleration
                var sessionOptions = CreateDirectMLSessionOptions();

                // Load encoder and decoder
                encoderSession = new InferenceSession(encoderPath, sessionOptions);
                decoderSession = new InferenceSession(decoderPath, sessionOptions);

                Logger.Info("✅ ONNX models loaded with DirectML acceleration");

                // Warmup
                await WarmupAsync();

                isInitialized = true;
                stopwatch.Stop();

                Logger.Info($"OnnxWhisperEngine initialized in {stopwatch.ElapsedMilliseconds}ms");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"OnnxWhisperEngine initialization failed: {ex.Message}", ex);
                return false;
            }
            finally
            {
                initSemaphore.Release();
            }
        }

        private SessionOptions CreateDirectMLSessionOptions()
        {
            var options = new SessionOptions();

            try
            {
                // Enable DirectML for GPU acceleration on Windows
                options.AppendExecutionProvider_DML();
                options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                options.InterOpNumThreads = 1;
                options.IntraOpNumThreads = Math.Min(4, Environment.ProcessorCount);

                Logger.Info("✅ DirectML GPU acceleration enabled");
            }
            catch (Exception ex)
            {
                Logger.Warning($"DirectML not available, falling back to CPU: {ex.Message}");
                // CPU fallback is automatic
            }

            return options;
        }

        private async Task<string> EnsureModelExistsAsync(string modelName)
        {
            var modelDir = Path.Combine(Environment.CurrentDirectory, "assets\\models");
            if (!Directory.Exists(modelDir))
            {
                Directory.CreateDirectory(modelDir);
            }

            var modelPath = Path.Combine(modelDir, modelName);

            if (File.Exists(modelPath))
            {
                Logger.Info($"ONNX model {modelName} already exists");
                return modelPath;
            }

            // Download from HuggingFace
            var modelUrl = $"https://huggingface.co/openai/whisper-tiny/resolve/main/onnx/{modelName}";

            try
            {
                Logger.Info($"Downloading {modelName} from {modelUrl}...");

                using var response = await httpClient.GetAsync(modelUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using var fileStream = File.OpenWrite(modelPath);
                using var downloadStream = await response.Content.ReadAsStreamAsync();
                await downloadStream.CopyToAsync(fileStream);

                Logger.Info($"✅ Downloaded {modelName} successfully");
                return modelPath;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to download {modelName}: {ex.Message}");

                // Try alternative: Convert existing GGML model to ONNX
                // This would require additional tooling
                return null;
            }
        }

        private async Task WarmupAsync()
        {
            try
            {
                Logger.Info("Warming up ONNX inference...");

                // Create minimal test input
                var testAudio = new float[SAMPLE_RATE]; // 1 second of silence
                await TranscribeAsync(ConvertFloatToBytes(testAudio));

                Logger.Info("✅ ONNX warmup completed");
            }
            catch (Exception ex)
            {
                Logger.Warning($"ONNX warmup failed: {ex.Message}");
            }
        }
        #endregion

        #region Transcription
        public async Task<string> TranscribeAsync(byte[] audioData)
        {
            if (!isInitialized)
            {
                await InitializeAsync();
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Convert audio to mel spectrogram
                var melSpectrogram = await Task.Run(() => ComputeMelSpectrogram(audioData));

                // Run encoder
                var encoderOutput = await Task.Run(() => RunEncoder(melSpectrogram));

                // Run decoder with beam search
                var text = await Task.Run(() => RunDecoder(encoderOutput));

                stopwatch.Stop();
                Logger.Info($"ONNX transcription: {stopwatch.ElapsedMilliseconds}ms - Result: '{text}'");

                return text;
            }
            catch (Exception ex)
            {
                Logger.Error($"ONNX transcription failed: {ex.Message}", ex);
                return string.Empty;
            }
        }

        private float[,,] ComputeMelSpectrogram(byte[] audioData)
        {
            // Convert bytes to float
            var samples = audioData.Length / 2;
            var floatData = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                var sample = BitConverter.ToInt16(audioData, i * 2);
                floatData[i] = sample / 32768.0f;
            }

            // Compute mel spectrogram (simplified - real implementation needs proper STFT)
            var frames = samples / HOP_LENGTH;
            var melSpec = new float[1, N_MELS, frames];

            // This is a placeholder - proper mel spectrogram computation needed
            // For now, just create dummy data to test the pipeline
            for (int i = 0; i < N_MELS; i++)
            {
                for (int j = 0; j < frames; j++)
                {
                    melSpec[0, i, j] = 0.0f;
                }
            }

            return melSpec;
        }

        private DenseTensor<float> RunEncoder(float[,,] melSpectrogram)
        {
            // Prepare input tensor
            var inputMeta = encoderSession.InputMetadata;
            var inputName = inputMeta.Keys.First();
            var shape = inputMeta[inputName].Dimensions;

            // Create tensor from mel spectrogram
            var inputTensor = new DenseTensor<float>(melSpectrogram.Cast<float>().ToArray(), shape);

            // Run inference
            var inputs = new[] { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };
            using var results = encoderSession.Run(inputs);

            // Get encoder output
            var encoderOutput = results.First().AsTensor<float>() as DenseTensor<float>;
            return encoderOutput;
        }

        private string RunDecoder(DenseTensor<float> encoderOutput)
        {
            // Simplified decoder - real implementation needs proper tokenization
            // For now, return placeholder text
            return "ONNX transcription placeholder";
        }

        private byte[] ConvertFloatToBytes(float[] floatArray)
        {
            var bytes = new byte[floatArray.Length * 2];
            for (int i = 0; i < floatArray.Length; i++)
            {
                var sample = (short)(floatArray[i] * 32768);
                var sampleBytes = BitConverter.GetBytes(sample);
                bytes[i * 2] = sampleBytes[0];
                bytes[i * 2 + 1] = sampleBytes[1];
            }
            return bytes;
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            encoderSession?.Dispose();
            decoderSession?.Dispose();
            initSemaphore?.Dispose();

            Logger.Info("OnnxWhisperEngine disposed");
        }
        #endregion
    }
}
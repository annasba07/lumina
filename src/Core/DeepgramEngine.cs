using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// Deepgram API integration for ultra-low latency transcription (sub-300ms).
    /// This is a proof of concept to demonstrate achievable latency with cloud APIs.
    /// </summary>
    public class DeepgramEngine : IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly string apiKey;
        private bool isInitialized = false;

        // Performance tracking
        private long totalTranscriptions = 0;
        private double averageLatency = 0;

        public bool IsInitialized => isInitialized;
        public double AverageLatency => averageLatency;
        public long TotalTranscriptions => totalTranscriptions;

        public DeepgramEngine()
        {
            httpClient = new HttpClient();
            // You'll need to set this API key - get a free one from deepgram.com
            apiKey = Environment.GetEnvironmentVariable("DEEPGRAM_API_KEY") ?? "YOUR_API_KEY_HERE";

            if (!string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_API_KEY_HERE")
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Token", apiKey);
                httpClient.Timeout = TimeSpan.FromSeconds(10);
            }
        }

        public async Task<bool> InitializeAsync()
        {
            if (isInitialized) return true;

            try
            {
                Logger.Info("DeepgramEngine: Initializing cloud API connection...");

                if (apiKey == "YOUR_API_KEY_HERE")
                {
                    Logger.Warning("DeepgramEngine: API key not set. Set DEEPGRAM_API_KEY environment variable.");
                    Logger.Info("Get a free API key at: https://console.deepgram.com/signup");
                    return false;
                }

                // Test the API connection
                var testUrl = "https://api.deepgram.com/v1/projects";
                var response = await httpClient.GetAsync(testUrl);

                if (response.IsSuccessStatusCode)
                {
                    isInitialized = true;
                    Logger.Info("✅ DeepgramEngine initialized successfully");
                    return true;
                }
                else
                {
                    Logger.Error($"DeepgramEngine initialization failed: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"DeepgramEngine initialization error: {ex.Message}");
                return false;
            }
        }

        public async Task<string> TranscribeAsync(byte[] audioData)
        {
            if (!isInitialized)
            {
                Logger.Warning("DeepgramEngine not initialized");
                return string.Empty;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                Logger.Debug($"DeepgramEngine: Processing {audioData.Length} bytes");

                // Convert PCM to WAV format (Deepgram needs WAV headers)
                var wavData = CreateWavFromPcm(audioData, 16000, 1, 16);

                // Deepgram API endpoint with Nova-2 model for fastest performance
                var url = "https://api.deepgram.com/v1/listen?" +
                         "model=nova-2&" +        // Fastest model
                         "language=en&" +         // English
                         "smart_format=true&" +   // Better formatting
                         "punctuate=true&" +      // Add punctuation
                         "profanity_filter=false";

                var content = new ByteArrayContent(wavData);
                content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

                // Send to Deepgram API
                var response = await httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonDocument.Parse(json);

                    // Extract transcript from response
                    var transcript = result.RootElement
                        .GetProperty("results")
                        .GetProperty("channels")[0]
                        .GetProperty("alternatives")[0]
                        .GetProperty("transcript")
                        .GetString() ?? string.Empty;

                    stopwatch.Stop();
                    RecordLatency(stopwatch.ElapsedMilliseconds);

                    Logger.Info($"DeepgramEngine transcription: {stopwatch.ElapsedMilliseconds}ms, Result: '{transcript}'");
                    return transcript;
                }
                else
                {
                    Logger.Error($"Deepgram API error: {response.StatusCode}");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"DeepgramEngine transcription error: {ex.Message}");
                return string.Empty;
            }
        }

        private byte[] CreateWavFromPcm(byte[] pcmData, int sampleRate, int channels, int bitsPerSample)
        {
            var wavData = new byte[44 + pcmData.Length];
            var byteRate = sampleRate * channels * bitsPerSample / 8;
            var blockAlign = channels * bitsPerSample / 8;

            // RIFF header
            Encoding.ASCII.GetBytes("RIFF").CopyTo(wavData, 0);
            BitConverter.GetBytes(36 + pcmData.Length).CopyTo(wavData, 4);
            Encoding.ASCII.GetBytes("WAVE").CopyTo(wavData, 8);

            // fmt chunk
            Encoding.ASCII.GetBytes("fmt ").CopyTo(wavData, 12);
            BitConverter.GetBytes(16).CopyTo(wavData, 16); // Subchunk1Size
            BitConverter.GetBytes((short)1).CopyTo(wavData, 20); // AudioFormat (PCM)
            BitConverter.GetBytes((short)channels).CopyTo(wavData, 22);
            BitConverter.GetBytes(sampleRate).CopyTo(wavData, 24);
            BitConverter.GetBytes(byteRate).CopyTo(wavData, 28);
            BitConverter.GetBytes((short)blockAlign).CopyTo(wavData, 32);
            BitConverter.GetBytes((short)bitsPerSample).CopyTo(wavData, 34);

            // data chunk
            Encoding.ASCII.GetBytes("data").CopyTo(wavData, 36);
            BitConverter.GetBytes(pcmData.Length).CopyTo(wavData, 40);
            pcmData.CopyTo(wavData, 44);

            return wavData;
        }

        private void RecordLatency(long latencyMs)
        {
            totalTranscriptions++;
            averageLatency = (averageLatency * (totalTranscriptions - 1) + latencyMs) / totalTranscriptions;
        }

        public void Dispose()
        {
            httpClient?.Dispose();
            Logger.Info($"DeepgramEngine disposed. Stats: {totalTranscriptions} transcriptions, {averageLatency:F1}ms avg");
        }
    }
}
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// Deepgram WebSocket streaming for ultra-low latency (sub-200ms).
    /// Uses live streaming API for real-time transcription.
    /// </summary>
    public class DeepgramStreamingEngine : IDisposable
    {
        private ClientWebSocket webSocket;
        private readonly string apiKey;
        private bool isConnected = false;
        private CancellationTokenSource cancellationTokenSource;

        // Callback for transcription results
        public event Action<string> OnTranscriptionReceived;

        // Performance tracking
        private long totalTranscriptions = 0;
        private double averageLatency = 0;

        public bool IsConnected => isConnected;
        public double AverageLatency => averageLatency;
        public long TotalTranscriptions => totalTranscriptions;

        public DeepgramStreamingEngine()
        {
            apiKey = Environment.GetEnvironmentVariable("DEEPGRAM_API_KEY") ?? "YOUR_API_KEY_HERE";
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                Logger.Info("DeepgramStreamingEngine: Connecting to WebSocket...");

                webSocket = new ClientWebSocket();
                webSocket.Options.SetRequestHeader("Authorization", $"Token {apiKey}");

                // Deepgram live streaming endpoint with optimized settings for speed
                var uri = new Uri("wss://api.deepgram.com/v1/listen?" +
                    "model=nova-2-general&" +        // Fastest general model
                    "language=en&" +                 // English only
                    "punctuate=false&" +              // Disable punctuation for speed
                    "smart_format=false&" +           // Disable formatting for speed
                    "interim_results=true&" +         // Send partial results immediately
                    "endpointing=100&" +              // Very short endpointing (100ms)
                    "vad_events=false&" +             // No VAD events
                    "encoding=linear16&" +            // PCM encoding
                    "sample_rate=16000&" +            // 16kHz
                    "channels=1");                    // Mono

                cancellationTokenSource = new CancellationTokenSource();
                await webSocket.ConnectAsync(uri, cancellationTokenSource.Token);

                isConnected = true;
                Logger.Info("✅ DeepgramStreamingEngine connected - expecting sub-200ms!");

                // Start receiving messages
                _ = Task.Run(() => ReceiveLoop());

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"DeepgramStreamingEngine connection failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stream audio data for immediate transcription.
        /// Expected latency: 50-150ms for streaming.
        /// </summary>
        public async Task StreamAudioAsync(byte[] audioData)
        {
            if (!isConnected || webSocket.State != WebSocketState.Open)
            {
                Logger.Warning("WebSocket not connected");
                return;
            }

            try
            {
                // Send raw PCM audio directly to WebSocket
                await webSocket.SendAsync(
                    new ArraySegment<byte>(audioData),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    cancellationTokenSource.Token);

                Logger.Debug($"Streamed {audioData.Length} bytes");
            }
            catch (Exception ex)
            {
                Logger.Error($"Streaming error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send end-of-stream signal to get final transcription.
        /// </summary>
        public async Task EndStreamAsync()
        {
            if (!isConnected) return;

            try
            {
                // Send empty message to signal end of audio
                var closeMessage = JsonSerializer.Serialize(new { type = "CloseStream" });
                var bytes = Encoding.UTF8.GetBytes(closeMessage);

                await webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationTokenSource.Token);

                Logger.Debug("Sent end-of-stream signal");
            }
            catch (Exception ex)
            {
                Logger.Error($"End stream error: {ex.Message}");
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);

            while (isConnected && webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(buffer, cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                        ProcessTranscriptionResponse(json);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Receive error: {ex.Message}");
                    break;
                }
            }
        }

        private void ProcessTranscriptionResponse(string json)
        {
            try
            {
                var response = JsonDocument.Parse(json);

                // Check if this is a transcript message
                if (response.RootElement.TryGetProperty("channel", out var channel))
                {
                    var alternatives = channel.GetProperty("alternatives");
                    if (alternatives.GetArrayLength() > 0)
                    {
                        var transcript = alternatives[0].GetProperty("transcript").GetString();

                        if (!string.IsNullOrWhiteSpace(transcript))
                        {
                            var isFinal = response.RootElement.GetProperty("is_final").GetBoolean();

                            // Log latency (Deepgram includes timing info)
                            if (response.RootElement.TryGetProperty("duration", out var duration))
                            {
                                var latencyMs = duration.GetDouble() * 1000;
                                RecordLatency((long)latencyMs);
                                Logger.Info($"Streaming transcription: {latencyMs:F0}ms, Final: {isFinal}, Text: '{transcript}'");
                            }

                            // Fire event with transcription
                            OnTranscriptionReceived?.Invoke(transcript);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Parse error: {ex.Message}");
            }
        }

        private void RecordLatency(long latencyMs)
        {
            totalTranscriptions++;
            averageLatency = (averageLatency * (totalTranscriptions - 1) + latencyMs) / totalTranscriptions;
        }

        public async Task DisconnectAsync()
        {
            if (!isConnected) return;

            isConnected = false;
            cancellationTokenSource?.Cancel();

            if (webSocket?.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }

            Logger.Info($"DeepgramStreamingEngine disconnected. Stats: {totalTranscriptions} transcriptions, {averageLatency:F1}ms avg");
        }

        public void Dispose()
        {
            _ = DisconnectAsync();
            webSocket?.Dispose();
            cancellationTokenSource?.Dispose();
        }
    }
}
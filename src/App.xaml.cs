using ModernWpf;
using System.Windows;
using Velopack;
using System;
using System.Threading.Tasks;
using SuperWhisperWPF.Core;
using SuperWhisperWPF.Views;

namespace SuperWhisperWPF
{
    /// <summary>
    /// Main application class for Lumina speech-to-text application.
    /// Handles application lifecycle, auto-updates, and theme initialization.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Called when the application starts.
        /// Initializes Velopack auto-updater, checks for updates, and sets the application theme.
        /// </summary>
        /// <param name="e">Startup event arguments.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            // Handle command-line arguments
            if (e.Args.Length > 0)
            {
                _ = Task.Run(async () =>
                {
                    foreach (var arg in e.Args)
                    {
                        switch (arg.ToLower())
                        {
                            case "--compare-engines":
                                Logger.Info("Running A/B engine comparison...");
                                // Generate test audio (1 second of speech-like audio)
                                var testAudio = GenerateTestAudio(1000);
                                await EngineComparison.CompareEnginesAsync(testAudio);
                                Environment.Exit(0);
                                break;
                            case "--compare-engines-live":
                                Logger.Info("Starting live A/B comparison with microphone input...");
                                await EngineComparison.RunLiveComparisonAsync();
                                Environment.Exit(0);
                                break;
                            case "--latency-benchmark":
                                var latencyBenchmark = new LatencyBenchmark();
                                await latencyBenchmark.RunFullBenchmarkAsync();
                                break;
                            case "--help":
                                Logger.Info("Available commands:");
                                Logger.Info("  --compare-engines      : A/B test all engines in parallel (synthetic audio)");
                                Logger.Info("  --compare-engines-live : A/B test all engines with real microphone input");
                                Logger.Info("  --latency-benchmark    : Run latency benchmark");
                                Logger.Info("  --enable-tiny          : Enable tiny model for ~5x speed");
                                Logger.Info("  --benchmark            : Run model performance comparison");
                                Logger.Info("  --benchmark-onnx       : Test ONNX Runtime with DirectML");
                                Logger.Info("  --realistic-benchmark  : Test with real speech samples");
                                Logger.Info("  --ultra-benchmark      : Test ultra performance target (sub-200ms)");
                                break;
                        }
                    }
                });
            }

            // Initialize Velopack for auto-updates
            try
            {
                VelopackApp.Build().Run();
            }
            catch (Exception ex)
            {
                // Log or handle Velopack initialization errors
                // This might happen in development environment
                System.Diagnostics.Debug.WriteLine($"Velopack initialization failed: {ex.Message}");
            }

            // Check for updates asynchronously with proper error handling
            _ = Task.Run(async () =>
            {
                try
                {
                    await CheckForUpdatesAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Background update check failed: {ex.Message}");
                }
            });

            // Set light theme for modern appearance (matching our minimal UI)
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;

            // Note: Removed background engine initialization to prevent semaphore deadlock
            // Engine will initialize on first transcription request

            // Check for GPU and log capabilities
            _ = Task.Run(() =>
            {
                var gpu = GpuAccelerator.Instance;
                System.Diagnostics.Debug.WriteLine($"GPU: {gpu.GpuInfo}");
            });

            // Always use hybrid architecture with WebView2 UI
            bool runBenchmark = false;

            // Check command line arguments
            foreach (string arg in e.Args)
            {
                if (arg.Equals("--benchmark", StringComparison.OrdinalIgnoreCase))
                {
                    runBenchmark = true;
                }
            }

            // Run benchmark if requested
            if (runBenchmark)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000); // Wait for initialization
                    var monitor = PerformanceMonitor.Instance;
                    var results = await monitor.RunBenchmarkAsync();
                    System.Diagnostics.Debug.WriteLine(results.ToString());
                    Logger.Info(results.ToString());
                });
            }

            // Create and show the hybrid window with WebView2 and native features
            Window mainWindow = new HybridMainWindow();
            System.Diagnostics.Debug.WriteLine("Starting Lumina with hybrid WebView2 UI");

            MainWindow = mainWindow;
            mainWindow.Show();

            base.OnStartup(e);
        }

        /// <summary>
        /// Checks for application updates from GitHub releases asynchronously.
        /// Downloads and applies updates automatically if available.
        /// </summary>
        private static async Task CheckForUpdatesAsync()
        {
            try
            {
                // GitHub releases URL for Lumina
                var mgr = new UpdateManager(Constants.App.UPDATE_URL);

                // Check for new version
                var newVersion = await mgr.CheckForUpdatesAsync();
                if (newVersion == null)
                    return; // No update available

                // Download new version
                await mgr.DownloadUpdatesAsync(newVersion);

                // Install new version and restart app
                await Task.Run(() => mgr.ApplyUpdatesAndRestart(newVersion));
            }
            catch (Exception ex)
            {
                // Log update check errors
                System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates test audio for A/B testing.
        /// Creates PCM audio data (16-bit, 16kHz, mono) simulating speech.
        /// </summary>
        /// <param name="durationMs">Duration of audio in milliseconds</param>
        /// <returns>PCM audio data as byte array</returns>
        private static byte[] GenerateTestAudio(int durationMs)
        {
            const int sampleRate = 16000;
            const int bytesPerSample = 2; // 16-bit

            int sampleCount = (sampleRate * durationMs) / 1000;
            byte[] audioData = new byte[sampleCount * bytesPerSample];

            // Generate speech-like audio with varying frequencies
            // Simulates human voice frequencies (85-255 Hz fundamental)
            var random = new Random(42); // Deterministic for consistent testing

            for (int i = 0; i < sampleCount; i++)
            {
                // Mix multiple frequencies to simulate speech formants
                double t = i / (double)sampleRate;
                double sample = 0;

                // Fundamental frequency (varies like pitch)
                double f0 = 120 + 30 * Math.Sin(2 * Math.PI * 3 * t);
                sample += 0.3 * Math.Sin(2 * Math.PI * f0 * t);

                // First formant (~500-700 Hz)
                sample += 0.2 * Math.Sin(2 * Math.PI * 600 * t);

                // Second formant (~1000-1500 Hz)
                sample += 0.15 * Math.Sin(2 * Math.PI * 1200 * t);

                // Add some noise for realism
                sample += 0.05 * (random.NextDouble() * 2 - 1);

                // Apply envelope (fade in/out to avoid clicks)
                double envelope = 1.0;
                if (i < sampleRate * 0.05) // 50ms fade in
                    envelope = i / (sampleRate * 0.05);
                else if (i > sampleCount - sampleRate * 0.05) // 50ms fade out
                    envelope = (sampleCount - i) / (sampleRate * 0.05);

                sample *= envelope;

                // Convert to 16-bit PCM
                short pcmSample = (short)(sample * 8000); // Scale to reasonable amplitude
                audioData[i * 2] = (byte)(pcmSample & 0xFF);
                audioData[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }

            return audioData;
        }
    }
}
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
                            case "--enable-tiny":
                                await ModelBenchmark.EnableTinyModelAsync();
                                break;
                            case "--benchmark":
                                await ModelBenchmark.RunBenchmarkAsync();
                                break;
                            case "--benchmark-onnx":
                                await ModelBenchmark.RunOnnxBenchmarkAsync();
                                break;
                            case "--realistic-benchmark":
                                await RealisticBenchmark.RunRealisticBenchmarkAsync();
                                break;
                            case "--ultra-benchmark":
                                await UltraBenchmark.RunUltraBenchmarkAsync();
                                break;
                            case "--latency-benchmark":
                                var latencyBenchmark = new LatencyBenchmark();
                                await latencyBenchmark.RunFullBenchmarkAsync();
                                break;
                            case "--help":
                                Logger.Info("Available commands:");
                                Logger.Info("  --enable-tiny         : Enable tiny model for ~5x speed");
                                Logger.Info("  --benchmark           : Run model performance comparison");
                                Logger.Info("  --benchmark-onnx      : Test ONNX Runtime with DirectML");
                                Logger.Info("  --realistic-benchmark : Test with real speech samples");
                                Logger.Info("  --ultra-benchmark     : Test ultra performance target (sub-200ms)");
                                Logger.Info("  --latency-benchmark   : Test all optimization engines for sub-200ms target");
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
    }
}
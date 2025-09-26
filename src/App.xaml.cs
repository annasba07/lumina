using ModernWpf;
using System.Windows;
using Velopack;
using System;
using System.Threading.Tasks;
using SuperWhisperWPF.Core;

namespace SuperWhisperWPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
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

            base.OnStartup(e);
        }

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
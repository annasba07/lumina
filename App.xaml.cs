using ModernWpf;
using System.Windows;

namespace SuperWhisperWPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Set dark theme for modern appearance
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            
            base.OnStartup(e);
        }
    }
}
using System;
using System.Windows;
using System.Windows.Media;
using ModernWpf;
using SuperWhisperWPF.Core;

namespace SuperWhisperWPF.Services
{
    /// <summary>
    /// Manages application theme (Light/Dark mode) and provides theme-related utilities.
    /// </summary>
    public class ThemeService
    {
        private static ThemeService _instance;
        private ApplicationTheme _currentTheme;

        /// <summary>
        /// Gets the singleton instance of ThemeService.
        /// </summary>
        public static ThemeService Instance => _instance ??= new ThemeService();

        /// <summary>
        /// Event fired when theme changes.
        /// </summary>
        public event EventHandler<ApplicationTheme> ThemeChanged;

        /// <summary>
        /// Gets the current application theme.
        /// </summary>
        public ApplicationTheme CurrentTheme
        {
            get => _currentTheme;
            private set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    ThemeChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// Gets whether dark mode is currently active.
        /// </summary>
        public bool IsDarkMode => CurrentTheme == ApplicationTheme.Dark;

        private ThemeService()
        {
            // Load saved theme preference or default to system theme
            LoadThemePreference();
        }

        /// <summary>
        /// Sets the application theme to Light or Dark mode.
        /// </summary>
        public void SetTheme(ApplicationTheme theme)
        {
            CurrentTheme = theme;
            ThemeManager.Current.ApplicationTheme = theme;
            SaveThemePreference(theme);
            UpdateColors();
            Logger.Info($"Theme changed to: {theme}");
        }

        /// <summary>
        /// Toggles between Light and Dark themes.
        /// </summary>
        public void ToggleTheme()
        {
            var newTheme = CurrentTheme == ApplicationTheme.Light
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
            SetTheme(newTheme);
        }

        /// <summary>
        /// Sets theme based on Windows system theme.
        /// </summary>
        public void UseSystemTheme()
        {
            // Check Windows 10/11 theme setting
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key?.GetValue("AppsUseLightTheme");
                    if (value is int intValue)
                    {
                        var theme = intValue == 0 ? ApplicationTheme.Dark : ApplicationTheme.Light;
                        SetTheme(theme);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not read system theme: {ex.Message}");
            }

            // Default to light theme if system theme can't be determined
            SetTheme(ApplicationTheme.Light);
        }

        /// <summary>
        /// Updates application colors based on current theme.
        /// </summary>
        private void UpdateColors()
        {
            var resources = Application.Current.Resources;

            if (IsDarkMode)
            {
                // Dark mode colors
                resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                resources["SurfaceBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(180, 180, 180));
                resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(96, 165, 250));
                resources["AccentHoverBrush"] = new SolidColorBrush(Color.FromRgb(59, 130, 246));
            }
            else
            {
                // Light mode colors (original)
                resources["BackgroundBrush"] = new SolidColorBrush(Colors.White);
                resources["SurfaceBrush"] = new SolidColorBrush(Color.FromRgb(249, 250, 251));
                resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(229, 231, 235));
                resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(17, 24, 39));
                resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(107, 114, 128));
                resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                resources["AccentHoverBrush"] = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            }
        }

        /// <summary>
        /// Saves theme preference to settings.
        /// </summary>
        private void SaveThemePreference(ApplicationTheme theme)
        {
            try
            {
                var settings = AppSettings.Instance;
                settings.Theme = theme.ToString();
                settings.Save();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save theme preference: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads saved theme preference from settings.
        /// </summary>
        private void LoadThemePreference()
        {
            try
            {
                var settings = AppSettings.Instance;
                if (!string.IsNullOrEmpty(settings.Theme) &&
                    Enum.TryParse<ApplicationTheme>(settings.Theme, out var theme))
                {
                    CurrentTheme = theme;
                    ThemeManager.Current.ApplicationTheme = theme;
                    UpdateColors();
                }
                else
                {
                    // Use system theme if no preference saved
                    UseSystemTheme();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load theme preference: {ex.Message}", ex);
                SetTheme(ApplicationTheme.Light); // Default to light
            }
        }

        /// <summary>
        /// Gets the appropriate color for the current theme.
        /// </summary>
        public Color GetThemedColor(Color lightColor, Color darkColor)
        {
            return IsDarkMode ? darkColor : lightColor;
        }

        /// <summary>
        /// Gets the appropriate brush for the current theme.
        /// </summary>
        public Brush GetThemedBrush(Brush lightBrush, Brush darkBrush)
        {
            return IsDarkMode ? darkBrush : lightBrush;
        }
    }
}
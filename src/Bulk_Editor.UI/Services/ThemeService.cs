using System;
using System.Windows;
using ModernWpf;
using Microsoft.Extensions.Logging;

namespace Doc_Helper.UI.Services
{
    /// <summary>
    /// Service for managing application themes
    /// </summary>
    public class ThemeService
    {
        private readonly ILogger<ThemeService> _logger;

        public ThemeService(ILogger<ThemeService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Apply theme to the application shell
        /// </summary>
        public void ApplyTheme(Window shell)
        {
            try
            {
                // Apply ModernWpf theme
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                ThemeManager.Current.AccentColor = System.Windows.Media.Colors.Blue;

                _logger.LogInformation("Applied default theme to application shell");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme to application shell");
            }
        }

        /// <summary>
        /// Change application theme
        /// </summary>
        public void ChangeTheme(ApplicationTheme theme)
        {
            try
            {
                ThemeManager.Current.ApplicationTheme = theme;
                _logger.LogInformation("Changed application theme to {Theme}", theme);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to change application theme to {Theme}", theme);
            }
        }

        /// <summary>
        /// Change accent color
        /// </summary>
        public void ChangeAccentColor(System.Windows.Media.Color color)
        {
            try
            {
                ThemeManager.Current.AccentColor = color;
                _logger.LogInformation("Changed accent color to {Color}", color);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to change accent color to {Color}", color);
            }
        }
    }
}
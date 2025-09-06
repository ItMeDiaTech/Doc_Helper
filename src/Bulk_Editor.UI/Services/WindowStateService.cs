using System;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace Doc_Helper.UI.Services
{
    /// <summary>
    /// Service for managing window state persistence
    /// </summary>
    public class WindowStateService
    {
        private readonly ILogger<WindowStateService> _logger;

        public WindowStateService(ILogger<WindowStateService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Restore window state from saved settings
        /// </summary>
        public void RestoreWindowState(Window window)
        {
            try
            {
                // For now, just set a reasonable default size and center the window
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                window.Width = 1200;
                window.Height = 800;
                window.WindowState = WindowState.Normal;

                _logger.LogInformation("Restored window state");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore window state");
            }
        }

        /// <summary>
        /// Save current window state
        /// </summary>
        public void SaveWindowState(Window window)
        {
            try
            {
                // TODO: Implement persistent storage of window state
                _logger.LogInformation("Window state saved");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save window state");
            }
        }

        /// <summary>
        /// Maximize window
        /// </summary>
        public void MaximizeWindow(Window window)
        {
            try
            {
                window.WindowState = WindowState.Maximized;
                _logger.LogInformation("Window maximized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to maximize window");
            }
        }

        /// <summary>
        /// Minimize window
        /// </summary>
        public void MinimizeWindow(Window window)
        {
            try
            {
                window.WindowState = WindowState.Minimized;
                _logger.LogInformation("Window minimized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to minimize window");
            }
        }

        /// <summary>
        /// Restore window from minimized/maximized state
        /// </summary>
        public void RestoreWindow(Window window)
        {
            try
            {
                window.WindowState = WindowState.Normal;
                _logger.LogInformation("Window restored");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore window");
            }
        }
    }
}
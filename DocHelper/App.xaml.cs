using Prism.Ioc;
using Prism.DryIoc;
using System.Windows;

namespace DocHelper;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : PrismApplication
{
    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<UpdateService>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Show a debug message to confirm the app is starting
            #if DEBUG
            System.Windows.MessageBox.Show("App is starting up...", "Debug", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            #endif
            
            base.OnStartup(e);
            
            // Simplify startup for single-file apps - skip complex installation logic
            try
            {
                // Just ensure the logs directory exists - keep it simple
                var logsPath = InstallationService.GetLogsPath();
                if (!System.IO.Directory.Exists(logsPath))
                {
                    System.IO.Directory.CreateDirectory(logsPath);
                }
            }
            catch (Exception ex)
            {
                // Even this simple operation can fail - just continue
                System.Diagnostics.Debug.WriteLine($"Logs directory creation failed: {ex.Message}");
            }
            
            // Skip taskbar pinning for now to avoid startup issues
            
            // Simplify update service - may fail in single-file apps
            try
            {
                var updateService = Container.Resolve<UpdateService>();
                var settingsViewModel = new SettingsViewModel();
                await updateService.CheckForUpdatesOnStartupAsync(settingsViewModel.AutoCheckForUpdates);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
                // Continue without update checking
            }
        }
        catch (Exception ex)
        {
            // Last resort error handling - show message and try to continue
            try
            {
                System.Windows.MessageBox.Show($"Startup error: {ex.Message}\n\nTrying to continue...", 
                    "Startup Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            catch
            {
                // Even MessageBox failed - try to continue silently
            }
        }
    }
}


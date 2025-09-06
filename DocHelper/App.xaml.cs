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
            base.OnStartup(e);
            
            // Try to ensure proper installation in %AppData% - don't block startup if it fails
            try
            {
                await InstallationService.EnsureInstallationAsync();
            }
            catch (Exception ex)
            {
                // Log but don't crash - continue with normal startup
                System.Diagnostics.Debug.WriteLine($"Installation service failed: {ex.Message}");
            }
            
            // Try to pin to taskbar (first run or if not already pinned) - run in background
            _ = Task.Run(async () => 
            {
                try
                {
                    await InstallationService.TryPinToTaskbarAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Taskbar pinning failed: {ex.Message}");
                }
            });
            
            var updateService = Container.Resolve<UpdateService>();
            var settingsViewModel = new SettingsViewModel();
            
            // Check for updates on startup if auto-check is enabled
            try
            {
                await updateService.CheckForUpdatesOnStartupAsync(settingsViewModel.AutoCheckForUpdates);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            // Last resort error handling - show message and continue
            System.Windows.MessageBox.Show($"Startup error: {ex.Message}\n\nThe application will continue to start.", 
                "Startup Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }
}


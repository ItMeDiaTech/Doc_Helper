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
            
            // Create logs directory (installer-deployed apps can do this safely)
            try
            {
                var logsPath = InstallationService.GetLogsPath();
                if (!System.IO.Directory.Exists(logsPath))
                {
                    System.IO.Directory.CreateDirectory(logsPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Logs directory creation failed: {ex.Message}");
            }
            
            // Initialize services for installer-deployed version
            try
            {
                var updateService = Container.Resolve<UpdateService>();
                var settingsViewModel = new SettingsViewModel();
                await updateService.CheckForUpdatesOnStartupAsync(settingsViewModel.AutoCheckForUpdates);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            // Continue silently if there are any startup issues
            System.Diagnostics.Debug.WriteLine($"Startup error: {ex.Message}");
        }
    }
}


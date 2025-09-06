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
        base.OnStartup(e);
        
        // Ensure proper installation in %AppData%
        await InstallationService.EnsureInstallationAsync();
        
        // Try to pin to taskbar (first run or if not already pinned)
        _ = Task.Run(async () => await InstallationService.TryPinToTaskbarAsync());
        
        var updateService = Container.Resolve<UpdateService>();
        var settingsViewModel = new SettingsViewModel();
        
        // Check for updates on startup if auto-check is enabled
        await updateService.CheckForUpdatesOnStartupAsync(settingsViewModel.AutoCheckForUpdates);
    }
}


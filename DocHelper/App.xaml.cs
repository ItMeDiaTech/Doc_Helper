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

    protected override void OnStartup(StartupEventArgs e)
    {
        // Ultra-minimal startup - just start the app with no additional services
        // This eliminates all potential failure points that could prevent the app from showing
        base.OnStartup(e);
        
        // Skip all initialization services for single-file deployment
        // The app will work in basic mode without logs, updates, etc.
    }
}


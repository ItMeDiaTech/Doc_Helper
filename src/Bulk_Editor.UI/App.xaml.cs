using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Windows;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Events;
using Prism.Dialogs;
using DryIoc;
using Serilog;
using Serilog.Events;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Data;
using Doc_Helper.Data.Repositories;
using Doc_Helper.Infrastructure.Configuration;
using Doc_Helper.Infrastructure.Services;
using Doc_Helper.Infrastructure.Migration;
using Doc_Helper.Infrastructure.Deployment;
using Doc_Helper.Shared.Configuration;
using Doc_Helper.UI.ViewModels;
using Doc_Helper.UI.ViewModels.Base;
using Doc_Helper.UI.Views;
using Doc_Helper.UI.Services;
using Doc_Helper.UI.Views.Dialogs;
using Microsoft.EntityFrameworkCore;

namespace Doc_Helper.UI
{
    /// <summary>
    /// Modern WPF application with Prism MVVM and dependency injection
    /// </summary>
    public partial class App : PrismApplication
    {
        private IHost? _host;

        public App()
        {
            // Required for WPF App.xaml code-behind
            // InitializeComponent(); // Commented out - ensure App.xaml exists and is properly configured
        }

        [SupportedOSPlatform("windows7.0")]
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            ConfigureLogging();
            Log.Information("Doc Helper starting up...");

            base.OnStartup(e);
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.Exception, "Unhandled dispatcher exception");
            MessageBox.Show(
                $"Unhandled exception: {e.Exception.Message}\n\nStack trace:\n{e.Exception.StackTrace}",
                "Application Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
            Environment.Exit(1);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "Unhandled application exception");
                MessageBox.Show(
                    $"Fatal error: {ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                    "Fatal Application Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            Environment.Exit(1);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Doc Helper shutting down");
            _host?.Dispose();
            Log.CloseAndFlush();
            base.OnExit(e);
        }

        [SupportedOSPlatform("windows7.0")]
        protected override Window CreateShell()
        {
            Log.Information("Creating shell window...");
            var mainWindow = Container.Resolve<MainWindow>();
            Log.Information("Shell window created successfully");
            return mainWindow;
        }

        [SupportedOSPlatform("windows7.0")]
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Build host for configuration and services
            _host = CreateHostBuilder().Build();
            var sp = _host.Services;

            // Register configuration root
            var configuration = sp.GetRequiredService<IConfiguration>();
            containerRegistry.RegisterInstance(configuration);

            // Register app options
            var appOptions = configuration.GetSection("App").Get<AppOptions>() ?? new AppOptions();
            containerRegistry.RegisterInstance(appOptions);

            // Register IOptions<AppOptions>
            containerRegistry.RegisterInstance<Microsoft.Extensions.Options.IOptions<AppOptions>>(
                Microsoft.Extensions.Options.Options.Create(appOptions));

            // Convert and register AppOptions for Shared.Models.Configuration namespace
            var modelsAppOptions = new Doc_Helper.Shared.Models.Configuration.AppOptions
            {
                ApplicationName = "Doc Helper",
                Version = "3.0.0",
                Environment = "Development",
                Api = new Doc_Helper.Shared.Models.Configuration.ApiOptions
                {
                    PowerAutomateFlowUrl = appOptions.Api.PowerAutomateFlowUrl,
                    TimeoutSeconds = appOptions.Api.TimeoutSeconds,
                    RetryCount = 3,
                    RetryDelaySeconds = 2,
                    MaxBatchSize = 100,
                    RateLimitPerMinute = 60
                },
                Processing = new Doc_Helper.Shared.Models.Configuration.ProcessingOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    BufferSize = 100,
                    EnableOptimizations = true,
                    DefaultFileExtensions = new List<string> { ".docx" },
                    EnableAutoBackup = appOptions.Processing.CreateBackups,
                    BackupPath = appOptions.Processing.BackupFolderName,
                    UseCentralizedBackups = false,
                    BackupRootPath = "Backups",
                    BackupFolderName = appOptions.Processing.BackupFolderName,
                    BackupRetentionDays = 30,
                    EnableUndo = true,
                    AutoCleanupBackups = true
                },
                Ui = new Doc_Helper.Shared.Models.Configuration.UiOptions
                {
                    Theme = "System",
                    ShowSplashScreen = false,
                    EnableAnimations = true,
                    RememberWindowState = appOptions.UI.RememberWindowPosition,
                    DefaultWindowWidth = 1200,
                    DefaultWindowHeight = 800,
                    EnableTooltips = appOptions.UI.ShowToolTips,
                    AutoOpenChangelog = false,
                    SaveChangelogToDownloads = false,
                    ShowIndividualChangelogs = false,
                    EnableChangelogExport = true,
                    FixSourceHyperlinks = appOptions.UI.FixSourceHyperlinks,
                    AppendContentID = appOptions.UI.AppendContentID,
                    CheckTitleChanges = appOptions.UI.CheckTitleChanges,
                    FixTitles = appOptions.UI.FixTitles,
                    FixInternalHyperlink = appOptions.UI.FixInternalHyperlink,
                    FixDoubleSpaces = appOptions.UI.FixDoubleSpaces,
                    ReplaceHyperlink = appOptions.UI.ReplaceHyperlink,
                    ReplaceText = false,
                    OpenChangelogAfterUpdates = appOptions.UI.OpenChangelogAfterUpdates
                },
                Data = new Doc_Helper.Shared.Models.Configuration.DataOptions
                {
                    ConnectionString = $"Data Source={appOptions.Data.DatabasePath}",
                    EnableDatabaseLogging = false,
                    CacheExpirationMinutes = (int)(appOptions.Data.CacheExpiryHours * 60),
                    MaxCacheSizeMB = 100,
                    EnableCompression = false,
                    AutoMigrateDatabase = true
                },
                Update = new Doc_Helper.Shared.Models.Configuration.UpdateOptions
                {
                    EnableAutoUpdates = false,
                    CheckFrequencyHours = 24,
                    UpdateServerUrl = "",
                    EnablePreReleaseUpdates = false,
                    SilentInstall = false
                }
            };

            // Register IOptionsMonitor<AppOptions> (required by ConfigurationService)
            containerRegistry.RegisterInstance<Microsoft.Extensions.Options.IOptionsMonitor<Doc_Helper.Shared.Models.Configuration.AppOptions>>(
                new SimpleOptionsMonitor<Doc_Helper.Shared.Models.Configuration.AppOptions>(modelsAppOptions));

            // Register IOptions<AppOptions> for Models.Configuration namespace (required by other services)
            containerRegistry.RegisterInstance<Microsoft.Extensions.Options.IOptions<Doc_Helper.Shared.Models.Configuration.AppOptions>>(
                Microsoft.Extensions.Options.Options.Create(modelsAppOptions));

            // Register the ApiOptions instance (already created in modelsAppOptions)
            containerRegistry.RegisterInstance(modelsAppOptions.Api);

            // Register the IOptions wrapper for Shared.Models.Configuration.ApiOptions
            var optionsWrapper = Microsoft.Extensions.Options.Options.Create(modelsAppOptions.Api);
            containerRegistry.RegisterInstance<Microsoft.Extensions.Options.IOptions<Doc_Helper.Shared.Models.Configuration.ApiOptions>>(optionsWrapper);

            // Logging
            containerRegistry.RegisterInstance(Log.Logger);
            containerRegistry.RegisterInstance<Serilog.ILogger>(Log.Logger);
            containerRegistry.Register<ILogger<ConfigurationService>, SerilogLoggerAdapter<ConfigurationService>>();
            containerRegistry.Register<ILogger<DocumentProcessingService>, SerilogLoggerAdapter<DocumentProcessingService>>();
            containerRegistry.Register<ILogger<WordDocumentProcessor>, SerilogLoggerAdapter<WordDocumentProcessor>>();
            containerRegistry.Register<ILogger<ApiService>, SerilogLoggerAdapter<ApiService>>();
            containerRegistry.Register<ILogger<CacheService>, SerilogLoggerAdapter<CacheService>>();
            containerRegistry.Register<ILogger<ValidationService>, SerilogLoggerAdapter<ValidationService>>();
            containerRegistry.Register<ILogger<HyperlinkProcessingService>, SerilogLoggerAdapter<HyperlinkProcessingService>>();
            containerRegistry.Register<ILogger<ThemeService>, SerilogLoggerAdapter<ThemeService>>();
            containerRegistry.Register<ILogger<SettingsService>, SerilogLoggerAdapter<SettingsService>>();
            containerRegistry.Register<ILogger<WindowStateService>, SerilogLoggerAdapter<WindowStateService>>();
            containerRegistry.Register<ILogger<LogViewerService>, SerilogLoggerAdapter<LogViewerService>>();
            containerRegistry.Register<ILogger<BackgroundSyncService>, SerilogLoggerAdapter<BackgroundSyncService>>();
            containerRegistry.Register<ILogger<BackgroundUpdateService>, SerilogLoggerAdapter<BackgroundUpdateService>>();
            containerRegistry.Register<ILogger<VelopackDeploymentService>, SerilogLoggerAdapter<VelopackDeploymentService>>();
            containerRegistry.Register<ILogger<BackupService>, SerilogLoggerAdapter<BackupService>>();
            containerRegistry.Register<ILogger<ChangelogService>, SerilogLoggerAdapter<ChangelogService>>();

            // DbContextOptions factory
            containerRegistry.Register<DbContextOptions<DocHelperDbContext>>(() =>
            {
                var dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DocHelper",
                    appOptions.Data.DatabasePath);

                Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

                var ob = new DbContextOptionsBuilder<DocHelperDbContext>();
                ob.UseSqlite($"Data Source={dbPath}");
                return ob.Options;
            });

            // DbContext as transient
            containerRegistry.Register<DocHelperDbContext>(() =>
            {
                var options = Container.Resolve<DbContextOptions<DocHelperDbContext>>();
                return new DocHelperDbContext(options);
            });

            // Infra services
            containerRegistry.RegisterSingleton<IMemoryCache>(() => new MemoryCache(new MemoryCacheOptions()));
            containerRegistry.RegisterSingleton<HttpClient>(() => new HttpClient());

            // Core services
            containerRegistry.Register<IConfigurationService, ConfigurationService>();
            containerRegistry.Register<IDocumentProcessingService, DocumentProcessingService>();
            containerRegistry.Register<IWordDocumentProcessor, WordDocumentProcessor>();
            containerRegistry.Register<IValidationService, ValidationService>();
            containerRegistry.Register<ICacheService, CacheService>();

            // API and processing
            containerRegistry.Register<IApiService, ApiService>();
            containerRegistry.Register<IHyperlinkProcessingService, HyperlinkProcessingService>();
            containerRegistry.Register<IHyperlinkLookupService, HyperlinkLookupService>();

            // Backup and Changelog services
            containerRegistry.Register<IBackupService, BackupService>();
            containerRegistry.Register<IChangelogService, ChangelogService>();

            // Repositories — transient for WPF
            containerRegistry.Register<IDocumentRepository, DocumentRepository>();
            containerRegistry.Register<IHyperlinkRepository, HyperlinkRepository>();

            // Migration & deployment
            containerRegistry.Register<IDeploymentService, VelopackDeploymentService>();
            containerRegistry.Register<IDatabaseMigrationService, DatabaseMigrationService>();
            containerRegistry.Register<IExcelToSqliteMigrator, ExcelToSqliteMigrator>();

            // UI Services
            containerRegistry.Register<ThemeService>();
            containerRegistry.Register<SettingsService>();
            containerRegistry.Register<WindowStateService>();
            containerRegistry.Register<LogViewerService>();
            containerRegistry.Register<IEventAggregator, EventAggregator>();
            containerRegistry.RegisterInstance<IDialogService>(new MockDialogServiceImpl());

            // ViewModels
            containerRegistry.Register<MainWindowViewModel>();
            containerRegistry.Register<Doc_Helper.UI.ViewModels.Dialogs.SettingsDialogViewModel>();

            // Register MainWindow
            containerRegistry.RegisterSingleton<MainWindow>();

            Log.Information("Dependency injection container configured");
        }

        [SupportedOSPlatform("windows7.0")]
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            base.ConfigureModuleCatalog(moduleCatalog);
        }

        [SupportedOSPlatform("windows7.0")]
        protected override void InitializeShell(Window shell)
        {
            base.InitializeShell(shell);

            using (var scope = Container.Resolve<IContainerProvider>().CreateScope())
            {
                var dbContext = scope.Resolve<DocHelperDbContext>();
                dbContext.Database.EnsureCreated();
                Log.Information("Database initialized");
            }

            var themeService = Container.Resolve<ThemeService>();
            themeService.ApplyTheme(shell);

            var windowStateService = Container.Resolve<WindowStateService>();
            windowStateService.RestoreWindowState(shell);

            Log.Information("Application shell initialized");
        }

        private static IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<AppOptions>(context.Configuration.GetSection("App"));
                });
        }

        private void ConfigureLogging()
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DocHelper",
                "Logs",
                "dochelper-.log");

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                    shared: true)
                .WriteTo.Debug()
                .CreateLogger();
        }
    }

    /// <summary>
    /// Adapter to bridge Serilog ILogger to Microsoft.Extensions.Logging.ILogger
    /// </summary>
    public class SerilogLoggerAdapter<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        private readonly Serilog.ILogger _logger;
        public SerilogLoggerAdapter(Serilog.ILogger logger) => _logger = logger.ForContext<T>();

        public IDisposable BeginScope<TState>(TState state) => null!;
        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(ConvertLogLevel(logLevel));

        public void Log<TState>(LogLevel logLevel, EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var message = formatter(state, exception);
            var serilogLevel = ConvertLogLevel(logLevel);
            if (exception != null)
                _logger.Write(serilogLevel, exception, message);
            else
                _logger.Write(serilogLevel, message);
        }

        private static LogEventLevel ConvertLogLevel(LogLevel logLevel) =>
            logLevel switch
            {
                LogLevel.Trace => LogEventLevel.Verbose,
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Information => LogEventLevel.Information,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Critical => LogEventLevel.Fatal,
                LogLevel.None => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };
    }

    /// <summary>
    /// Minimal mock dialog service to satisfy dependency injection
    /// </summary>
    public class MockDialogServiceImpl : IDialogService
    {
        public void Show(string name, IDialogParameters parameters, Action<IDialogResult> callback)
        {
            var message = parameters.ContainsKey("message") ? parameters.GetValue<string>("message") : "Dialog";
            var title = parameters.ContainsKey("title") ? parameters.GetValue<string>("title") : "Info";
            MessageBox.Show(message, title);
            callback?.Invoke(new DialogResult(ButtonResult.OK));
        }

        public void Show(string name, IDialogParameters parameters, Action<IDialogResult> callback, string windowName)
            => Show(name, parameters, callback);

        public void ShowDialog(string name, IDialogParameters parameters, Action<IDialogResult> callback)
            => Show(name, parameters, callback);

        public void ShowDialog(string name, IDialogParameters parameters, DialogCallback callback)
        {
            var message = parameters.ContainsKey("message") ? parameters.GetValue<string>("message") : "Dialog";
            var title = parameters.ContainsKey("title") ? parameters.GetValue<string>("title") : "Info";
            MessageBox.Show(message, title);
            callback.Invoke(new DialogResult(ButtonResult.OK));
        }
    }

    /// <summary>
    /// Simple implementation of IOptionsMonitor for dependency injection compatibility
    /// </summary>
    public class SimpleOptionsMonitor<T> : Microsoft.Extensions.Options.IOptionsMonitor<T> where T : class
    {
        private readonly T _value;
        private readonly List<Action<T, string?>> _listeners = new();

        public SimpleOptionsMonitor(T value)
        {
            _value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public T CurrentValue => _value;

        public T Get(string? name) => _value;

        public IDisposable OnChange(Action<T, string?> listener)
        {
            _listeners.Add(listener);
            return new ChangeTokenRegistration(() => _listeners.Remove(listener));
        }

        private class ChangeTokenRegistration : IDisposable
        {
            private readonly Action _dispose;
            public ChangeTokenRegistration(Action dispose) => _dispose = dispose;
            public void Dispose() => _dispose();
        }
    }
}

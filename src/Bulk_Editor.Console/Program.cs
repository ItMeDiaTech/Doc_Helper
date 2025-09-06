using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Doc_Helper.Data;
using Doc_Helper.Data.Repositories;
using Doc_Helper.Infrastructure.Configuration;
using Doc_Helper.Infrastructure.Services;
using Doc_Helper.Infrastructure.Migration;
using Doc_Helper.Shared.Configuration;

namespace Doc_Helper.Console
{
    /// <summary>
    /// Console application for Doc Helper functionality
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            // Configure Serilog
            ConfigureLogging();

            try
            {
                Log.Information("Bulk Editor Console starting...");

                // Build host
                var host = CreateHostBuilder(args).Build();

                // Initialize database
                await InitializeDatabaseAsync(host);

                // Run the application
                await RunApplicationAsync(host, args);

                Log.Information("Bulk Editor Console completed successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddCommandLine(args);
                })
                .ConfigureServices((context, services) =>
                {
                    // Register configuration
                    var appOptions = context.Configuration.GetSection("App").Get<AppOptions>() ?? new AppOptions();
                    services.AddSingleton(appOptions);

                    // Register DbContext
                    var dbPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "DocHelper",
                        appOptions.Data.DatabasePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

                    services.AddDbContext<DocHelperDbContext>(options =>
                        options.UseSqlite($"Data Source={dbPath}"));

                    // Register HTTP Client
                    services.AddHttpClient();

                    // Register Core Services
                    services.AddSingleton<IConfigurationService, ConfigurationService>();
                    services.AddSingleton<IApiService, ApiService>();
                    services.AddSingleton<IHyperlinkProcessingService, HyperlinkProcessingService>();
                    services.AddSingleton<IWordDocumentProcessor, WordDocumentProcessor>();
                    services.AddSingleton<IDocumentProcessingService, DocumentProcessingService>();
                    services.AddSingleton<IValidationService, ValidationService>();
                    services.AddSingleton<ICacheService, CacheService>();

                    // Register Repository Services
                    services.AddScoped<IDocumentRepository, DocumentRepository>();
                    services.AddScoped<IHyperlinkRepository, HyperlinkRepository>();

                    // Register Migration Services
                    services.AddSingleton<IDatabaseMigrationService, DatabaseMigrationService>();
                    services.AddSingleton<IExcelToSqliteMigrator, ExcelToSqliteMigrator>();

                    // Register Logging
                    services.AddLogging(builder => builder.AddSerilog());
                });

        private static async Task InitializeDatabaseAsync(IHost host)
        {
            using var scope = host.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DocHelperDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            Log.Information("Database initialized");
        }

        private static async Task RunApplicationAsync(IHost host, string[] args)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;

            var documentProcessingService = services.GetRequiredService<IDocumentProcessingService>();
            var validationService = services.GetRequiredService<IValidationService>();

            System.Console.WriteLine("=== Bulk Editor Console v3.0 ===");
            System.Console.WriteLine();

            if (args.Length == 0)
            {
                ShowUsage();
                return;
            }

            var command = args[0].ToLowerInvariant();

            switch (command)
            {
                case "process":
                    await ProcessDocumentsCommand(args, documentProcessingService, validationService);
                    break;
                case "validate":
                    await ValidateDocumentsCommand(args, validationService);
                    break;
                case "version":
                    ShowVersion();
                    break;
                case "help":
                case "--help":
                case "-h":
                    ShowUsage();
                    break;
                default:
                    System.Console.WriteLine($"Unknown command: {command}");
                    ShowUsage();
                    break;
            }
        }

        private static async Task ProcessDocumentsCommand(string[] args, IDocumentProcessingService processingService, IValidationService validationService)
        {
            if (args.Length < 2)
            {
                System.Console.WriteLine("Error: Please specify a file or folder path to process.");
                return;
            }

            var path = args[1];
            var files = new List<string>();

            if (File.Exists(path))
            {
                files.Add(path);
            }
            else if (Directory.Exists(path))
            {
                files.AddRange(Directory.GetFiles(path, "*.docx", SearchOption.AllDirectories));
            }
            else
            {
                System.Console.WriteLine($"Error: Path not found: {path}");
                return;
            }

            if (files.Count == 0)
            {
                System.Console.WriteLine("No Word documents found to process.");
                return;
            }

            System.Console.WriteLine($"Found {files.Count} document(s) to process.");

            try
            {
                // Validate documents first
                var validationSummary = await processingService.ValidateDocumentsAsync(files, default);
                System.Console.WriteLine($"Validation: {validationSummary.ValidFiles} valid, {validationSummary.InvalidFiles} invalid");

                if (validationSummary.InvalidFiles > 0)
                {
                    System.Console.WriteLine("Warning: Some files failed validation. Continue? (y/n)");
                    var response = System.Console.ReadLine();
                    if (response?.ToLowerInvariant() != "y")
                    {
                        System.Console.WriteLine("Processing cancelled.");
                        return;
                    }
                }

                // Process documents
                var progress = new Progress<ProcessingProgressReport>(report =>
                {
                    System.Console.WriteLine($"[{report.ProcessedCount}/{report.TotalCount}] {report.Message}");
                });

                var result = await processingService.ProcessDocumentsAsync(files, progress, default);

                System.Console.WriteLine();
                System.Console.WriteLine("=== Processing Results ===");
                System.Console.WriteLine($"Success: {result.Success}");
                System.Console.WriteLine($"Processed: {result.SuccessfulFiles}/{result.TotalFiles} files");
                System.Console.WriteLine($"Duration: {result.ProcessingDuration.TotalSeconds:F2} seconds");

                if (!result.Success)
                {
                    System.Console.WriteLine($"Error: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error during processing: {ex.Message}");
                Log.Error(ex, "Processing failed");
            }
        }

        private static async Task ValidateDocumentsCommand(string[] args, IValidationService validationService)
        {
            if (args.Length < 2)
            {
                System.Console.WriteLine("Error: Please specify a file or folder path to validate.");
                return;
            }

            var path = args[1];
            var files = new List<string>();

            if (File.Exists(path))
            {
                files.Add(path);
            }
            else if (Directory.Exists(path))
            {
                files.AddRange(Directory.GetFiles(path, "*.docx", SearchOption.AllDirectories));
            }
            else
            {
                System.Console.WriteLine($"Error: Path not found: {path}");
                return;
            }

            System.Console.WriteLine($"Validating {files.Count} document(s)...");

            try
            {
                foreach (var file in files)
                {
                    try
                    {
                        // Simple validation - check if file exists and has correct extension
                        var isValid = File.Exists(file) && Path.GetExtension(file).ToLowerInvariant() == ".docx";
                        var status = isValid ? "✓ Valid" : "✗ Invalid";
                        System.Console.WriteLine($"{status}: {Path.GetFileName(file)}");

                        if (!isValid)
                        {
                            System.Console.WriteLine($"  - Invalid file format or file not found");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"✗ Error: {Path.GetFileName(file)} - {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error during validation: {ex.Message}");
                Log.Error(ex, "Validation failed");
            }
        }

        private static void ShowVersion()
        {
            System.Console.WriteLine("Doc Helper Console v3.0");
            System.Console.WriteLine("Copyright © 2025 DiaTech. All rights reserved.");
        }

        private static void ShowUsage()
        {
            System.Console.WriteLine("Usage:");
            System.Console.WriteLine("  DocHelperConsole.exe process <file-or-folder-path>");
            System.Console.WriteLine("  DocHelperConsole.exe validate <file-or-folder-path>");
            System.Console.WriteLine("  DocHelperConsole.exe version");
            System.Console.WriteLine("  DocHelperConsole.exe help");
            System.Console.WriteLine();
            System.Console.WriteLine("Examples:");
            System.Console.WriteLine("  DocHelperConsole.exe process \"C:\\Documents\\MyFile.docx\"");
            System.Console.WriteLine("  DocHelperConsole.exe process \"C:\\Documents\"");
            System.Console.WriteLine("  DocHelperConsole.exe validate \"C:\\Documents\"");
        }

        private static void ConfigureLogging()
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DocHelper",
                "Logs",
                "console-.log");

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30)
                .CreateLogger();
        }
    }
}
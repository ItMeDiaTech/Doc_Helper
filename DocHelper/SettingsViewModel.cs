using Prism.Commands;
using Prism.Mvvm;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace DocHelper;

public class SettingsViewModel : BindableBase
{
    private string _apiUrl;
    private bool _autoCheckForUpdates;
    private string _currentVersion;
    private string _availableVersion;
    private string _updateStatus;
    private bool _isCheckingForUpdates;
    private readonly UpdateService _updateService;

    public SettingsViewModel()
    {
        _apiUrl = "";
        _autoCheckForUpdates = true;
        _currentVersion = "";
        _availableVersion = "";
        _updateStatus = "Ready";
        _isCheckingForUpdates = false;
        _updateService = new UpdateService();
        
        SaveSettingsCommand = new DelegateCommand(ExecuteSaveSettings);
        CheckForUpdatesCommand = new DelegateCommand(ExecuteCheckForUpdates, CanExecuteCheckForUpdates);
        
        LoadSettings();
        LoadCurrentVersion();
    }

    public string ApiUrl
    {
        get { return _apiUrl; }
        set { SetProperty(ref _apiUrl, value); }
    }

    public bool AutoCheckForUpdates
    {
        get { return _autoCheckForUpdates; }
        set { SetProperty(ref _autoCheckForUpdates, value); }
    }

    public string CurrentVersion
    {
        get { return _currentVersion; }
        set { SetProperty(ref _currentVersion, value); }
    }

    public string AvailableVersion
    {
        get { return _availableVersion; }
        set { SetProperty(ref _availableVersion, value); }
    }

    public string UpdateStatus
    {
        get { return _updateStatus; }
        set { SetProperty(ref _updateStatus, value); }
    }

    public bool IsCheckingForUpdates
    {
        get { return _isCheckingForUpdates; }
        set 
        { 
            SetProperty(ref _isCheckingForUpdates, value);
            CheckForUpdatesCommand.RaiseCanExecuteChanged();
        }
    }

    public DelegateCommand SaveSettingsCommand { get; }
    public DelegateCommand CheckForUpdatesCommand { get; }

    private void LoadSettings()
    {
        try
        {
            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                using var document = JsonDocument.Parse(json);
                
                if (document.RootElement.TryGetProperty("ApiEndpoints", out var apiEndpoints) &&
                    apiEndpoints.TryGetProperty("PowerAutomateWorkflow", out var workflow))
                {
                    ApiUrl = workflow.GetString() ?? "";
                }
                
                if (document.RootElement.TryGetProperty("Updates", out var updates) &&
                    updates.TryGetProperty("AutoCheckForUpdates", out var autoCheck))
                {
                    AutoCheckForUpdates = autoCheck.GetBoolean();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading settings: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadCurrentVersion()
    {
        CurrentVersion = _updateService.GetCurrentVersion();
        AvailableVersion = CurrentVersion; // Initially same as current
    }

    private void ExecuteSaveSettings()
    {
        try
        {
            var settings = new
            {
                ApiEndpoints = new
                {
                    PowerAutomateWorkflow = ApiUrl
                },
                Updates = new
                {
                    AutoCheckForUpdates = AutoCheckForUpdates
                },
                Security = new
                {
                    Note = "This file contains protected URLs and should never be committed to public repositories"
                }
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            File.WriteAllText(settingsPath, json);

            MessageBox.Show("Settings saved successfully!", "Success", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving settings: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanExecuteCheckForUpdates()
    {
        return !IsCheckingForUpdates;
    }

    private async void ExecuteCheckForUpdates()
    {
        IsCheckingForUpdates = true;
        UpdateStatus = "Checking for updates...";
        
        try
        {
            var result = await _updateService.CheckForUpdatesManuallyAsync();
            
            if (result.UpdateAvailable)
            {
                AvailableVersion = result.AvailableVersion ?? "Unknown";
                UpdateStatus = result.Message;
                
                var updateResult = MessageBox.Show(
                    $"{result.Message}\n\nWould you like to download and install the update now?", 
                    "Update Available", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Information);
                
                if (updateResult == MessageBoxResult.Yes)
                {
                    UpdateStatus = "Downloading and installing update...";
                    var installSuccess = await _updateService.DownloadAndInstallUpdateAsync();
                    
                    if (!installSuccess)
                    {
                        UpdateStatus = "Update installation failed";
                        MessageBox.Show("Failed to install update. Please try again later.", "Update Failed", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    UpdateStatus = "Update available - not installed";
                }
            }
            else
            {
                UpdateStatus = result.Message;
                AvailableVersion = CurrentVersion;
            }
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Update check failed: {ex.Message}";
            MessageBox.Show($"Error checking for updates: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }
}
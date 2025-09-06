using Prism.Commands;
using Prism.Mvvm;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace DocHelper;

public class SettingsViewModel : BindableBase
{
    private string _apiUrl;

    public SettingsViewModel()
    {
        _apiUrl = "";
        SaveSettingsCommand = new DelegateCommand(ExecuteSaveSettings);
        LoadSettings();
    }

    public string ApiUrl
    {
        get { return _apiUrl; }
        set { SetProperty(ref _apiUrl, value); }
    }

    public DelegateCommand SaveSettingsCommand { get; }

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
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading settings: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
}
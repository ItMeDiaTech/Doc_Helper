using System;
using System.Threading.Tasks;
using Velopack;
using System.Reflection;

namespace DocHelper;

public class UpdateService
{
    private readonly UpdateManager? _updateManager;
    
    public UpdateService()
    {
        try
        {
            _updateManager = new UpdateManager("https://github.com/ItMeDiaTech/Doc_Helper/releases");
        }
        catch
        {
            _updateManager = null;
        }
    }
    
    public string GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "Unknown";
    }
    
    public async Task<bool> CheckForUpdatesOnStartupAsync(bool autoCheckEnabled)
    {
        if (!autoCheckEnabled || _updateManager == null)
            return false;
            
        try
        {
            var updateInfo = await _updateManager.CheckForUpdatesAsync();
            if (updateInfo != null)
            {
                await _updateManager.DownloadUpdatesAsync(updateInfo);
                _updateManager.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);
                return true;
            }
        }
        catch
        {
            // Silently fail for startup checks
        }
        
        return false;
    }
    
    public async Task<UpdateCheckResult> CheckForUpdatesManuallyAsync()
    {
        if (_updateManager == null)
            return new UpdateCheckResult(false, "Update service not available", null);
            
        try
        {
            var updateInfo = await _updateManager.CheckForUpdatesAsync();
            if (updateInfo != null)
            {
                return new UpdateCheckResult(true, $"Update available: {updateInfo.TargetFullRelease.Version}", updateInfo.TargetFullRelease.Version?.ToString());
            }
            else
            {
                return new UpdateCheckResult(false, "No updates available", null);
            }
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, $"Update check failed: {ex.Message}", null);
        }
    }
    
    public async Task<bool> DownloadAndInstallUpdateAsync()
    {
        if (_updateManager == null)
            return false;
            
        try
        {
            var updateInfo = await _updateManager.CheckForUpdatesAsync();
            if (updateInfo != null)
            {
                await _updateManager.DownloadUpdatesAsync(updateInfo);
                _updateManager.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);
                return true;
            }
        }
        catch
        {
            return false;
        }
        
        return false;
    }
}

public class UpdateCheckResult
{
    public bool UpdateAvailable { get; }
    public string Message { get; }
    public string? AvailableVersion { get; }
    
    public UpdateCheckResult(bool updateAvailable, string message, string? availableVersion)
    {
        UpdateAvailable = updateAvailable;
        Message = message;
        AvailableVersion = availableVersion;
    }
}
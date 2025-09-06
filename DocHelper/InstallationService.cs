using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DocHelper;

public class InstallationService
{
    private static readonly string AppName = "DocHelper";
    private static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
    private static readonly string LogsPath = Path.Combine(AppDataPath, "logs");
    private static readonly string InstalledExePath = Path.Combine(AppDataPath, "DocHelper.exe");

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public int lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static string GetInstallationPath() => AppDataPath;
    public static string GetLogsPath() => LogsPath;

    public static async Task<bool> EnsureInstallationAsync()
    {
        try
        {
            // Create AppData directory if it doesn't exist
            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }

            // Create logs directory
            if (!Directory.Exists(LogsPath))
            {
                Directory.CreateDirectory(LogsPath);
            }

            // Get current exe path (compatible with single-file apps)
            var currentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(currentExePath))
            {
                currentExePath = AppContext.BaseDirectory;
                if (!currentExePath.EndsWith(".exe"))
                {
                    currentExePath = Path.Combine(currentExePath, "DocHelper.exe");
                }
            }

            // If we're not running from the AppData location, copy ourselves there
            if (!string.Equals(currentExePath, InstalledExePath, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(currentExePath))
                {
                    // Copy the exe to AppData
                    File.Copy(currentExePath, InstalledExePath, overwrite: true);
                    
                    // Copy any additional files that might be needed
                    var currentDir = Path.GetDirectoryName(currentExePath);
                    if (!string.IsNullOrEmpty(currentDir))
                    {
                        foreach (var file in Directory.GetFiles(currentDir, "*.dll"))
                        {
                            var fileName = Path.GetFileName(file);
                            var destPath = Path.Combine(AppDataPath, fileName);
                            File.Copy(file, destPath, overwrite: true);
                        }
                        
                        foreach (var file in Directory.GetFiles(currentDir, "*.json"))
                        {
                            var fileName = Path.GetFileName(file);
                            if (fileName != "appsettings.json") // Don't overwrite settings
                            {
                                var destPath = Path.Combine(AppDataPath, fileName);
                                File.Copy(file, destPath, overwrite: true);
                            }
                        }
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            // Log error if possible
            try
            {
                var errorLog = Path.Combine(LogsPath, "installation_error.log");
                await File.WriteAllTextAsync(errorLog, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Installation Error: {ex}");
            }
            catch { }
            
            return false;
        }
    }

    public static async Task<bool> TryPinToTaskbarAsync()
    {
        try
        {
            // Create a shortcut in the user's profile that can be pinned
            var shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "DocHelper.lnk");
            
            // Use PowerShell to create shortcut and attempt to pin
            var script = $@"
                $WshShell = New-Object -comObject WScript.Shell
                $Shortcut = $WshShell.CreateShortcut('{shortcutPath}')
                $Shortcut.TargetPath = '{InstalledExePath}'
                $Shortcut.WorkingDirectory = '{AppDataPath}'
                $Shortcut.Description = 'DocHelper - Document Processing Application'
                $Shortcut.Save()
                
                # Try to pin to taskbar (this may not work on all Windows versions due to security restrictions)
                try {{
                    $shell = New-Object -ComObject Shell.Application
                    $folder = $shell.Namespace('{Path.GetDirectoryName(shortcutPath)}')
                    $item = $folder.ParseName('{Path.GetFileName(shortcutPath)}')
                    $item.InvokeVerb('taskbarpin')
                }} catch {{
                    # Pinning failed, but shortcut was created
                }}
            ";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();
                
                // Clean up the temporary shortcut
                try
                {
                    if (File.Exists(shortcutPath))
                        File.Delete(shortcutPath);
                }
                catch { }
                
                return process.ExitCode == 0;
            }

            return false;
        }
        catch (Exception ex)
        {
            // Log error if possible
            try
            {
                var errorLog = Path.Combine(LogsPath, "taskbar_pin_error.log");
                await File.WriteAllTextAsync(errorLog, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Taskbar Pin Error: {ex}");
            }
            catch { }
            
            return false;
        }
    }

    public static void OpenLogsFolder()
    {
        try
        {
            if (Directory.Exists(LogsPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = LogsPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            else
            {
                // Create the directory and then open it
                Directory.CreateDirectory(LogsPath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = LogsPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
        }
        catch (Exception ex)
        {
            // Could show a message box or log the error
            System.Windows.MessageBox.Show($"Could not open logs folder: {ex.Message}", "Error", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
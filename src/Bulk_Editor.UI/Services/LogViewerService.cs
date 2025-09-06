using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Doc_Helper.UI.Services
{
    /// <summary>
    /// Service for viewing and managing application logs
    /// </summary>
    public class LogViewerService
    {
        private readonly ILogger<LogViewerService> _logger;
        private readonly string _logDirectory;

        public LogViewerService(ILogger<LogViewerService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DocHelper",
                "Logs");
        }

        /// <summary>
        /// Get available log files
        /// </summary>
        public async Task<IEnumerable<string>> GetLogFilesAsync()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    return Enumerable.Empty<string>();
                }

                var files = Directory.GetFiles(_logDirectory, "*.log")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .ToList();

                _logger.LogInformation("Found {Count} log files", files.Count);
                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get log files from {Directory}", _logDirectory);
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// Read log file content
        /// </summary>
        public async Task<string> ReadLogFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Log file not found: {FilePath}", filePath);
                    return string.Empty;
                }

                var content = await File.ReadAllTextAsync(filePath);
                _logger.LogInformation("Read log file: {FilePath}", filePath);
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read log file: {FilePath}", filePath);
                return $"Error reading log file: {ex.Message}";
            }
        }

        /// <summary>
        /// Get recent log entries
        /// </summary>
        public async Task<IEnumerable<string>> GetRecentLogEntriesAsync(int maxEntries = 100)
        {
            try
            {
                var logFiles = await GetLogFilesAsync();
                var recentEntries = new List<string>();

                foreach (var logFile in logFiles.Take(3)) // Check last 3 files
                {
                    var lines = await File.ReadAllLinesAsync(logFile);
                    recentEntries.AddRange(lines.TakeLast(maxEntries / 3));

                    if (recentEntries.Count >= maxEntries)
                        break;
                }

                return recentEntries.TakeLast(maxEntries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recent log entries");
                return new[] { $"Error getting recent logs: {ex.Message}" };
            }
        }

        /// <summary>
        /// Clear old log files
        /// </summary>
        public async Task ClearOldLogsAsync(int keepDays = 30)
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    return;
                }

                var cutoffDate = DateTime.Now.AddDays(-keepDays);
                var files = Directory.GetFiles(_logDirectory, "*.log")
                    .Where(f => File.GetLastWriteTime(f) < cutoffDate)
                    .ToList();

                foreach (var file in files)
                {
                    File.Delete(file);
                }

                _logger.LogInformation("Cleared {Count} old log files", files.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear old log files");
            }
        }

        /// <summary>
        /// Get log file size in MB
        /// </summary>
        public double GetLogDirectorySize()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    return 0;
                }

                var totalSize = Directory.GetFiles(_logDirectory, "*.log")
                    .Sum(f => new FileInfo(f).Length);

                return totalSize / (1024.0 * 1024.0); // Convert to MB
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate log directory size");
                return 0;
            }
        }
    }
}
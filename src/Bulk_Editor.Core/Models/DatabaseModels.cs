using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Doc_Helper.Core.Models
{
    /// <summary>
    /// Represents a database version using semantic versioning
    /// </summary>
    public class DatabaseVersion : IComparable<DatabaseVersion>
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }
        public string PreRelease { get; set; } = string.Empty;

        public DatabaseVersion() { }

        public DatabaseVersion(int major, int minor, int patch, string preRelease = "")
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            PreRelease = preRelease ?? string.Empty;
        }

        /// <summary>
        /// Parses version string into DatabaseVersion object
        /// </summary>
        public static bool TryParse(string versionString, out DatabaseVersion version)
        {
            version = new DatabaseVersion();

            if (string.IsNullOrEmpty(versionString))
                return false;

            var regex = new Regex(@"^(\d+)\.(\d+)\.(\d+)(?:-(.+))?$");
            var match = regex.Match(versionString);

            if (!match.Success)
                return false;

            if (!int.TryParse(match.Groups[1].Value, out var major) ||
                !int.TryParse(match.Groups[2].Value, out var minor) ||
                !int.TryParse(match.Groups[3].Value, out var patch))
                return false;

            version.Major = major;
            version.Minor = minor;
            version.Patch = patch;
            version.PreRelease = match.Groups[4].Value;

            return true;
        }

        public override string ToString()
        {
            var version = $"{Major}.{Minor}.{Patch}";
            return string.IsNullOrEmpty(PreRelease) ? version : $"{version}-{PreRelease}";
        }

        public int CompareTo(DatabaseVersion? other)
        {
            if (other == null) return 1;

            var majorComparison = Major.CompareTo(other.Major);
            if (majorComparison != 0) return majorComparison;

            var minorComparison = Minor.CompareTo(other.Minor);
            if (minorComparison != 0) return minorComparison;

            var patchComparison = Patch.CompareTo(other.Patch);
            if (patchComparison != 0) return patchComparison;

            // Handle pre-release versions
            if (string.IsNullOrEmpty(PreRelease) && !string.IsNullOrEmpty(other.PreRelease))
                return 1; // Release version is greater than pre-release

            if (!string.IsNullOrEmpty(PreRelease) && string.IsNullOrEmpty(other.PreRelease))
                return -1; // Pre-release is less than release

            return string.Compare(PreRelease, other.PreRelease, StringComparison.OrdinalIgnoreCase);
        }

        public static bool operator >(DatabaseVersion left, DatabaseVersion right) => left?.CompareTo(right) > 0;
        public static bool operator <(DatabaseVersion left, DatabaseVersion right) => left?.CompareTo(right) < 0;
        public static bool operator >=(DatabaseVersion left, DatabaseVersion right) => left?.CompareTo(right) >= 0;
        public static bool operator <=(DatabaseVersion left, DatabaseVersion right) => left?.CompareTo(right) <= 0;
        public static bool operator ==(DatabaseVersion? left, DatabaseVersion? right) =>
            ReferenceEquals(left, right) || (left is not null && left.CompareTo(right) == 0);
        public static bool operator !=(DatabaseVersion? left, DatabaseVersion? right) => !(left == right);

        public override bool Equals(object? obj) => obj is DatabaseVersion other && CompareTo(other) == 0;
        public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, PreRelease);
    }

    /// <summary>
    /// Result of database initialization operation
    /// </summary>
    public class DatabaseInitializationResult
    {
        public bool Success { get; set; }
        public bool DatabaseCreated { get; set; }
        public int EfMigrationsApplied { get; set; }
        public int CustomMigrationsApplied { get; set; }
        public DatabaseVersion InitialVersion { get; set; } = new();
        public DatabaseVersion FinalVersion { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public List<string> AppliedMigrations { get; set; } = new();

        /// <summary>
        /// Total number of migrations applied
        /// </summary>
        public int TotalMigrationsApplied => EfMigrationsApplied + CustomMigrationsApplied;

        /// <summary>
        /// Indicates if any migrations were applied
        /// </summary>
        public bool MigrationsApplied => TotalMigrationsApplied > 0;
    }

    /// <summary>
    /// Database validation result
    /// </summary>
    public class DatabaseValidationResult
    {
        public bool Success { get; set; }
        public bool CanConnect { get; set; }
        public int TotalRecords { get; set; }
        public int ActiveRecords { get; set; }
        public int InactiveRecords { get; set; }
        public long DatabaseSizeBytes { get; set; }
        public int QueryPerformanceMs { get; set; }
        public DateTime ValidationStartTime { get; set; }
        public DateTime ValidationEndTime { get; set; }
        public TimeSpan ValidationDuration => ValidationEndTime - ValidationStartTime;
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> ValidationWarnings { get; set; } = new();
        public Exception? Exception { get; set; }

        /// <summary>
        /// Database size in megabytes
        /// </summary>
        public double DatabaseSizeMB => DatabaseSizeBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Indicates if there are blocking validation errors
        /// </summary>
        public bool HasErrors => ValidationErrors.Count > 0;

        /// <summary>
        /// Indicates if there are warnings
        /// </summary>
        public bool HasWarnings => ValidationWarnings.Count > 0;
    }

    /// <summary>
    /// Database optimization result
    /// </summary>
    public class DatabaseOptimizationResult
    {
        public bool Success { get; set; }
        public long SizeBeforeBytes { get; set; }
        public long SizeAfterBytes { get; set; }
        public long SpaceSavedBytes { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public List<string> OptimizationSteps { get; set; } = new();

        /// <summary>
        /// Space saved in megabytes
        /// </summary>
        public double SpaceSavedMB => SpaceSavedBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Percentage of space saved
        /// </summary>
        public double SpaceSavedPercentage => SizeBeforeBytes > 0
            ? (double)SpaceSavedBytes / SizeBeforeBytes * 100
            : 0;
    }

    /// <summary>
    /// Database migration options
    /// </summary>
    public class DatabaseMigrationOptions
    {
        /// <summary>
        /// Timeout for migration operations
        /// </summary>
        public TimeSpan MigrationTimeout { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Create backup before applying migrations
        /// </summary>
        public bool CreateBackupBeforeMigration { get; set; } = true;

        /// <summary>
        /// Validate database after migration
        /// </summary>
        public bool ValidateAfterMigration { get; set; } = true;

        /// <summary>
        /// Enable detailed migration logging
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>
        /// Continue migration on non-critical errors
        /// </summary>
        public bool ContinueOnError { get; set; } = false;

        /// <summary>
        /// Maximum number of retry attempts for failed migrations
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between retry attempts
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
        public string DatabasePath { get; set; } = "Data/bulkeditor.db";
    }

    /// <summary>
    /// Database performance metrics
    /// </summary>
    public class DatabasePerformanceMetrics
    {
        public DateTime CollectionTime { get; set; } = DateTime.UtcNow;
        public long DatabaseSizeBytes { get; set; }
        public int TotalTables { get; set; }
        public int TotalIndexes { get; set; }
        public long TotalRecords { get; set; }
        public int QueryResponseTimeMs { get; set; }
        public double CacheHitRatio { get; set; }
        public int ActiveConnections { get; set; }
        public long MemoryUsageBytes { get; set; }
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();

        /// <summary>
        /// Database size in megabytes
        /// </summary>
        public double DatabaseSizeMB => DatabaseSizeBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Memory usage in megabytes
        /// </summary>
        public double MemoryUsageMB => MemoryUsageBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Average records per table
        /// </summary>
        public double AverageRecordsPerTable => TotalTables > 0 ? (double)TotalRecords / TotalTables : 0;
    }

    /// <summary>
    /// Database health status
    /// </summary>
    public class DatabaseHealthStatus
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;
        public DatabasePerformanceMetrics Metrics { get; set; } = new();
        public List<string> HealthIssues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public TimeSpan Uptime { get; set; }
        public int FailedOperations { get; set; }
        public int SuccessfulOperations { get; set; }

        /// <summary>
        /// Operation success rate
        /// </summary>
        public double SuccessRate
        {
            get
            {
                var totalOps = SuccessfulOperations + FailedOperations;
                return totalOps > 0 ? (double)SuccessfulOperations / totalOps * 100 : 100;
            }
        }

        /// <summary>
        /// Indicates if database is performing well
        /// </summary>
        public bool IsPerformant => IsHealthy &&
                                   Metrics.QueryResponseTimeMs < 1000 &&
                                   Metrics.CacheHitRatio > 0.8;
    }
}
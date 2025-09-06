using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Models;

namespace Doc_Helper.Core.Interfaces
{
    /// <summary>
    /// Interface for validation services with modern dependency injection
    /// </summary>
    public interface IValidationService
    {
        /// <summary>
        /// Validates hyperlinks for data integrity and format compliance
        /// </summary>
        Task<HyperlinkValidationResult> ValidateHyperlinksAsync(
            List<HyperlinkData> hyperlinks,
            ValidationOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates API connectivity and response format
        /// </summary>
        Task<ApiValidationResult> ValidateApiConnectivityAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates document files before processing
        /// </summary>
        Task<DocumentValidationSummary> ValidateDocumentsAsync(
            IEnumerable<string> filePaths,
            ValidationOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates system configuration and dependencies
        /// </summary>
        Task<SystemValidationResult> ValidateSystemConfigurationAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs comprehensive system health check
        /// </summary>
        Task<SystemHealthResult> PerformHealthCheckAsync(
            CancellationToken cancellationToken = default);
    }

}
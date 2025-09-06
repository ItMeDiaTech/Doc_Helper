using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Models;

namespace Doc_Helper.Core.Interfaces
{
    /// <summary>
    /// API service interface for hyperlink data retrieval and PowerAutomate integration
    /// </summary>
    public interface IApiService
    {
        /// <summary>
        /// Retrieves hyperlink data from the API using lookup IDs
        /// </summary>
        /// <param name="lookupIds">Collection of lookup IDs</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>API response with hyperlink data</returns>
        Task<ApiResponse> GetHyperlinkDataAsync(
            IEnumerable<string> lookupIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves hyperlink data in batches for better performance
        /// </summary>
        /// <param name="lookupIds">Collection of lookup IDs</param>
        /// <param name="batchSize">Size of each batch</param>
        /// <param name="progress">Progress reporting callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Batched API responses</returns>
        Task<BatchApiResponse> GetHyperlinkDataBatchAsync(
            IEnumerable<string> lookupIds,
            int batchSize = 50,
            IProgress<ApiProgressReport>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates API connectivity and authentication
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>API validation result</returns>
        Task<ApiValidationResult> ValidateApiConnectionAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets API health status and performance metrics
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>API health information</returns>
        Task<ApiHealthStatus> GetApiHealthAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends lookup IDs to PowerAutomate flow for processing
        /// </summary>
        /// <param name="lookupIds">List of lookup IDs to process</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Raw API response string</returns>
        Task<string> SendToPowerAutomateFlowAsync(
            List<string> lookupIds,
            CancellationToken cancellationToken = default);
    }
}
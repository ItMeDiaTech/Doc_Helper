using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Doc_Helper.Infrastructure.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doc_Helper.Infrastructure.Services
{
    /// <summary>
    /// Modern document processing service using TPL Dataflow pipeline
    /// Replaces legacy SemaphoreSlim approach with high-performance async pipeline
    /// </summary>
    public class DocumentProcessingService : IDocumentProcessingService, IDisposable
    {
        private readonly ILogger<DocumentProcessingService> _logger;
        private readonly IWordDocumentProcessor _documentProcessor;
        private readonly IApiService _apiService;
        private readonly IHyperlinkProcessingService _hyperlinkProcessor;
        private readonly ICacheService _cacheService;
        private readonly DocumentPipelineOptions _pipelineOptions;

        // Pipeline components
        private readonly TransformBlock<string, DocumentInput> _fileValidationBlock;
        private readonly TransformBlock<DocumentInput, DocumentExtractionResult> _extractionBlock;
        private readonly TransformManyBlock<DocumentExtractionResult, HyperlinkProcessingBatch> _batchingBlock;
        private readonly TransformBlock<HyperlinkProcessingBatch, ApiProcessingResult> _apiProcessingBlock;
        private readonly TransformBlock<ApiProcessingResult, DocumentUpdateResult> _documentUpdateBlock;
        private readonly ActionBlock<DocumentUpdateResult> _completionBlock;

        // Progress and control
        private readonly Channel<ProcessingProgressReport> _progressChannel;
        private readonly CancellationTokenSource _globalCancellation;
        private readonly ProcessingStatistics _statistics;
        private bool _disposed;

        // Events
        public event EventHandler<ProcessingStageChangedEventArgs>? ProcessingStageChanged;
        public event EventHandler<ProcessingErrorEventArgs>? ProcessingError;

        public DocumentProcessingService(
            ILogger<DocumentProcessingService> logger,
            IWordDocumentProcessor documentProcessor,
            IApiService apiService,
            IHyperlinkProcessingService hyperlinkProcessor,
            ICacheService cacheService,
            IOptions<DocumentPipelineOptions>? pipelineOptions = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _documentProcessor = documentProcessor ?? throw new ArgumentNullException(nameof(documentProcessor));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _hyperlinkProcessor = hyperlinkProcessor ?? throw new ArgumentNullException(nameof(hyperlinkProcessor));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _pipelineOptions = pipelineOptions?.Value ?? new DocumentPipelineOptions();

            _globalCancellation = new CancellationTokenSource();
            _progressChannel = Channel.CreateUnbounded<ProcessingProgressReport>();
            _statistics = new ProcessingStatistics();

            // Initialize pipeline blocks
            _fileValidationBlock = CreateFileValidationBlock();
            _extractionBlock = CreateExtractionBlock();
            _batchingBlock = CreateBatchingBlock();
            _apiProcessingBlock = CreateApiProcessingBlock();
            _documentUpdateBlock = CreateDocumentUpdateBlock();
            _completionBlock = CreateCompletionBlock();

            // Link pipeline blocks
            InitializePipeline();

            _logger.LogInformation("Document processing service initialized with TPL Dataflow pipeline");
        }

        /// <summary>
        /// Processes multiple documents through the TPL Dataflow pipeline
        /// </summary>
        public async Task<DocumentProcessingResult> ProcessDocumentsAsync(
            IEnumerable<string> filePaths,
            IProgress<ProcessingProgressReport>? progress = null,
            CancellationToken cancellationToken = default)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _globalCancellation.Token);

            var filePathList = filePaths.ToList();
            var startTime = DateTime.UtcNow;

            var result = new DocumentProcessingResult
            {
                TotalFiles = filePathList.Count,
                StartTime = startTime
            };

            _logger.LogInformation("Starting TPL Dataflow pipeline processing for {FileCount} files", filePathList.Count);

            try
            {
                // Start progress reporting
                var progressTask = StartProgressReporting(progress, linkedCts.Token);

                // Feed files into pipeline
                var processingTask = FeedFilesIntoPipeline(filePathList, linkedCts.Token);

                // Wait for completion
                await Task.WhenAll(progressTask, processingTask);

                result.Success = true;
                result.EndTime = DateTime.UtcNow;
                result.ProcessingDuration = result.EndTime - result.StartTime;
                result.SuccessfulFiles = _statistics.ProcessedFiles;
                result.FailedFiles = _statistics.FailedFiles;

                _logger.LogInformation(
                    "Pipeline processing completed: {SuccessfulFiles}/{TotalFiles} files in {Duration:F2}s",
                    result.SuccessfulFiles, result.TotalFiles, result.ProcessingDuration.TotalSeconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Document processing was cancelled");
                result.Success = false;
                result.ErrorMessage = "Processing was cancelled by user";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pipeline processing failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
            }

            return result;
        }

        /// <summary>
        /// Processes a single document with detailed progress reporting
        /// </summary>
        public async Task<SingleDocumentProcessingResult> ProcessSingleDocumentAsync(
            string filePath,
            IProgress<ProcessingProgressReport>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = await ProcessDocumentsAsync(new[] { filePath }, progress, cancellationToken);

            return new SingleDocumentProcessingResult
            {
                FilePath = filePath,
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                Exception = result.Exception,
                ProcessingDuration = result.ProcessingDuration,
                HyperlinksProcessed = result.TotalHyperlinksProcessed,
                HyperlinksUpdated = result.TotalHyperlinksUpdated
            };
        }

        /// <summary>
        /// Validates documents before processing
        /// </summary>
        public async Task<DocumentValidationSummary> ValidateDocumentsAsync(
            IEnumerable<string> filePaths,
            CancellationToken cancellationToken = default)
        {
            var validationTasks = filePaths.Select(filePath => _documentProcessor.ValidateDocumentAsync(filePath, cancellationToken));
            var validationResults = await Task.WhenAll(validationTasks);

            return new DocumentValidationSummary
            {
                TotalFiles = validationResults.Length,
                ValidFiles = validationResults.Count(r => r.IsValid),
                InvalidFiles = validationResults.Count(r => !r.IsValid),
                FileValidations = validationResults.ToList()
            };
        }

        /// <summary>
        /// Gets processing statistics for completed operations
        /// </summary>
        public ProcessingStatistics GetProcessingStatistics() => _statistics.Clone();

        /// <summary>
        /// Configures pipeline options for processing
        /// </summary>
        public void ConfigurePipeline(PipelineConfiguration options)
        {
            // Update pipeline configuration - would require pipeline restart for full effect
            _logger.LogInformation("Pipeline configuration updated");
        }

        /// <summary>
        /// Cancels all ongoing processing operations
        /// </summary>
        public async Task CancelAllProcessingAsync()
        {
            _globalCancellation.Cancel();

            // Wait for pipeline to drain
            _fileValidationBlock.Complete();
            await _completionBlock.Completion.ConfigureAwait(false);

            _logger.LogInformation("All processing operations cancelled");
        }

        /// <summary>
        /// Initializes the TPL Dataflow pipeline blocks
        /// </summary>
        private void InitializePipeline()
        {
            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

            // Link all pipeline blocks
            _fileValidationBlock.LinkTo(_extractionBlock, linkOptions);
            _extractionBlock.LinkTo(_batchingBlock, linkOptions);
            _batchingBlock.LinkTo(_apiProcessingBlock, linkOptions);
            _apiProcessingBlock.LinkTo(_documentUpdateBlock, linkOptions);
            _documentUpdateBlock.LinkTo(_completionBlock, linkOptions);

            _logger.LogDebug("TPL Dataflow pipeline blocks initialized and linked");
        }

        /// <summary>
        /// Creates file validation block with proper error handling
        /// </summary>
        private TransformBlock<string, DocumentInput> CreateFileValidationBlock()
        {
            return new TransformBlock<string, DocumentInput>(
                async filePath =>
                {
                    try
                    {
                        await ReportProgress(ProcessingStage.FileValidation, filePath, "Validating file access");

                        if (!File.Exists(filePath))
                            throw new FileNotFoundException($"File not found: {filePath}");

                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.Length == 0)
                            throw new InvalidOperationException($"File is empty: {filePath}");

                        // Test file accessibility
                        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                        return new DocumentInput
                        {
                            FilePath = filePath,
                            FileName = fileInfo.Name,
                            FileSize = fileInfo.Length,
                            ProcessingId = Guid.NewGuid(),
                            InputTimestamp = DateTime.UtcNow
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "File validation failed for {FilePath}", filePath);
                        OnProcessingError(new ProcessingErrorEventArgs
                        {
                            FilePath = filePath,
                            Stage = ProcessingStage.FileValidation,
                            Exception = ex
                        });
                        throw;
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = _pipelineOptions.FileValidationParallelism,
                    BoundedCapacity = _pipelineOptions.BoundedCapacity,
                    CancellationToken = _globalCancellation.Token
                });
        }

        /// <summary>
        /// Creates the extraction block for hyperlink extraction
        /// </summary>
        private TransformBlock<DocumentInput, DocumentExtractionResult> CreateExtractionBlock()
        {
            return new TransformBlock<DocumentInput, DocumentExtractionResult>(
                async input =>
                {
                    try
                    {
                        await ReportProgress(ProcessingStage.HyperlinkExtraction, input.FileName, "Extracting hyperlinks");

                        var hyperlinks = await _documentProcessor.ExtractHyperlinksAsync(input.FilePath);

                        return new DocumentExtractionResult
                        {
                            Input = input,
                            Hyperlinks = hyperlinks,
                            ExtractionTimestamp = DateTime.UtcNow,
                            Success = true
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Hyperlink extraction failed for {FileName}", input.FileName);
                        return new DocumentExtractionResult
                        {
                            Input = input,
                            Success = false,
                            ErrorMessage = ex.Message,
                            Exception = ex
                        };
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = _pipelineOptions.ExtractionParallelism,
                    BoundedCapacity = _pipelineOptions.BoundedCapacity,
                    CancellationToken = _globalCancellation.Token
                });
        }

        /// <summary>
        /// Creates the batching block for efficient processing
        /// </summary>
        private TransformManyBlock<DocumentExtractionResult, HyperlinkProcessingBatch> CreateBatchingBlock()
        {
            return new TransformManyBlock<DocumentExtractionResult, HyperlinkProcessingBatch>(
                async extractionResult =>
                {
                    try
                    {
                        if (!extractionResult.Success)
                        {
                            return new[] { new HyperlinkProcessingBatch { Documents = { extractionResult } } };
                        }

                        // Create batch for processing
                        var batch = new HyperlinkProcessingBatch
                        {
                            Documents = { extractionResult },
                            LookupIds = ExtractLookupIds(extractionResult.Hyperlinks),
                            BatchTimestamp = DateTime.UtcNow
                        };

                        return new[] { batch };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Batching failed for {FileName}", extractionResult.Input.FileName);
                        return Array.Empty<HyperlinkProcessingBatch>();
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = _pipelineOptions.HyperlinkProcessingParallelism,
                    BoundedCapacity = _pipelineOptions.BoundedCapacity,
                    CancellationToken = _globalCancellation.Token
                });
        }

        /// <summary>
        /// Creates the API processing block
        /// </summary>
        private TransformBlock<HyperlinkProcessingBatch, ApiProcessingResult> CreateApiProcessingBlock()
        {
            return new TransformBlock<HyperlinkProcessingBatch, ApiProcessingResult>(
                async batch =>
                {
                    try
                    {
                        await ReportProgress(ProcessingStage.ApiProcessing,
                            batch.Documents.FirstOrDefault()?.Input.FileName ?? "Batch",
                            $"Processing {batch.LookupIds.Count} lookup IDs");

                        var apiResponse = await _apiService.GetHyperlinkDataAsync(batch.LookupIds);

                        return new ApiProcessingResult
                        {
                            HyperlinkResult = new Core.Models.HyperlinkProcessingResult
                            {
                                ExtractionResult = batch.Documents.FirstOrDefault() ?? new DocumentExtractionResult(),
                                ProcessedHyperlinks = batch.Documents.SelectMany(d => d.Hyperlinks).ToList(),
                                LookupIds = batch.LookupIds,
                                Success = true
                            },
                            ApiResults = apiResponse.Data,
                            ApiCallTimestamp = DateTime.UtcNow,
                            Success = apiResponse.Success
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "API processing failed for batch");
                        return new ApiProcessingResult
                        {
                            Success = false,
                            ErrorMessage = ex.Message,
                            Exception = ex
                        };
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = _pipelineOptions.ApiProcessingParallelism,
                    BoundedCapacity = _pipelineOptions.BoundedCapacity,
                    CancellationToken = _globalCancellation.Token
                });
        }

        /// <summary>
        /// Creates the document update block
        /// </summary>
        private TransformBlock<ApiProcessingResult, DocumentUpdateResult> CreateDocumentUpdateBlock()
        {
            return new TransformBlock<ApiProcessingResult, DocumentUpdateResult>(
                async apiResult =>
                {
                    try
                    {
                        await ReportProgress(ProcessingStage.DocumentUpdate,
                            apiResult.HyperlinkResult.ExtractionResult.Input.FileName,
                            "Updating document");

                        var updateResult = await _documentProcessor.UpdateHyperlinksAsync(
                            apiResult.HyperlinkResult.ExtractionResult.Input.FilePath,
                            apiResult.HyperlinkResult.ProcessedHyperlinks);

                        return new DocumentUpdateResult
                        {
                            ApiResult = apiResult,
                            UpdatedHyperlinks = apiResult.HyperlinkResult.ProcessedHyperlinks,
                            UpdateTimestamp = DateTime.UtcNow,
                            Success = updateResult.Success,
                            HyperlinksUpdated = updateResult.UpdatedCount
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Document update failed");
                        return new DocumentUpdateResult
                        {
                            ApiResult = apiResult,
                            Success = false,
                            ErrorMessage = ex.Message,
                            Exception = ex
                        };
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = _pipelineOptions.DocumentUpdateParallelism,
                    BoundedCapacity = _pipelineOptions.BoundedCapacity,
                    CancellationToken = _globalCancellation.Token
                });
        }

        /// <summary>
        /// Creates the completion block
        /// </summary>
        private ActionBlock<DocumentUpdateResult> CreateCompletionBlock()
        {
            return new ActionBlock<DocumentUpdateResult>(
                async updateResult =>
                {
                    try
                    {
                        await ReportProgress(ProcessingStage.Completion,
                            updateResult.ApiResult.HyperlinkResult.ExtractionResult.Input.FileName,
                            updateResult.Success ? "Processing completed" : "Processing failed");

                        if (updateResult.Success)
                        {
                            _statistics.ProcessedFiles++;
                        }
                        else
                        {
                            _statistics.FailedFiles++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in completion block");
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = _pipelineOptions.CompletionParallelism,
                    BoundedCapacity = _pipelineOptions.BoundedCapacity,
                    CancellationToken = _globalCancellation.Token
                });
        }

        /// <summary>
        /// Feeds files into the pipeline and waits for completion
        /// </summary>
        private async Task FeedFilesIntoPipeline(List<string> filePaths, CancellationToken cancellationToken)
        {
            foreach (var filePath in filePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!await _fileValidationBlock.SendAsync(filePath, cancellationToken))
                {
                    _logger.LogWarning("Failed to send file to pipeline: {FilePath}", filePath);
                }
            }

            _fileValidationBlock.Complete();
            await _completionBlock.Completion.ConfigureAwait(false);
        }

        /// <summary>
        /// Starts progress reporting task
        /// </summary>
        private async Task StartProgressReporting(IProgress<ProcessingProgressReport>? progress, CancellationToken cancellationToken)
        {
            if (progress == null) return;

            try
            {
                await foreach (var progressReport in _progressChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    progress.Report(progressReport);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }

        /// <summary>
        /// Reports progress through the channel
        /// </summary>
        private async Task ReportProgress(ProcessingStage stage, string fileName, string message)
        {
            try
            {
                var progressReport = new ProcessingProgressReport
                {
                    Stage = stage,
                    CurrentFile = fileName,
                    Message = message,
                    Timestamp = DateTime.UtcNow,
                    ProcessedCount = _statistics.ProcessedFiles,
                    TotalCount = _statistics.TotalFiles
                };

                await _progressChannel.Writer.WriteAsync(progressReport, _globalCancellation.Token);
            }
            catch (InvalidOperationException)
            {
                // Channel closed, ignore
            }
        }

        /// <summary>
        /// Extracts lookup IDs from hyperlinks
        /// </summary>
        private List<string> ExtractLookupIds(List<HyperlinkData> hyperlinks)
        {
            return hyperlinks
                .Where(h => !string.IsNullOrEmpty(h.Address) || !string.IsNullOrEmpty(h.SubAddress))
                .Select(h => ExtractLookupId(h.Address, h.SubAddress))
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Extracts lookup ID from address and subaddress
        /// </summary>
        private string ExtractLookupId(string address, string subAddress)
        {
            // This is a simplified version - the real implementation would use regex patterns
            var fullAddress = address + (!string.IsNullOrEmpty(subAddress) ? "#" + subAddress : "");

            // Extract TSRC or CMS patterns
            if (fullAddress.Contains("TSRC-") || fullAddress.Contains("CMS-"))
            {
                var start = Math.Max(fullAddress.IndexOf("TSRC-"), fullAddress.IndexOf("CMS-"));
                if (start >= 0)
                {
                    var end = fullAddress.IndexOf(" ", start);
                    if (end < 0) end = fullAddress.Length;
                    return fullAddress.Substring(start, end - start);
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Fires processing stage changed event
        /// </summary>
        private void OnProcessingStageChanged(ProcessingStageChangedEventArgs args)
        {
            ProcessingStageChanged?.Invoke(this, args);
        }

        /// <summary>
        /// Fires processing error event
        /// </summary>
        private void OnProcessingError(ProcessingErrorEventArgs args)
        {
            ProcessingError?.Invoke(this, args);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _globalCancellation?.Cancel();
            _progressChannel?.Writer?.Complete();

            _fileValidationBlock?.Complete();
            _extractionBlock?.Complete();
            _batchingBlock?.Complete();
            _apiProcessingBlock?.Complete();
            _documentUpdateBlock?.Complete();
            _completionBlock?.Complete();

            _globalCancellation?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Processing batch for efficient API calls
    /// </summary>
    public class HyperlinkProcessingBatch
    {
        public List<DocumentExtractionResult> Documents { get; set; } = new();
        public List<string> LookupIds { get; set; } = new();
        public int BatchSize => Documents.Count;
        public DateTime BatchTimestamp { get; set; } = DateTime.UtcNow;
    }
}
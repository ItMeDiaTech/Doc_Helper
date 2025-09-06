
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Doc_Helper.Data.Entities;

namespace Doc_Helper.Data.Repositories
{
    /// <summary>
    /// Repository for document data access operations
    /// </summary>
    public class DocumentRepository : IDocumentRepository
    {
        private readonly DocHelperDbContext _context;

        public DocumentRepository(DocHelperDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // IRepository<T> implementation
        public async Task<DocumentData?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

            return entity != null ? MapToModel(entity) : null;
        }

        public async Task<IEnumerable<DocumentData>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _context.Documents
                .AsNoTracking()
                .Where(d => !d.IsDeleted)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<IEnumerable<DocumentData>> FindAsync(Expression<Func<DocumentData, bool>> predicate, CancellationToken cancellationToken = default)
        {
            // Simple implementation - in production would use expression translation
            var entities = await _context.Documents.AsNoTracking().Where(d => !d.IsDeleted).ToListAsync(cancellationToken);
            var models = entities.Select(MapToModel);
            return models.Where(predicate.Compile());
        }

        public async Task<DocumentData?> FirstOrDefaultAsync(Expression<Func<DocumentData, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var results = await FindAsync(predicate, cancellationToken);
            return results.FirstOrDefault();
        }

        public async Task<bool> AnyAsync(Expression<Func<DocumentData, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var results = await FindAsync(predicate, cancellationToken);
            return results.Any();
        }

        public async Task<int> CountAsync(Expression<Func<DocumentData, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            if (predicate == null)
                return await _context.Documents.Where(d => !d.IsDeleted).CountAsync(cancellationToken);

            var results = await FindAsync(predicate, cancellationToken);
            return results.Count();
        }

        public IQueryable<DocumentData> Query()
        {
            return _context.Documents.AsNoTracking().Where(d => !d.IsDeleted).Select(e => MapToModel(e));
        }

        public IQueryable<DocumentData> QueryIncluding(params Expression<Func<DocumentData, object>>[] includes)
        {
            return Query(); // Simple implementation
        }

        public async Task<IEnumerable<DocumentData>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var entities = await _context.Documents
                .AsNoTracking()
                .Where(d => !d.IsDeleted)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<IEnumerable<DocumentData>> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<DocumentData, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var results = await FindAsync(predicate, cancellationToken);
            return results.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        }

        public async Task<DocumentData> AddAsync(DocumentData entity, CancellationToken cancellationToken = default)
        {
            var documentEntity = MapToEntity(entity);
            _context.Documents.Add(documentEntity);
            await _context.SaveChangesAsync(cancellationToken);
            return MapToModel(documentEntity);
        }

        public async Task<IEnumerable<DocumentData>> AddRangeAsync(IEnumerable<DocumentData> entities, CancellationToken cancellationToken = default)
        {
            var documentEntities = entities.Select(MapToEntity).ToList();
            _context.Documents.AddRange(documentEntities);
            await _context.SaveChangesAsync(cancellationToken);
            return documentEntities.Select(MapToModel);
        }

        public async Task<DocumentData> UpdateAsync(DocumentData entity, CancellationToken cancellationToken = default)
        {
            var documentEntity = await _context.Documents.FirstOrDefaultAsync(d => d.Id == entity.Id, cancellationToken);
            if (documentEntity == null)
                throw new InvalidOperationException($"Document with ID {entity.Id} not found");

            UpdateEntityFromModel(documentEntity, entity);
            await _context.SaveChangesAsync(cancellationToken);
            return MapToModel(documentEntity);
        }

        public async Task<IEnumerable<DocumentData>> UpdateRangeAsync(IEnumerable<DocumentData> entities, CancellationToken cancellationToken = default)
        {
            var results = new List<DocumentData>();
            foreach (var entity in entities)
            {
                results.Add(await UpdateAsync(entity, cancellationToken));
            }
            return results;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
            if (entity == null) return false;

            _context.Documents.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeleteAsync(DocumentData entity, CancellationToken cancellationToken = default)
        {
            return await DeleteAsync(entity.Id, cancellationToken);
        }

        public async Task<int> DeleteRangeAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
        {
            var entities = await _context.Documents
                .Where(d => ids.Contains(d.Id))
                .ToListAsync(cancellationToken);

            _context.Documents.RemoveRange(entities);
            await _context.SaveChangesAsync(cancellationToken);
            return entities.Count;
        }

        public async Task<int> DeleteRangeAsync(IEnumerable<DocumentData> entities, CancellationToken cancellationToken = default)
        {
            var ids = entities.Select(e => e.Id);
            return await DeleteRangeAsync(ids, cancellationToken);
        }

        public async Task<int> DeleteRangeAsync(Expression<Func<DocumentData, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var entities = await FindAsync(predicate, cancellationToken);
            var ids = entities.Select(e => e.Id);
            return await DeleteRangeAsync(ids, cancellationToken);
        }

        public async Task<bool> SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
            if (entity == null) return false;

            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> SoftDeleteAsync(DocumentData entity, CancellationToken cancellationToken = default)
        {
            return await SoftDeleteAsync(entity.Id, cancellationToken);
        }

        public async Task<IEnumerable<DocumentData>> GetAllIncludingDeletedAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _context.Documents
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<int> BulkInsertAsync(IEnumerable<DocumentData> entities, CancellationToken cancellationToken = default)
        {
            var documentEntities = entities.Select(MapToEntity).ToList();
            _context.Documents.AddRange(documentEntities);
            await _context.SaveChangesAsync(cancellationToken);
            return documentEntities.Count;
        }

        public async Task<int> BulkUpdateAsync(IEnumerable<DocumentData> entities, CancellationToken cancellationToken = default)
        {
            var count = 0;
            foreach (var entity in entities)
            {
                var documentEntity = await _context.Documents.FirstOrDefaultAsync(d => d.Id == entity.Id, cancellationToken);
                if (documentEntity != null)
                {
                    UpdateEntityFromModel(documentEntity, entity);
                    count++;
                }
            }
            await _context.SaveChangesAsync(cancellationToken);
            return count;
        }

        public async Task<int> BulkDeleteAsync(Expression<Func<DocumentData, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await DeleteRangeAsync(predicate, cancellationToken);
        }

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await operation();
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        // IDocumentRepository specific methods
        public async Task<DocumentData?> GetByFilePathAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.FilePath == filePath && !d.IsDeleted, cancellationToken);

            return entity != null ? MapToModel(entity) : null;
        }

        public async Task<DocumentData?> GetByFileHashAsync(string fileHash, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.FileHash == fileHash && !d.IsDeleted, cancellationToken);

            return entity != null ? MapToModel(entity) : null;
        }

        public async Task<IEnumerable<DocumentData>> GetByProcessingStatusAsync(string processingStatus, CancellationToken cancellationToken = default)
        {
            var entities = await _context.Documents
                .AsNoTracking()
                .Where(d => d.ProcessingStatus == processingStatus && !d.IsDeleted)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<IEnumerable<DocumentData>> GetModifiedSinceAsync(DateTime modifiedSince, CancellationToken cancellationToken = default)
        {
            var entities = await _context.Documents
                .AsNoTracking()
                .Where(d => d.FileModifiedDate > modifiedSince && !d.IsDeleted)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<DocumentData?> GetWithHyperlinksAsync(int documentId, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Documents
                .Include(d => d.Hyperlinks)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, cancellationToken);

            return entity != null ? MapToModel(entity) : null;
        }

        public async Task<IEnumerable<DocumentData>> GetAllWithHyperlinksAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _context.Documents
                .Include(d => d.Hyperlinks)
                .AsNoTracking()
                .Where(d => !d.IsDeleted)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<IEnumerable<DocumentData>> GetPendingProcessingAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _context.Documents
                .AsNoTracking()
                .Where(d => d.ProcessingStatus == "Pending" && !d.IsDeleted)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<IEnumerable<DocumentData>> GetFailedProcessingAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _context.Documents
                .AsNoTracking()
                .Where(d => d.ProcessingStatus == "Failed" && !d.IsDeleted)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<int> UpdateProcessingStatusAsync(int documentId, string status, string? notes = null, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
            if (entity == null) return 0;

            entity.ProcessingStatus = status;
            if (notes != null) entity.ProcessingNotes = notes;
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return 1;
        }

        public async Task<int> UpdateHyperlinkCountsAsync(int documentId, int hyperlinkCount, int processedCount, int failedCount, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
            if (entity == null) return 0;

            entity.HyperlinkCount = hyperlinkCount;
            entity.ProcessedHyperlinkCount = processedCount;
            entity.FailedHyperlinkCount = failedCount;
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return 1;
        }

        public async Task<bool> ExistsByPathAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return await _context.Documents
                .AsNoTracking()
                .AnyAsync(d => d.FilePath == filePath && !d.IsDeleted, cancellationToken);
        }

        public async Task<bool> ExistsByHashAsync(string fileHash, CancellationToken cancellationToken = default)
        {
            return await _context.Documents
                .AsNoTracking()
                .AnyAsync(d => d.FileHash == fileHash && !d.IsDeleted, cancellationToken);
        }

        public async Task<IEnumerable<DocumentData>> GetOutdatedDocumentsAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _context.Documents
                .AsNoTracking()
                .Where(d => !d.IsDeleted && d.LastProcessedAt < d.FileModifiedDate)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<IEnumerable<DocumentData>> GetBySyncStatusAsync(DateTime? lastSyncedBefore = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Documents.AsNoTracking().Where(d => !d.IsDeleted);

            if (lastSyncedBefore.HasValue)
            {
                query = query.Where(d => d.UpdatedAt < lastSyncedBefore.Value);
            }

            var entities = await query.ToListAsync(cancellationToken);
            return entities.Select(MapToModel);
        }

        public async Task<int> UpdateSyncStatusAsync(int documentId, DateTime syncTimestamp, string? excelPath = null, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
            if (entity == null) return 0;

            entity.UpdatedAt = syncTimestamp;
            await _context.SaveChangesAsync(cancellationToken);
            return 1;
        }

        public async Task<Dictionary<string, int>> GetProcessingStatusCountsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Documents
                .AsNoTracking()
                .Where(d => !d.IsDeleted)
                .GroupBy(d => d.ProcessingStatus)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);
        }

        public async Task<Dictionary<string, int>> GetDocumentTypeCountsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Documents
                .AsNoTracking()
                .Where(d => !d.IsDeleted)
                .GroupBy(d => Path.GetExtension(d.FileName).ToLowerInvariant())
                .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);
        }

        public async Task<long> GetTotalFileSizeAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Documents
                .AsNoTracking()
                .Where(d => !d.IsDeleted)
                .SumAsync(d => d.FileSizeBytes, cancellationToken);
        }

        public async Task<int> BulkUpdateProcessingStatusAsync(IEnumerable<int> documentIds, string status, CancellationToken cancellationToken = default)
        {
            var entities = await _context.Documents
                .Where(d => documentIds.Contains(d.Id))
                .ToListAsync(cancellationToken);

            foreach (var entity in entities)
            {
                entity.ProcessingStatus = status;
                entity.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return entities.Count;
        }

        public async Task<int> DeleteOrphanedDocumentsAsync(CancellationToken cancellationToken = default)
        {
            var orphanedEntities = await _context.Documents
                .Where(d => !File.Exists(d.FilePath))
                .ToListAsync(cancellationToken);

            _context.Documents.RemoveRange(orphanedEntities);
            await _context.SaveChangesAsync(cancellationToken);
            return orphanedEntities.Count;
        }

        public string GenerateFileHash(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return Convert.ToBase64String(hash);
        }

        public async Task<bool> ValidateFileIntegrityAsync(int documentId, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

            if (entity == null || !File.Exists(entity.FilePath))
                return false;

            var currentHash = GenerateFileHash(entity.FilePath);
            return currentHash == entity.FileHash;
        }

        // Helper methods for mapping
        private static DocumentData MapToModel(DocumentEntity entity)
        {
            return new DocumentData
            {
                Id = entity.Id,
                FilePath = entity.FilePath,
                FileName = entity.FileName,
                FileSize = entity.FileSizeBytes,
                FileHash = entity.FileHash,
                FileLastModified = entity.FileModifiedDate,
                ProcessingStatus = entity.ProcessingStatus,
                ProcessingNotes = entity.ProcessingNotes,
                ProcessingStartTime = entity.LastProcessedAt,
                ProcessingEndTime = entity.LastValidatedAt,
                HyperlinkCount = entity.HyperlinkCount,
                ProcessedHyperlinkCount = entity.ProcessedHyperlinkCount,
                FailedHyperlinkCount = entity.FailedHyperlinkCount,
                ExcelPath = entity.SourceExcelPath,
                LastSyncedAt = entity.LastSyncedFromExcel,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                IsDeleted = entity.IsDeleted
            };
        }

        private static DocumentEntity MapToEntity(DocumentData model)
        {
            return new DocumentEntity
            {
                Id = model.Id,
                FilePath = model.FilePath,
                FileName = model.FileName,
                FileSizeBytes = model.FileSize,
                FileHash = model.FileHash,
                FileModifiedDate = model.FileLastModified,
                ProcessingStatus = model.ProcessingStatus,
                ProcessingNotes = model.ProcessingNotes ?? string.Empty,
                LastProcessedAt = model.ProcessingStartTime,
                LastValidatedAt = model.ProcessingEndTime,
                HyperlinkCount = model.HyperlinkCount,
                ProcessedHyperlinkCount = model.ProcessedHyperlinkCount,
                FailedHyperlinkCount = model.FailedHyperlinkCount,
                SourceExcelPath = model.ExcelPath ?? string.Empty,
                LastSyncedFromExcel = model.LastSyncedAt,
                CreatedAt = model.CreatedAt,
                UpdatedAt = model.UpdatedAt,
                IsDeleted = model.IsDeleted
            };
        }

        private static void UpdateEntityFromModel(DocumentEntity entity, DocumentData model)
        {
            entity.FilePath = model.FilePath;
            entity.FileName = model.FileName;
            entity.FileSizeBytes = model.FileSize;
            entity.FileHash = model.FileHash;
            entity.FileModifiedDate = model.FileLastModified;
            entity.ProcessingStatus = model.ProcessingStatus;
            entity.ProcessingNotes = model.ProcessingNotes ?? string.Empty;
            entity.LastProcessedAt = model.ProcessingStartTime;
            entity.LastValidatedAt = model.ProcessingEndTime;
            entity.HyperlinkCount = model.HyperlinkCount;
            entity.ProcessedHyperlinkCount = model.ProcessedHyperlinkCount;
            entity.FailedHyperlinkCount = model.FailedHyperlinkCount;
            entity.SourceExcelPath = model.ExcelPath ?? string.Empty;
            entity.LastSyncedFromExcel = model.LastSyncedAt;
            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}

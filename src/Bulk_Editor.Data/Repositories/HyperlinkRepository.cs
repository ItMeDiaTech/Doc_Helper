using System;
using System.Collections.Generic;
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
    /// Repository for hyperlink data access operations
    /// </summary>
    public class HyperlinkRepository : IHyperlinkRepository
    {
        private readonly DocHelperDbContext _context;

        public HyperlinkRepository(DocHelperDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // IRepository<T> implementation
        public async Task<HyperlinkData?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Hyperlinks
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

            return entity != null ? MapToModel(entity) : null;
        }

        public async Task<IEnumerable<HyperlinkData>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _context.Hyperlinks
                .AsNoTracking()
                .Where(h => !h.IsDeleted)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<IEnumerable<HyperlinkData>> FindAsync(Expression<Func<HyperlinkData, bool>> predicate, CancellationToken cancellationToken = default)
        {
            // Simple implementation - in production would use expression translation
            var entities = await _context.Hyperlinks.AsNoTracking().Where(h => !h.IsDeleted).ToListAsync(cancellationToken);
            var models = entities.Select(MapToModel);
            return models.Where(predicate.Compile());
        }

        public async Task<HyperlinkData?> FirstOrDefaultAsync(Expression<Func<HyperlinkData, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var results = await FindAsync(predicate, cancellationToken);
            return results.FirstOrDefault();
        }

        public async Task<bool> AnyAsync(Expression<Func<HyperlinkData, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var results = await FindAsync(predicate, cancellationToken);
            return results.Any();
        }

        public async Task<int> CountAsync(Expression<Func<HyperlinkData, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            if (predicate == null)
                return await _context.Hyperlinks.Where(h => !h.IsDeleted).CountAsync(cancellationToken);

            var results = await FindAsync(predicate, cancellationToken);
            return results.Count();
        }

        public IQueryable<HyperlinkData> Query()
        {
            return _context.Hyperlinks.AsNoTracking().Where(h => !h.IsDeleted).Select(e => MapToModel(e));
        }

        public IQueryable<HyperlinkData> QueryIncluding(params Expression<Func<HyperlinkData, object>>[] includes)
        {
            return Query(); // Simple implementation
        }

        public async Task<IEnumerable<HyperlinkData>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var entities = await _context.Hyperlinks
                .AsNoTracking()
                .Where(h => !h.IsDeleted)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<IEnumerable<HyperlinkData>> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<HyperlinkData, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var results = await FindAsync(predicate, cancellationToken);
            return results.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        }

        public async Task<HyperlinkData> AddAsync(HyperlinkData entity, CancellationToken cancellationToken = default)
        {
            var hyperlinkEntity = MapToEntity(entity);
            _context.Hyperlinks.Add(hyperlinkEntity);
            await _context.SaveChangesAsync(cancellationToken);
            return MapToModel(hyperlinkEntity);
        }

        public async Task<IEnumerable<HyperlinkData>> AddRangeAsync(IEnumerable<HyperlinkData> entities, CancellationToken cancellationToken = default)
        {
            var hyperlinkEntities = entities.Select(MapToEntity).ToList();
            _context.Hyperlinks.AddRange(hyperlinkEntities);
            await _context.SaveChangesAsync(cancellationToken);
            return hyperlinkEntities.Select(MapToModel);
        }

        public async Task<HyperlinkData> UpdateAsync(HyperlinkData entity, CancellationToken cancellationToken = default)
        {
            var hyperlinkEntity = await _context.Hyperlinks.FirstOrDefaultAsync(h => h.Id == entity.Id, cancellationToken);
            if (hyperlinkEntity == null)
                throw new InvalidOperationException($"Hyperlink with ID {entity.Id} not found");

            UpdateEntityFromModel(hyperlinkEntity, entity);
            await _context.SaveChangesAsync(cancellationToken);
            return MapToModel(hyperlinkEntity);
        }

        public async Task<IEnumerable<HyperlinkData>> UpdateRangeAsync(IEnumerable<HyperlinkData> entities, CancellationToken cancellationToken = default)
        {
            var results = new List<HyperlinkData>();
            foreach (var entity in entities)
            {
                results.Add(await UpdateAsync(entity, cancellationToken));
            }
            return results;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Hyperlinks.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
            if (entity == null) return false;

            _context.Hyperlinks.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeleteAsync(HyperlinkData entity, CancellationToken cancellationToken = default)
        {
            return await DeleteAsync(entity.Id, cancellationToken);
        }

        public async Task<int> DeleteRangeAsync(Expression<Func<HyperlinkData, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var entities = await FindAsync(predicate, cancellationToken);
            var ids = entities.Select(e => e.Id);
            var hyperlinkEntities = await _context.Hyperlinks
                .Where(h => ids.Contains(h.Id))
                .ToListAsync(cancellationToken);

            _context.Hyperlinks.RemoveRange(hyperlinkEntities);
            await _context.SaveChangesAsync(cancellationToken);
            return hyperlinkEntities.Count;
        }

        public async Task<bool> SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Hyperlinks.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
            if (entity == null) return false;

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> SoftDeleteAsync(HyperlinkData entity, CancellationToken cancellationToken = default)
        {
            return await SoftDeleteAsync(entity.Id, cancellationToken);
        }

        public async Task<IEnumerable<HyperlinkData>> GetAllIncludingDeletedAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _context.Hyperlinks
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<int> BulkInsertAsync(IEnumerable<HyperlinkData> entities, CancellationToken cancellationToken = default)
        {
            var hyperlinkEntities = entities.Select(MapToEntity).ToList();
            _context.Hyperlinks.AddRange(hyperlinkEntities);
            await _context.SaveChangesAsync(cancellationToken);
            return hyperlinkEntities.Count;
        }

        public async Task<int> BulkUpdateAsync(IEnumerable<HyperlinkData> entities, CancellationToken cancellationToken = default)
        {
            var count = 0;
            foreach (var entity in entities)
            {
                var hyperlinkEntity = await _context.Hyperlinks.FirstOrDefaultAsync(h => h.Id == entity.Id, cancellationToken);
                if (hyperlinkEntity != null)
                {
                    UpdateEntityFromModel(hyperlinkEntity, entity);
                    count++;
                }
            }
            await _context.SaveChangesAsync(cancellationToken);
            return count;
        }

        public async Task<int> BulkDeleteAsync(Expression<Func<HyperlinkData, bool>> predicate, CancellationToken cancellationToken = default)
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

        // IHyperlinkRepository specific methods
        public async Task<IEnumerable<HyperlinkData>> GetByDocumentIdAsync(int documentId, CancellationToken cancellationToken = default)
        {
            var entities = await _context.Hyperlinks
                .AsNoTracking()
                .Where(h => h.DocumentId == documentId && !h.IsDeleted)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<IEnumerable<HyperlinkData>> GetByAddressAsync(string address, CancellationToken cancellationToken = default)
        {
            var entities = await _context.Hyperlinks
                .AsNoTracking()
                .Where(h => h.Address == address && !h.IsDeleted)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<IEnumerable<HyperlinkData>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            var entities = await _context.Hyperlinks
                .AsNoTracking()
                .Where(h => h.Status == status && !h.IsDeleted)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<IEnumerable<HyperlinkData>> GetByProcessingStatusAsync(string processingStatus, CancellationToken cancellationToken = default)
        {
            var entities = await _context.Hyperlinks
                .AsNoTracking()
                .Where(h => h.ProcessingStatus == processingStatus && !h.IsDeleted)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<HyperlinkData?> GetByContentHashAsync(string contentHash, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Hyperlinks
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.ContentHash == contentHash && !h.IsDeleted, cancellationToken);

            return entity != null ? MapToModel(entity) : null;
        }

        public async Task<IEnumerable<HyperlinkData>> SearchByTextAsync(string searchText, CancellationToken cancellationToken = default)
        {
            var entities = await _context.Hyperlinks
                .AsNoTracking()
                .Where(h => !h.IsDeleted && (
                    h.TextToDisplay.Contains(searchText) ||
                    h.Address.Contains(searchText) ||
                    h.Title.Contains(searchText)))
                .ToListAsync(cancellationToken);

            return entities.Select(MapToModel);
        }

        public async Task<IEnumerable<HyperlinkData>> GetDuplicatesAsync(CancellationToken cancellationToken = default)
        {
            var duplicates = await _context.Hyperlinks
                .AsNoTracking()
                .Where(h => !h.IsDeleted)
                .GroupBy(h => h.ContentHash)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g)
                .ToListAsync(cancellationToken);

            return duplicates.Select(MapToModel);
        }

        public async Task<IEnumerable<HyperlinkData>> GetPendingProcessingAsync(int? documentId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Hyperlinks
                .AsNoTracking()
                .Where(h => h.ProcessingStatus == "Pending" && !h.IsDeleted);

            if (documentId.HasValue)
            {
                query = query.Where(h => h.DocumentId == documentId.Value);
            }

            var entities = await query.ToListAsync(cancellationToken);
            return entities.Select(MapToModel);
        }

        public async Task<IEnumerable<HyperlinkData>> GetFailedProcessingAsync(int? documentId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Hyperlinks
                .AsNoTracking()
                .Where(h => h.ProcessingStatus == "Failed" && !h.IsDeleted);

            if (documentId.HasValue)
            {
                query = query.Where(h => h.DocumentId == documentId.Value);
            }

            var entities = await query.ToListAsync(cancellationToken);
            return entities.Select(MapToModel);
        }

        public async Task<int> UpdateProcessingStatusAsync(IEnumerable<int> hyperlinkIds, string status, string? notes = null, CancellationToken cancellationToken = default)
        {
            var entities = await _context.Hyperlinks
                .Where(h => hyperlinkIds.Contains(h.Id))
                .ToListAsync(cancellationToken);

            foreach (var entity in entities)
            {
                entity.ProcessingStatus = status;
                if (notes != null) entity.ProcessingNotes = notes;
                entity.LastProcessedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return entities.Count;
        }

        public async Task<Dictionary<string, int>> GetStatusCountsAsync(int? documentId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Hyperlinks.AsNoTracking().Where(h => !h.IsDeleted);

            if (documentId.HasValue)
            {
                query = query.Where(h => h.DocumentId == documentId.Value);
            }

            return await query
                .GroupBy(h => h.Status)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);
        }

        public async Task<Dictionary<string, int>> GetProcessingStatusCountsAsync(int? documentId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Hyperlinks.AsNoTracking().Where(h => !h.IsDeleted);

            if (documentId.HasValue)
            {
                query = query.Where(h => h.DocumentId == documentId.Value);
            }

            return await query
                .GroupBy(h => h.ProcessingStatus)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);
        }

        public async Task<int> BulkInsertWithHashAsync(IEnumerable<HyperlinkData> hyperlinks, CancellationToken cancellationToken = default)
        {
            var entities = hyperlinks.Select(h =>
            {
                var entity = MapToEntity(h);
                entity.ContentHash = GenerateContentHash(h);
                return entity;
            }).ToList();

            _context.Hyperlinks.AddRange(entities);
            await _context.SaveChangesAsync(cancellationToken);
            return entities.Count;
        }

        public async Task<int> DeduplicateAsync(CancellationToken cancellationToken = default)
        {
            var duplicates = await _context.Hyperlinks
                .Where(h => !h.IsDeleted)
                .GroupBy(h => h.ContentHash)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.Skip(1)) // Keep first, remove rest
                .ToListAsync(cancellationToken);

            _context.Hyperlinks.RemoveRange(duplicates);
            await _context.SaveChangesAsync(cancellationToken);
            return duplicates.Count;
        }

        public string GenerateContentHash(HyperlinkData hyperlink)
        {
            return hyperlink.GenerateContentHash();
        }

        public async Task<bool> ExistsWithHashAsync(string contentHash, CancellationToken cancellationToken = default)
        {
            return await _context.Hyperlinks
                .AsNoTracking()
                .AnyAsync(h => h.ContentHash == contentHash && !h.IsDeleted, cancellationToken);
        }

        // Helper methods for mapping
        private static HyperlinkData MapToModel(HyperlinkEntity entity)
        {
            return new HyperlinkData
            {
                Id = entity.Id,
                Address = entity.Address,
                SubAddress = entity.SubAddress,
                TextToDisplay = entity.TextToDisplay,
                PageNumber = entity.PageNumber,
                LineNumber = entity.LineNumber,
                Title = entity.Title,
                ContentID = entity.ContentID,
                Status = entity.Status,
                ElementId = entity.ElementId,
                ProcessingStatus = entity.ProcessingStatus,
                LastProcessedAt = entity.LastProcessedAt,
                ProcessingNotes = entity.ProcessingNotes,
                DocumentId = entity.DocumentId,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                CreatedBy = entity.CreatedBy,
                UpdatedBy = entity.UpdatedBy
            };
        }

        private static HyperlinkEntity MapToEntity(HyperlinkData model)
        {
            return new HyperlinkEntity
            {
                Id = model.Id,
                Address = model.Address,
                SubAddress = model.SubAddress,
                TextToDisplay = model.TextToDisplay,
                PageNumber = model.PageNumber,
                LineNumber = model.LineNumber,
                Title = model.Title,
                ContentID = model.ContentID,
                Status = model.Status,
                ElementId = model.ElementId,
                ProcessingStatus = model.ProcessingStatus,
                LastProcessedAt = model.LastProcessedAt,
                ProcessingNotes = model.ProcessingNotes,
                DocumentId = model.DocumentId,
                CreatedAt = model.CreatedAt,
                UpdatedAt = model.UpdatedAt,
                CreatedBy = model.CreatedBy,
                UpdatedBy = model.UpdatedBy
            };
        }

        private static void UpdateEntityFromModel(HyperlinkEntity entity, HyperlinkData model)
        {
            entity.Address = model.Address;
            entity.SubAddress = model.SubAddress;
            entity.TextToDisplay = model.TextToDisplay;
            entity.PageNumber = model.PageNumber;
            entity.LineNumber = model.LineNumber;
            entity.Title = model.Title;
            entity.ContentID = model.ContentID;
            entity.Status = model.Status;
            entity.ElementId = model.ElementId;
            entity.ProcessingStatus = model.ProcessingStatus;
            entity.LastProcessedAt = model.LastProcessedAt;
            entity.ProcessingNotes = model.ProcessingNotes;
            entity.DocumentId = model.DocumentId;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = model.UpdatedBy;
        }
    }
}
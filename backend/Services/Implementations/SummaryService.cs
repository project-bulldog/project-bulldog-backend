using backend.Data;
using backend.Dtos.ActionItems;
using backend.Dtos.Summaries;
using backend.Models;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace backend.Services.Implementations
{
    public class SummaryService : ISummaryService
    {
        private readonly BulldogDbContext _context;
        private readonly ILogger<SummaryService> _logger;

        public SummaryService(BulldogDbContext context, ILogger<SummaryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<SummaryDto>> GetSummariesAsync()
        {
            _logger.LogInformation("Fetching all summaries");
            var summaries = await _context.Summaries
                .AsNoTracking()
                .Include(s => s.ActionItems)
                .Include(s => s.User)
                .ToListAsync();

            _logger.LogInformation("Fetched {Count} summaries", summaries.Count);

            return [.. summaries.Select(s => new SummaryDto
            {
                Id = s.Id,
                OriginalText = s.OriginalText,
                SummaryText = s.SummaryText,
                CreatedAt = s.CreatedAt,
                UserId = s.UserId,
                UserDisplayName = s.User != null ? s.User.DisplayName : "[Unknown]",
                ActionItems = [.. s.ActionItems.Select(ai => new ActionItemDto
                {
                    Id = ai.Id,
                    Text = ai.Text,
                    IsDone = ai.IsDone,
                    DueAt = ai.DueAt
                })]
            })];
        }

        public async Task<SummaryDto?> GetSummaryAsync(int id)
        {
            _logger.LogInformation("Fetching summary with id {Id}", id);
            var summary = await _context.Summaries
                .AsNoTracking()
                .Include(s => s.ActionItems)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (summary == null)
            {
                _logger.LogWarning("Summary with id {Id} not found", id);
                return null;
            }

            _logger.LogInformation("Fetched summary with id {Id}", id);

            return new SummaryDto
            {
                Id = summary.Id,
                OriginalText = summary.OriginalText,
                SummaryText = summary.SummaryText,
                CreatedAt = summary.CreatedAt,
                UserId = summary.UserId,
                UserDisplayName = summary.User != null ? summary.User.DisplayName : "[Unknown]",
                ActionItems = [.. summary.ActionItems.Select(ai => new ActionItemDto
                {
                    Id = ai.Id,
                    Text = ai.Text,
                    IsDone = ai.IsDone,
                    DueAt = ai.DueAt
                })]
            };
        }

        public async Task<SummaryDto> CreateSummaryAsync(CreateSummaryDto dto)
        {
            _logger.LogInformation("Creating a new summary for user {UserId}", dto.UserId);
            var summary = new Summary
            {
                OriginalText = dto.OriginalText,
                SummaryText = dto.SummaryText,
                UserId = dto.UserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Summaries.Add(summary);
            await _context.SaveChangesAsync();

            var loaded = await _context.Summaries
                .Include(s => s.User)
                .Include(s => s.ActionItems)
                .FirstOrDefaultAsync(s => s.Id == summary.Id);

            _logger.LogInformation("Created summary with id {Id}", summary.Id);

            return new SummaryDto
            {
                Id = summary.Id,
                OriginalText = summary.OriginalText,
                SummaryText = summary.SummaryText,
                CreatedAt = summary.CreatedAt,
                UserId = summary.UserId,
                UserDisplayName = loaded?.User?.DisplayName ?? "[Unknown]",
                ActionItems = [.. loaded?.ActionItems.Select(ai => new ActionItemDto
                {
                    Id = ai.Id,
                    Text = ai.Text,
                    IsDone = ai.IsDone,
                    DueAt = ai.DueAt
                }) ?? []]
            };
        }

        public async Task<bool> UpdateSummaryAsync(int id, UpdateSummaryDto updateDto)
        {
            _logger.LogInformation("Updating summary with id {Id}", id);
            var summary = await _context.Summaries.FindAsync(id);

            if (summary == null)
            {
                _logger.LogWarning("Summary with id {Id} not found for update", id);
                return false;
            }

            // Update fields from the DTO
            summary.OriginalText = updateDto.OriginalText;
            summary.SummaryText = updateDto.SummaryText;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated summary with id {Id}", id);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Summaries.Any(s => s.Id == id))
                {
                    _logger.LogWarning("Summary with id {Id} no longer exists during update", id);
                    return false;
                }
                _logger.LogError("Concurrency error occurred while updating summary with id {Id}", id);
                throw;
            }
        }

        public async Task<bool> DeleteSummaryAsync(int id)
        {
            _logger.LogInformation("Deleting summary with id {Id}", id);
            var summary = await _context.Summaries.FindAsync(id);
            if (summary == null)
            {
                _logger.LogWarning("Summary with id {Id} not found for deletion", id);
                return false;
            }

            _context.Summaries.Remove(summary);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted summary with id {Id}", id);
            return true;
        }
    }
}

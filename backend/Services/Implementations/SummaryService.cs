using backend.Data;
using backend.Dtos.ActionItems;
using backend.Dtos.Summaries;
using backend.Models;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations
{
    public class SummaryService : ISummaryService
    {
        private readonly BulldogDbContext _context;

        public SummaryService(BulldogDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<SummaryDto>> GetSummariesAsync()
        {
            var summaries = await _context.Summaries
                .AsNoTracking()
                .Include(s => s.ActionItems)
                .Include(s => s.User)
                .ToListAsync();

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
            var summary = await _context.Summaries
                .AsNoTracking()
                .Include(s => s.ActionItems)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (summary == null)
            {
                return null;
            }

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

            return new SummaryDto
            {
                Id = summary.Id,
                OriginalText = summary.OriginalText,
                SummaryText = summary.SummaryText,
                CreatedAt = summary.CreatedAt,
                UserId = summary.UserId,
                UserDisplayName = loaded?.User?.DisplayName ?? "[Unknown]",
                ActionItems = loaded?.ActionItems.Select(ai => new ActionItemDto
                {
                    Id = ai.Id,
                    Text = ai.Text,
                    IsDone = ai.IsDone,
                    DueAt = ai.DueAt
                }).ToList() ?? []
            };
        }


        public async Task<bool> UpdateSummaryAsync(int id, UpdateSummaryDto updateDto)
        {
            var summary = await _context.Summaries.FindAsync(id);

            if (summary == null)
            {
                return false;
            }

            // Update fields from the DTO
            summary.OriginalText = updateDto.OriginalText;
            summary.SummaryText = updateDto.SummaryText;

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Summaries.Any(s => s.Id == id))
                {
                    return false;
                }
                throw;
            }
        }

        public async Task<bool> DeleteSummaryAsync(int id)
        {
            var summary = await _context.Summaries.FindAsync(id);
            if (summary == null)
            {
                return false;
            }

            _context.Summaries.Remove(summary);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}

using backend.Data;
using backend.Dtos;
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

            return summaries.Select(s => new SummaryDto
            {
                Id = s.Id,
                OriginalText = s.OriginalText,
                SummaryText = s.SummaryText,
                CreatedAt = s.CreatedAt,
                ActionItems = s.ActionItems.Select(ai => new ActionItemDto
                {
                    Id = ai.Id,
                    Text = ai.Text,
                    IsDone = ai.IsDone,
                    DueAt = ai.DueAt
                }).ToList()
            }).ToList();
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
                ActionItems = summary.ActionItems.Select(ai => new ActionItemDto
                {
                    Id = ai.Id,
                    Text = ai.Text,
                    IsDone = ai.IsDone,
                    DueAt = ai.DueAt
                }).ToList()
            };
        }

        public async Task<SummaryDto> CreateSummaryAsync(Summary summary)
        {
            _context.Summaries.Add(summary);
            await _context.SaveChangesAsync();

            return new SummaryDto
            {
                Id = summary.Id,
                OriginalText = summary.OriginalText,
                SummaryText = summary.SummaryText,
                CreatedAt = summary.CreatedAt,
                ActionItems = summary.ActionItems.Select(ai => new ActionItemDto
                {
                    Id = ai.Id,
                    Text = ai.Text,
                    IsDone = ai.IsDone,
                    DueAt = ai.DueAt
                }).ToList()
            };
        }

        public async Task<bool> UpdateSummaryAsync(int id, Summary summary)
        {
            if (id != summary.Id)
            {
                return false;
            }

            _context.Entry(summary).State = EntityState.Modified;

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

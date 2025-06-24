using backend.Data;
using backend.Dtos.AiSummaries;
using backend.Dtos.Summaries;
using backend.Helpers;
using backend.Mappers;
using backend.Models;
using backend.Services.Auth.Interfaces;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations
{
    public class SummaryService : ISummaryService
    {
        private readonly BulldogDbContext _context;
        private readonly ILogger<SummaryService> _logger;
        private readonly IAiService _aiService;
        private readonly ICurrentUserProvider _currentUserProvider;
        private Guid CurrentUserId => _currentUserProvider.UserId;

        public SummaryService(BulldogDbContext context, ILogger<SummaryService> logger, IAiService aiService, ICurrentUserProvider currentUserProvider)
        {
            _context = context;
            _logger = logger;
            _aiService = aiService;
            _currentUserProvider = currentUserProvider;
        }

        public async Task<IEnumerable<SummaryDto>> GetSummariesAsync()
        {
            _logger.LogInformation("Fetching all summaries for user {UserId}", CurrentUserId);

            var summaries = await _context.Summaries
                .AsNoTracking()
                .Include(s => s.ActionItems)
                .Include(s => s.User)
                .Where(s => s.UserId == CurrentUserId)
                .ToListAsync();

            _logger.LogInformation("Fetched {Count} summaries", summaries.Count);

            return [.. summaries.Select(SummaryMapper.ToDto)];
        }

        public async Task<SummaryDto?> GetSummaryAsync(Guid id)
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

            return SummaryMapper.ToDto(summary);
        }

        public async Task<SummaryDto> CreateSummaryAsync(CreateSummaryDto dto)
        {
            _logger.LogInformation("Creating a new summary for user {UserId}", CurrentUserId);

            var user = await _context.Users.FindAsync(CurrentUserId);
            var utcNow = DateTime.UtcNow;
            var localNow = TimeZoneHelpers.ConvertToLocal(utcNow, user?.TimeZoneId);

            var summary = new Summary
            {
                OriginalText = dto.OriginalText,
                SummaryText = dto.SummaryText,
                UserId = CurrentUserId,
                CreatedAtUtc = utcNow,
                CreatedAtLocal = localNow
            };

            if (dto.ActionItems is { Count: > 0 })
            {
                summary.ActionItems = [.. dto.ActionItems.Select(ai => new ActionItem
                {
                    Id = Guid.NewGuid(),
                    Text = ai.Text,
                    DueAt = ai.DueAt.HasValue
                        ? DateTime.SpecifyKind(ai.DueAt.Value, DateTimeKind.Utc)
                        : null,
                    IsDone = false,
                    IsDateOnly = ai.IsDateOnly,
                    ShouldRemind = ai.ShouldRemind,
                    ReminderMinutesBeforeDue = ai.ReminderMinutesBeforeDue
                })];
            }

            _context.Summaries.Add(summary);
            await _context.SaveChangesAsync();

            foreach (var actionItem in summary.ActionItems.Where(ai => ai is { DueAt: not null, ShouldRemind: true }))
            {
                var offset = actionItem.ReminderMinutesBeforeDue ?? 60;
                var reminderTime = actionItem.DueAt.Value.AddMinutes(-offset);

                var reminder = new Reminder
                {
                    Id = Guid.NewGuid(),
                    UserId = summary.UserId,
                    ActionItemId = actionItem.Id,
                    ReminderTime = reminderTime,
                    Message = $"Reminder: {actionItem.Text}",
                    CreatedAtUtc = utcNow,
                    CreatedAtLocal = localNow,
                    MaxSendAttempts = 3,
                    IsSent = false
                };

                _context.Reminders.Add(reminder);
            }

            await _context.SaveChangesAsync();

            var loaded = await _context.Summaries
                .AsNoTracking()
                .Include(s => s.User)
                .Include(s => s.ActionItems)
                .FirstOrDefaultAsync(s => s.Id == summary.Id);

            if (loaded is null)
            {
                _logger.LogError("Failed to re-load summary with id {Id} after creation", summary.Id);
                throw new InvalidOperationException("Summary was created but could not be reloaded.");
            }

            _logger.LogInformation("Created summary with id {Id}", loaded.Id);

            return SummaryMapper.ToDto(loaded);
        }

        public async Task<SummaryDto> GenerateChunkedSummaryWithActionItemsAsync(string input, bool useMapReduce = true, string? modelOverride = null)
        {
            _logger.LogInformation("Generating AI summary with action items for user {UserId}", CurrentUserId);

            var user = await _context.Users.FindAsync(CurrentUserId);
            var utcNow = DateTime.UtcNow;
            var localNow = TimeZoneHelpers.ConvertToLocal(utcNow, user?.TimeZoneId);

            var request = new AiChunkedSummaryResponseDto(input, CurrentUserId, useMapReduce, modelOverride);
            var (summaryText, actionItems) = await _aiService.SummarizeAndExtractActionItemsChunkedAsync(request);

            var summary = new Summary
            {
                OriginalText = input,
                SummaryText = summaryText,
                UserId = CurrentUserId,
                CreatedAtUtc = utcNow,
                CreatedAtLocal = localNow,
                ActionItems = [.. actionItems.Select(ai => new ActionItem
                {
                    Id = Guid.NewGuid(),
                    Text = ai.Text,
                    DueAt = ai.DueAt,
                    IsDone = false,
                    IsDateOnly = ai.IsDateOnly
                })]
            };

            _context.Summaries.Add(summary);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated summary with id {Id} and {Count} action items", summary.Id, summary.ActionItems.Count);

            return SummaryMapper.ToDto(summary);
        }

        public async Task<bool> UpdateSummaryAsync(Guid id, UpdateSummaryDto updateDto)
        {
            _logger.LogInformation("Updating summary with id {Id}", id);

            var summary = await _context.Summaries
                .Where(s => s.Id == id && s.UserId == CurrentUserId)
                .FirstOrDefaultAsync();

            if (summary == null)
            {
                _logger.LogWarning("Summary with id {Id} not found for update", id);
                return false;
            }

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

        public async Task<bool> DeleteSummaryAsync(Guid id)
        {
            _logger.LogInformation("Deleting summary with id {Id}", id);

            var summary = await _context.Summaries
                .Where(s => s.Id == id && s.UserId == CurrentUserId)
                .FirstOrDefaultAsync();

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

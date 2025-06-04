using backend.Data;
using backend.Dtos.ActionItems;
using backend.Dtos.Summaries;
using backend.Extensions;
using backend.Models;
using backend.Services.Auth.Interfaces;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations
{
    public class ActionItemService : IActionItemService
    {
        private readonly BulldogDbContext _context;
        private readonly ILogger<ActionItemService> _logger;
        private readonly ISummaryService _summaryService;
        private readonly ICurrentUserProvider _currentUserProvider;

        public ActionItemService(
            BulldogDbContext context,
            ILogger<ActionItemService> logger,
            ISummaryService summaryService,
            ICurrentUserProvider currentUserProvider)
        {
            _context = context;
            _logger = logger;
            _summaryService = summaryService;
            _currentUserProvider = currentUserProvider;
        }

        public async Task<IEnumerable<ActionItemDto>> GetActionItemsAsync()
        {
            _logger.LogInformation("Fetching all action items");
            var actionItems = await _context.ActionItems
                .Include(ai => ai.Summary)
                .ToListAsync();

            return actionItems.Select(MapToDto).ToList();
        }

        public async Task<ActionItemDto?> GetActionItemAsync(Guid id)
        {
            _logger.LogInformation("Fetching action item with id {Id}", id);
            var actionItem = await _context.ActionItems
                .Include(ai => ai.Summary)
                .FirstOrDefaultAsync(ai => ai.Id == id);

            if (actionItem == null)
            {
                _logger.LogWarning("Action item with id {Id} not found", id);
                return null;
            }

            return MapToDto(actionItem);
        }

        public async Task<ActionItemDto> CreateActionItemAsync(CreateActionItemDto itemDto)
        {
            _logger.LogInformation("Creating a new action item");

            // 1️⃣ Ensure we have a SummaryId
            var summaryId = itemDto.SummaryId ?? (
                await _summaryService.CreateSummaryAsync(
                new CreateSummaryDto
                {
                    OriginalText = itemDto.Text,
                    SummaryText = "[Manual Summary]",
                }
            )).Id;

            // 2️⃣ Create the ActionItem
            var item = new ActionItem
            {
                Text = itemDto.Text,
                DueAt = itemDto.DueAt,
                SummaryId = summaryId,
                IsDone = false
            };

            _context.ActionItems.Add(item);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Action item created with id {Id}", item.Id);
            return MapToDto(item);
        }


        public async Task<bool> UpdateActionItemAsync(Guid id, UpdateActionItemDto itemDto)
        {

            var item = await _context.ActionItems.FindAsync(id);
            if (item == null)
            {
                _logger.LogWarning("Update failed: action item with id {Id} not found", id);
                return false;
            }

            item.Text = itemDto.Text;
            item.IsDone = itemDto.IsDone;
            item.DueAt = itemDto.DueAt;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Action item with id {Id} updated successfully", id);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.ActionItems.Any(ai => ai.Id == id))
                {
                    _logger.LogWarning("Update failed: action item with id {Id} not found", id);
                    return false;
                }
                _logger.LogError("Concurrency error occurred while updating action item with id {Id}", id);
                throw;
            }
        }

        public async Task<bool> DeleteActionItemAsync(Guid id)
        {
            _logger.LogInformation("Deleting action item with id {Id}", id);
            var actionItem = await _context.ActionItems.FindAsync(id);
            if (actionItem == null)
            {
                _logger.LogWarning("Delete failed: action item with id {Id} not found", id);
                return false;
            }

            _context.ActionItems.Remove(actionItem);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Action item with id {Id} deleted successfully", id);
            return true;
        }

        public async Task<ActionItemDto?> ToggleDoneAsync(Guid id)
        {
            _logger.LogInformation("Toggling done status for action item with id {Id}", id);
            var item = await _context.ActionItems.FindAsync(id);
            if (item == null)
            {
                _logger.LogWarning("Toggle failed: action item with id {Id} not found", id);
                return null;
            }

            _logger.LogInformation("Toggling done status for action item with id {Id}. Previous value: {Prev}", id, item.IsDone);
            item.IsDone = !item.IsDone;
            await _context.SaveChangesAsync();
            _logger.LogInformation("New value for IsDone: {New}", item.IsDone);

            return MapToDto(item);
        }

        private static ActionItemDto MapToDto(ActionItem item)
        {
            return new ActionItemDto
            {
                Id = item.Id,
                Text = item.Text,
                IsDone = item.IsDone,
                DueAt = item.DueAt
            };
        }
    }
}

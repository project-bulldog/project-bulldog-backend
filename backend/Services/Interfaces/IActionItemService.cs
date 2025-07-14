using backend.Dtos.ActionItems;
using backend.Models;

namespace backend.Services.Interfaces;

public interface IActionItemService
{
    Task<IEnumerable<ActionItemDto>> GetActionItemsAsync();
    Task<ActionItemDto?> GetActionItemAsync(Guid id);
    Task<ActionItemDto> CreateActionItemAsync(CreateActionItemDto itemDto);
    Task<bool> UpdateActionItemAsync(Guid id, UpdateActionItemDto itemDto);
    public Task SoftDeleteActionItemAsync(Guid id);
    public Task RestoreActionItemAsync(Guid id);
    Task<ActionItemDto?> ToggleDoneAsync(Guid id);
}

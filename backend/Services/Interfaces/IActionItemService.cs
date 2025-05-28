using backend.Dtos.ActionItems;
using backend.Models;

namespace backend.Services.Interfaces;

public interface IActionItemService
{
    Task<IEnumerable<ActionItemDto>> GetActionItemsAsync();
    Task<ActionItemDto?> GetActionItemAsync(int id);
    Task<ActionItemDto> CreateActionItemAsync(CreateActionItemDto itemDto);
    Task<bool> UpdateActionItemAsync(int id, UpdateActionItemDto itemDto);
    Task<bool> DeleteActionItemAsync(int id);
    Task<ActionItemDto?> ToggleDoneAsync(int id);
}

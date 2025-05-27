using backend.Dtos;
using backend.Models;

namespace backend.Services.Interfaces;

public interface IActionItemService
{
    Task<IEnumerable<ActionItemDto>> GetActionItemsAsync();
    Task<ActionItemDto?> GetActionItemAsync(int id);
    Task<ActionItemDto> CreateActionItemAsync(ActionItemDto itemDto);
    Task<bool> UpdateActionItemAsync(int id, ActionItemDto itemDto);
    Task<bool> DeleteActionItemAsync(int id);
    Task<ActionItemDto?> ToggleDoneAsync(int id);
}

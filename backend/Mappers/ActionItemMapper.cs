using backend.Dtos.ActionItems;
using backend.Models;

namespace backend.Mappers;

public static class ActionItemMapper
{
    public static ActionItemDto ToDto(ActionItem item)
    {
        return new ActionItemDto
        {
            Id = item.Id,
            Text = item.Text,
            IsDone = item.IsDone,
            DueAt = item.DueAt
        };
    }

    public static List<ActionItemDto> ToDtoList(IEnumerable<ActionItem> items)
    {
        return [.. items.Select(ToDto)];
    }
}

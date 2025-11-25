using Kanban.Api.Dtos.Cards;
using Kanban.Api.Models;

namespace Kanban.Api.Extensions;

public static class KanbanMappingExtensions
{
    public static KanbanCardDto ToDto(this KanbanCard card) =>
        new(card.Id, card.Title, card.Description, card.Status, card.Position, card.CreatedAt, card.UpdatedAt);
}

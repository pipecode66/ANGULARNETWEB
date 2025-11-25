namespace Kanban.Api.Dtos.Cards;

public record KanbanCardDto(
    int Id,
    string Title,
    string? Description,
    string Status,
    int Position,
    DateTime CreatedAt,
    DateTime UpdatedAt);

namespace Kanban.Api.Dtos.Cards;

public class UpdateCardRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "todo";
    public int Position { get; set; }
}

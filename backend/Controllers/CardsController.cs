using Kanban.Api.Data;
using Kanban.Api.Dtos.Cards;
using Kanban.Api.Extensions;
using Kanban.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CardsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public CardsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<KanbanCardDto>>> GetAll()
    {
        var cards = await _dbContext.Cards
            .OrderBy(c => c.Status)
            .ThenBy(c => c.Position)
            .ThenBy(c => c.CreatedAt)
            .Select(c => c.ToDto())
            .ToListAsync();

        return Ok(cards);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<KanbanCardDto>> GetById(int id)
    {
        var card = await _dbContext.Cards.FindAsync(id);
        return card is null ? NotFound() : Ok(card.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<KanbanCardDto>> Create(CreateCardRequest request)
    {
        var normalizedStatus = NormalizeStatus(request.Status);
        var nextPosition = await NextPositionAsync(normalizedStatus);

        var card = new KanbanCard
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            Status = normalizedStatus,
            Position = nextPosition,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Cards.Add(card);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = card.Id }, card.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<KanbanCardDto>> Update(int id, UpdateCardRequest request)
    {
        var card = await _dbContext.Cards.FindAsync(id);
        if (card is null)
        {
            return NotFound();
        }

        var normalizedStatus = NormalizeStatus(request.Status);
        var statusChanged = !string.Equals(card.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase);

        card.Title = request.Title.Trim();
        card.Description = request.Description;
        card.Status = normalizedStatus;

        if (statusChanged)
        {
            card.Position = await NextPositionAsync(normalizedStatus);
        }
        else
        {
            card.Position = request.Position;
        }

        card.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return Ok(card.ToDto());
    }

    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder([FromBody] List<ReorderItem> items)
    {
        if (items.Count == 0)
        {
            return BadRequest("No hay elementos para reordenar.");
        }

        var ids = items.Select(i => i.Id).ToList();
        var cards = await _dbContext.Cards.Where(c => ids.Contains(c.Id)).ToListAsync();

        foreach (var group in items.GroupBy(i => NormalizeStatus(i.Status)))
        {
            var orderedGroup = group.OrderBy(i => i.Position).ToList();
            for (var index = 0; index < orderedGroup.Count; index++)
            {
                var item = orderedGroup[index];
                var card = cards.FirstOrDefault(c => c.Id == item.Id);
                if (card is null)
                {
                    continue;
                }

                card.Status = NormalizeStatus(item.Status);
                card.Position = index;
                card.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var card = await _dbContext.Cards.FindAsync(id);
        if (card is null)
        {
            return NotFound();
        }

        _dbContext.Cards.Remove(card);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private static string NormalizeStatus(string status) =>
        string.IsNullOrWhiteSpace(status) ? "todo" : status.Trim().ToLowerInvariant();

    private async Task<int> NextPositionAsync(string status) =>
        await _dbContext.Cards
            .Where(c => c.Status == status)
            .Select(c => c.Position)
            .DefaultIfEmpty(-1)
            .MaxAsync() + 1;
}

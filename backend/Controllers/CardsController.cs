using Kanban.Api.Data;
using Kanban.Api.Dtos.Cards;
using Kanban.Api.Extensions;
using Kanban.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

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
        var userId = GetUserId();

        var cards = await _dbContext.Cards
            .Where(c => c.UserId == userId)
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
        var userId = GetUserId();
        var card = await _dbContext.Cards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        return card is null ? NotFound() : Ok(card.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<KanbanCardDto>> Create(CreateCardRequest request)
    {
        var userId = GetUserId();
        var normalizedStatus = NormalizeStatus(request.Status);
        var nextPosition = await NextPositionAsync(normalizedStatus, userId);

        var card = new KanbanCard
        {
            UserId = userId,
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
        var userId = GetUserId();
        var card = await _dbContext.Cards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
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
            card.Position = await NextPositionAsync(normalizedStatus, userId);
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
        var userId = GetUserId();
        if (items.Count == 0)
        {
            return BadRequest("No hay elementos para reordenar.");
        }

        var ids = items.Select(i => i.Id).ToList();
        var cards = await _dbContext.Cards.Where(c => ids.Contains(c.Id) && c.UserId == userId).ToListAsync();

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
        var userId = GetUserId();
        var card = await _dbContext.Cards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (card is null)
        {
            return NotFound();
        }

        _dbContext.Cards.Remove(card);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private int GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(userId, out var id) ? id : throw new UnauthorizedAccessException("No se pudo obtener el usuario.");
    }

    private static string NormalizeStatus(string status) =>
        string.IsNullOrWhiteSpace(status) ? "todo" : status.Trim().ToLowerInvariant();

    private async Task<int> NextPositionAsync(string status, int userId)
    {
        // EF Core con PostgreSQL no traduce bien DefaultIfEmpty() + MaxAsync(), por eso calculamos en memoria.
        var positions = await _dbContext.Cards
            .Where(c => c.Status == status && c.UserId == userId)
            .Select(c => c.Position)
            .ToListAsync();

        return positions.Count == 0 ? 0 : positions.Max() + 1;
    }
}

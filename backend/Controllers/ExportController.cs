using Kanban.Api.Data;
using Kanban.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Kanban.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly PdfExportService _pdfExportService;
    private readonly ExcelExportService _excelExportService;

    public ExportController(
        AppDbContext dbContext,
        PdfExportService pdfExportService,
        ExcelExportService excelExportService)
    {
        _dbContext = dbContext;
        _pdfExportService = pdfExportService;
        _excelExportService = excelExportService;
    }

    [HttpGet("pdf")]
    public async Task<IActionResult> GetPdf()
    {
        var userId = GetUserId();
        var cards = await _dbContext.Cards
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Status)
            .ThenBy(c => c.Position)
            .ToListAsync();

        var pdfBytes = _pdfExportService.BuildDocument(cards);
        return File(pdfBytes, "application/pdf", "kanban.pdf");
    }

    [HttpGet("excel")]
    public async Task<IActionResult> GetExcel()
    {
        var userId = GetUserId();
        var cards = await _dbContext.Cards
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Status)
            .ThenBy(c => c.Position)
            .ToListAsync();

        var excelBytes = _excelExportService.BuildWorkbook(cards);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "kanban.xlsx");
    }

    private int GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(userId, out var id) ? id : throw new UnauthorizedAccessException("No se pudo obtener el usuario.");
    }
}

using Kanban.Api.Data;
using Kanban.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        var cards = await _dbContext.Cards
            .OrderBy(c => c.Status)
            .ThenBy(c => c.Position)
            .ToListAsync();

        var pdfBytes = _pdfExportService.BuildDocument(cards);
        return File(pdfBytes, "application/pdf", "kanban.pdf");
    }

    [HttpGet("excel")]
    public async Task<IActionResult> GetExcel()
    {
        var cards = await _dbContext.Cards
            .OrderBy(c => c.Status)
            .ThenBy(c => c.Position)
            .ToListAsync();

        var excelBytes = _excelExportService.BuildWorkbook(cards);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "kanban.xlsx");
    }
}

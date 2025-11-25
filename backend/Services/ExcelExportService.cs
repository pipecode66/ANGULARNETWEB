using ClosedXML.Excel;
using Kanban.Api.Models;

namespace Kanban.Api.Services;

public class ExcelExportService
{
    public byte[] BuildWorkbook(IEnumerable<KanbanCard> cards)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Kanban");

        worksheet.Cell(1, 1).Value = "Título";
        worksheet.Cell(1, 2).Value = "Descripción";
        worksheet.Cell(1, 3).Value = "Estado";
        worksheet.Cell(1, 4).Value = "Posición";
        worksheet.Cell(1, 5).Value = "Creado";
        worksheet.Cell(1, 6).Value = "Actualizado";

        var row = 2;
        foreach (var card in cards.OrderBy(c => c.Status).ThenBy(c => c.Position))
        {
            worksheet.Cell(row, 1).Value = card.Title;
            worksheet.Cell(row, 2).Value = card.Description ?? string.Empty;
            worksheet.Cell(row, 3).Value = card.Status;
            worksheet.Cell(row, 4).Value = card.Position;
            worksheet.Cell(row, 5).Value = card.CreatedAt.ToLocalTime();
            worksheet.Cell(row, 6).Value = card.UpdatedAt.ToLocalTime();
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

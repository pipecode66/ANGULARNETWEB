using Kanban.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kanban.Api.Services;

public class PdfExportService
{
    public byte[] BuildDocument(IEnumerable<KanbanCard> cards)
    {
        var ordered = cards
            .OrderBy(c => c.Status)
            .ThenBy(c => c.Position)
            .ThenBy(c => c.CreatedAt)
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header()
                    .Text("Tablero Kanban")
                    .SemiBold()
                    .FontSize(20)
                    .FontColor(Colors.Blue.Medium);

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(30);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                        columns.ConstantColumn(90);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("#").Bold();
                        header.Cell().Text("Título").Bold();
                        header.Cell().Text("Descripción").Bold();
                        header.Cell().Text("Estado").Bold();
                        header.Cell().Text("Actualizado").Bold();
                    });

                    var counter = 1;
                    foreach (var card in ordered)
                    {
                        table.Cell().Text(counter++.ToString());
                        table.Cell().Text(card.Title);
                        table.Cell().Text(card.Description ?? string.Empty);
                        table.Cell().Text(card.Status);
                        table.Cell().Text(card.UpdatedAt.ToLocalTime().ToString("g"));
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .Text(txt =>
                    {
                        txt.Span("Generado ").FontColor(Colors.Grey.Darken1);
                        txt.Span(DateTime.Now.ToString("g")).SemiBold();
                    });
            });
        });

        return document.GeneratePdf();
    }
}

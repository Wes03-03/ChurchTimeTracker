using ChurchTimeTracker.Models;
using ClosedXML.Excel;

namespace ChurchTimeTracker.Services;

public sealed record ExportDocument(string FileName, Stream Content);

public class ExportService
{
    private readonly DatabaseService database;

    public ExportService(DatabaseService databaseService)
    {
        database = databaseService;
    }

    public async Task<ExportDocument> BuildExcel(DateTime from, DateTime to)
    {
        List<SessionRow> rows = await database.Rows(from, to);
        MemoryStream output = new();

        using (XLWorkbook workbook = new())
        {
            IXLWorksheet sessionsSheet = workbook.Worksheets.Add("Category Time");
            string[] headers = ["Category", "Date", "First started", "Day ended", "Total time", "Notes"];

            for (int index = 0; index < headers.Length; index++)
            {
                sessionsSheet.Cell(1, index + 1).Value = headers[index];
            }

            int rowNumber = 2;
            foreach (SessionRow row in rows)
            {
                sessionsSheet.Cell(rowNumber, 1).Value = row.Category;
                sessionsSheet.Cell(rowNumber, 2).Value = row.StartedAt.Date;
                sessionsSheet.Cell(rowNumber, 3).Value = row.StartedAt;
                if (row.EndedAt.HasValue)
                {
                    sessionsSheet.Cell(rowNumber, 4).Value = row.EndedAt.Value;
                }
                else
                {
                    sessionsSheet.Cell(rowNumber, 4).Value = "Active";
                }
                sessionsSheet.Cell(rowNumber, 5).Value = TimeSpan.FromSeconds(row.DurationSeconds);
                sessionsSheet.Cell(rowNumber, 6).Value = row.Comments;
                rowNumber++;
            }

            IXLRange headerRange = sessionsSheet.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#285E61");
            sessionsSheet.SheetView.FreezeRows(1);
            sessionsSheet.Range(1, 1, Math.Max(1, rowNumber - 1), headers.Length).SetAutoFilter();
            sessionsSheet.Column(2).Style.DateFormat.Format = "mmm d, yyyy";
            sessionsSheet.Columns(3, 4).Style.DateFormat.Format = "h:mm AM/PM";
            sessionsSheet.Column(5).Style.NumberFormat.Format = "[h]:mm:ss";
            sessionsSheet.Column(6).Style.Alignment.WrapText = true;
            sessionsSheet.Columns().AdjustToContents();
            sessionsSheet.Column(6).Width = Math.Min(sessionsSheet.Column(6).Width, 55);

            IXLWorksheet summarySheet = workbook.Worksheets.Add("Summary", 1);
            summarySheet.Cell("A1").Value = "Church Time Tracker";
            summarySheet.Cell("A1").Style.Font.Bold = true;
            summarySheet.Cell("A1").Style.Font.FontSize = 20;
            summarySheet.Cell("A3").Value = "Report period";
            summarySheet.Cell("B3").Value = $"{from:d} – {to:d}";
            summarySheet.Cell("A4").Value = "Category entries";
            summarySheet.Cell("B4").Value = rows.Count;
            summarySheet.Cell("A5").Value = "Total time";
            summarySheet.Cell("B5").Value = TimeSpan.FromSeconds(rows.Sum(row => row.DurationSeconds));
            summarySheet.Cell("B5").Style.NumberFormat.Format = "[h]:mm:ss";
            summarySheet.Range("A3:A5").Style.Font.Bold = true;
            summarySheet.Columns().AdjustToContents();

            workbook.SaveAs(output);
        }

        output.Position = 0;
        string fileName = $"ChurchTimeTracker_{from:yyyy-MM-dd}_to_{to:yyyy-MM-dd}.xlsx";
        return new ExportDocument(fileName, output);
    }
}

using ChurchTimeTracker.Models;
using ChurchTimeTracker.Services;
using ClosedXML.Excel;

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

string databasePath = Path.Combine(Path.GetTempPath(), $"ChurchTimeTracker-smoke-{Guid.NewGuid():N}.db");
DatabaseService database = new(databasePath);
TimerService timer = new(database);

await database.AddCategory("Pastoral care");
await database.AddCategory("Administration");
List<Category> categories = await database.Categories();
Category pastoralCare = categories.Single(category => category.Name == "Pastoral care");
Category administration = categories.Single(category => category.Name == "Administration");

List<SlotAssignment> slots = await database.Slots();
slots[0].CategoryId = pastoralCare.Id;
slots[1].CategoryId = administration.Id;
await database.SaveSlot(slots[0]);
await database.SaveSlot(slots[1]);

await timer.InitializeAsync();
Expect(timer.Day is not null && timer.IsRunning, "A new workday should start automatically.");

await timer.SwitchToSlot(1);
string pastoralSessionId = timer.Active?.Id ?? throw new InvalidOperationException("Slot 1 did not start its category.");
await Task.Delay(1100);
await timer.Comment("Followed up with a family.");

await timer.SwitchToSlot(2);
Expect(timer.Category?.Id == administration.Id, "Slot 2 did not become active.");
await Task.Delay(1100);

await timer.Pause();
long pausedDaySeconds = (long)timer.DayElapsed.TotalSeconds;
long pausedCategorySeconds = (long)timer.CategoryElapsed.TotalSeconds;
await Task.Delay(1100);
Expect((long)timer.DayElapsed.TotalSeconds == pausedDaySeconds, "The universal timer advanced while paused.");
Expect((long)timer.CategoryElapsed.TotalSeconds == pausedCategorySeconds, "The category timer advanced while paused.");

await timer.Resume();
await timer.SwitchToSlot(1);
Expect(timer.Active?.Id == pastoralSessionId, "Switching back should resume the existing category entry.");
await timer.EndDay();
Expect(timer.Day is null && timer.Active is null, "End day did not clear the active timers.");

List<SessionRow> rows = await database.Rows(DateTime.Today, DateTime.Today);
Expect(rows.Count == 2, "The workday should contain one cumulative row per category.");
SessionRow pastoralRow = rows.Single(row => row.Category == "Pastoral care");
Expect(pastoralRow.Comments.Contains("Followed up with a family."), "The category note was not retained.");

ExportService export = new(database);
ExportDocument document = await export.BuildExcel(DateTime.Today, DateTime.Today);
using (document.Content)
using (XLWorkbook workbook = new(document.Content))
{
    IXLWorksheet sheet = workbook.Worksheet("Category Time");
    Expect(sheet.Cell(1, 1).GetString() == "Category", "The export should lead with the category name.");
    Expect(sheet.Cell(1, 6).GetString() == "Notes", "The export is missing its Notes column.");
    Expect(sheet.CellsUsed().Any(cell => cell.GetString().Contains("Followed up with a family.")), "The export is missing category notes.");
}

Console.WriteLine("Logic smoke test passed: switching, pause/resume, end day, notes, and Excel export.");

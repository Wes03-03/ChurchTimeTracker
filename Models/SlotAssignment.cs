using SQLite;

namespace ChurchTimeTracker.Models;

public class SlotAssignment
{
    [PrimaryKey]
    public int SlotNumber { get; set; }
    public int? CategoryId { get; set; }
    public string Shortcut { get; set; } = string.Empty;
}

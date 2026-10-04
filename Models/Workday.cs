using SQLite;

namespace ChurchTimeTracker.Models;

public class Workday
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public DateTime StartedAt { get; set; }
    public DateTime? LastResumedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public long DurationSeconds { get; set; }
    public bool IsPaused { get; set; }
    public int? CurrentCategoryId { get; set; }
}

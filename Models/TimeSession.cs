using SQLite;

namespace ChurchTimeTracker.Models;

public class TimeSession
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string DayId { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? LastResumedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public long DurationSeconds { get; set; }
    public bool IsPaused { get; set; }
}

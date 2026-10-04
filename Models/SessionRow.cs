namespace ChurchTimeTracker.Models;

public class SessionRow
{
    public string SessionId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public long DurationSeconds { get; set; }
    public string Comments { get; set; } = string.Empty;

    public string Date => StartedAt.ToString("ddd, MMM d");
    public string TimeRange => $"{StartedAt:t} – {(EndedAt.HasValue ? EndedAt.Value.ToString("t") : "Active")}";
    public string Duration => DurationFormatter.Format(DurationSeconds);
    public bool HasComments => !string.IsNullOrWhiteSpace(Comments);
}

public static class DurationFormatter
{
    public static string Format(long totalSeconds)
    {
        TimeSpan duration = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        return $"{(long)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }
}

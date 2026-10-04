using SQLite;

namespace ChurchTimeTracker.Models;

public class SessionComment
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Text { get; set; } = string.Empty;
}

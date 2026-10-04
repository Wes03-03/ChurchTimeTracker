using SQLite;

namespace ChurchTimeTracker.Models;

public class Category
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique]
    public string Name { get; set; } = string.Empty;

    public bool IsArchived { get; set; }

    public override string ToString() => Name;
}

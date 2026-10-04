using ChurchTimeTracker.Models;
using SQLite;

namespace ChurchTimeTracker.Services;

public class DatabaseService
{
    private readonly SQLiteAsyncConnection database;
    private readonly SemaphoreSlim initializationGate = new(1, 1);
    private bool isInitialized;

    public string PathName { get; }

    public DatabaseService(string? databasePath = null)
    {
        PathName = databasePath ?? Path.Combine(FileSystem.AppDataDirectory, "ChurchTimeTracker.db");
        database = new SQLiteAsyncConnection(PathName);
    }

    public async Task Init()
    {
        if (isInitialized)
        {
            return;
        }

        await initializationGate.WaitAsync();
        try
        {
            if (isInitialized)
            {
                return;
            }

            await database.CreateTableAsync<Category>();
            await database.CreateTableAsync<Workday>();
            await database.CreateTableAsync<TimeSession>();
            await database.CreateTableAsync<SessionComment>();
            await database.CreateTableAsync<SlotAssignment>();

            List<SlotAssignment> existingSlots = await database.Table<SlotAssignment>().ToListAsync();
            for (int slotNumber = 1; slotNumber <= 10; slotNumber++)
            {
                if (existingSlots.All(slot => slot.SlotNumber != slotNumber))
                {
                    await database.InsertAsync(new SlotAssignment
                    {
                        SlotNumber = slotNumber,
                        Shortcut = $"Ctrl+Alt+{(slotNumber == 10 ? 0 : slotNumber)}"
                    });
                }
            }

            isInitialized = true;
        }
        finally
        {
            initializationGate.Release();
        }
    }

    public async Task<List<Category>> Categories()
    {
        await Init();
        return await database.Table<Category>()
            .Where(category => !category.IsArchived)
            .OrderBy(category => category.Name)
            .ToListAsync();
    }

    public async Task<Category?> CategoryById(int categoryId)
    {
        await Init();
        return await database.Table<Category>()
            .Where(category => category.Id == categoryId)
            .FirstOrDefaultAsync();
    }

    public async Task<int> AddCategory(string categoryName)
    {
        await Init();
        string normalizedName = categoryName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Enter a category name.", nameof(categoryName));
        }

        Category? existing = await database.Table<Category>()
            .Where(category => category.Name == normalizedName)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            if (existing.IsArchived)
            {
                existing.IsArchived = false;
                await database.UpdateAsync(existing);
                return existing.Id;
            }

            throw new InvalidOperationException("That category already exists.");
        }

        return await database.InsertAsync(new Category { Name = normalizedName });
    }

    public async Task Rename(Category category, string newName)
    {
        await Init();
        string normalizedName = newName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Enter a category name.", nameof(newName));
        }

        Category? duplicate = await database.Table<Category>()
            .Where(item => item.Name == normalizedName && item.Id != category.Id)
            .FirstOrDefaultAsync();

        if (duplicate is not null)
        {
            throw new InvalidOperationException("That category name is already in use.");
        }

        category.Name = normalizedName;
        await database.UpdateAsync(category);
    }

    public async Task Archive(Category category)
    {
        await Init();
        category.IsArchived = true;
        await database.UpdateAsync(category);

        List<SlotAssignment> assignedSlots = await Slots();
        foreach (SlotAssignment slot in assignedSlots.Where(slot => slot.CategoryId == category.Id))
        {
            slot.CategoryId = null;
            await database.UpdateAsync(slot);
        }
    }

    public async Task<List<SlotAssignment>> Slots()
    {
        await Init();
        return await database.Table<SlotAssignment>()
            .OrderBy(slot => slot.SlotNumber)
            .ToListAsync();
    }

    public async Task SaveSlot(SlotAssignment slot)
    {
        await Init();
        await database.UpdateAsync(slot);
    }

    public async Task Start(TimeSession session)
    {
        await Init();
        await database.InsertAsync(session);
    }

    public async Task SaveSession(TimeSession session)
    {
        await Init();
        await database.UpdateAsync(session);
    }

    public async Task<Workday?> OpenWorkday()
    {
        await Init();
        return await database.Table<Workday>()
            .Where(day => day.EndedAt == null)
            .OrderByDescending(day => day.StartedAt)
            .FirstOrDefaultAsync();
    }

    public async Task StartWorkday(Workday day)
    {
        await Init();
        await database.InsertAsync(day);
    }

    public async Task SaveWorkday(Workday day)
    {
        await Init();
        await database.UpdateAsync(day);
    }

    public async Task<List<TimeSession>> OpenSessions(string dayId)
    {
        await Init();
        return await database.Table<TimeSession>()
            .Where(session => session.DayId == dayId && session.EndedAt == null)
            .OrderBy(session => session.StartedAt)
            .ToListAsync();
    }

    public async Task<TimeSession?> OpenSession(string dayId, int categoryId)
    {
        await Init();
        return await database.Table<TimeSession>()
            .Where(session => session.DayId == dayId &&
                              session.CategoryId == categoryId &&
                              session.EndedAt == null)
            .FirstOrDefaultAsync();
    }

    public async Task<TimeSession?> ActiveSession()
    {
        await Init();
        return await database.Table<TimeSession>()
            .Where(session => session.EndedAt == null)
            .OrderByDescending(session => session.StartedAt)
            .FirstOrDefaultAsync();
    }

    public async Task AddComment(string sessionId, string commentText)
    {
        await Init();
        string normalizedText = commentText.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            throw new ArgumentException("Enter a comment first.", nameof(commentText));
        }

        await database.InsertAsync(new SessionComment
        {
            SessionId = sessionId,
            CreatedAt = DateTime.Now,
            Text = normalizedText
        });
    }

    public async Task<List<SessionRow>> Rows(DateTime from, DateTime to)
    {
        await Init();
        DateTime rangeStart = from.Date;
        DateTime rangeEndExclusive = to.Date.AddDays(1);

        List<TimeSession> sessions = await database.Table<TimeSession>()
            .Where(session => session.StartedAt >= rangeStart && session.StartedAt < rangeEndExclusive)
            .OrderByDescending(session => session.StartedAt)
            .ToListAsync();

        List<Category> categories = await database.Table<Category>().ToListAsync();
        List<SessionComment> comments = await database.Table<SessionComment>().ToListAsync();
        DateTime now = DateTime.Now;

        return sessions.Select(session => new SessionRow
        {
            SessionId = session.Id,
            Category = categories.FirstOrDefault(category => category.Id == session.CategoryId)?.Name ?? "Archived category",
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            DurationSeconds = CurrentDurationSeconds(session, now),
            Comments = string.Join(Environment.NewLine,
                comments.Where(comment => comment.SessionId == session.Id)
                    .OrderBy(comment => comment.CreatedAt)
                    .Select(comment => $"[{comment.CreatedAt:t}] {comment.Text}"))
        }).ToList();
    }

    private static long CurrentDurationSeconds(TimeSession session, DateTime now)
    {
        long seconds = session.DurationSeconds;
        if (session.EndedAt is null && !session.IsPaused)
        {
            DateTime segmentStart = session.LastResumedAt ?? session.StartedAt;
            seconds += Math.Max(0, (long)(now - segmentStart).TotalSeconds);
        }

        return seconds;
    }
}

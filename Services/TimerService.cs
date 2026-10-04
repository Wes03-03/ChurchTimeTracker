using ChurchTimeTracker.Models;

namespace ChurchTimeTracker.Services;

public class TimerService
{
    private readonly DatabaseService database;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private bool isInitialized;

    public Workday? Day { get; private set; }
    public TimeSession? Active { get; private set; }
    public Category? Category { get; private set; }
    public event Action? Changed;

    public bool HasActiveCategory => Active is not null;
    public bool IsPaused => Day?.IsPaused == true;
    public bool IsRunning => Day is not null && !Day.IsPaused;

    public TimeSpan DayElapsed => TimeSpan.FromSeconds(CurrentDaySeconds());
    public TimeSpan CategoryElapsed => TimeSpan.FromSeconds(CurrentCategorySeconds());

    public TimerService(DatabaseService databaseService)
    {
        database = databaseService;
    }

    public async Task InitializeAsync()
    {
        if (isInitialized)
        {
            return;
        }

        await operationGate.WaitAsync();
        try
        {
            if (isInitialized)
            {
                return;
            }

            Day = await database.OpenWorkday();
            if (Day is null)
            {
                await CreateOrMigrateWorkdayAsync();
            }

            if (Day?.CurrentCategoryId is int categoryId)
            {
                Active = await database.OpenSession(Day.Id, categoryId);
                Category = await database.CategoryById(categoryId);
            }

            await RepairRecoveredStateAsync();
            isInitialized = true;
        }
        finally
        {
            operationGate.Release();
        }

        Changed?.Invoke();
    }

    public async Task SwitchToSlot(int slotNumber)
    {
        List<SlotAssignment> slots = await database.Slots();
        SlotAssignment? slot = slots.FirstOrDefault(item => item.SlotNumber == slotNumber);
        if (slot?.CategoryId is not int categoryId)
        {
            return;
        }

        Category? category = await database.CategoryById(categoryId);
        if (category is not null && !category.IsArchived)
        {
            await Switch(category);
        }
    }

    public async Task Switch(Category category)
    {
        await InitializeAsync();
        await operationGate.WaitAsync();
        try
        {
            if (Day is null)
            {
                throw new InvalidOperationException("Start a new workday by reopening the app.");
            }

            if (Active?.CategoryId == category.Id)
            {
                return;
            }

            DateTime now = DateTime.Now;
            if (Active is not null && !Active.IsPaused)
            {
                AccumulateCategory(Active, now);
                Active.IsPaused = true;
                Active.LastResumedAt = null;
                await database.SaveSession(Active);
            }

            TimeSession? target = await database.OpenSession(Day.Id, category.Id);
            if (target is null)
            {
                target = new TimeSession
                {
                    DayId = Day.Id,
                    CategoryId = category.Id,
                    StartedAt = now,
                    LastResumedAt = Day.IsPaused ? null : now,
                    IsPaused = Day.IsPaused
                };
                await database.Start(target);
            }
            else if (!Day.IsPaused)
            {
                target.IsPaused = false;
                target.LastResumedAt = now;
                await database.SaveSession(target);
            }

            Active = target;
            Category = category;
            Day.CurrentCategoryId = category.Id;
            await database.SaveWorkday(Day);
        }
        finally
        {
            operationGate.Release();
        }

        Changed?.Invoke();
    }

    public async Task Pause()
    {
        await InitializeAsync();
        await operationGate.WaitAsync();
        try
        {
            if (Day is null || Day.IsPaused)
            {
                return;
            }

            DateTime now = DateTime.Now;
            AccumulateDay(Day, now);
            Day.IsPaused = true;
            Day.LastResumedAt = null;
            await database.SaveWorkday(Day);

            if (Active is not null && !Active.IsPaused)
            {
                AccumulateCategory(Active, now);
                Active.IsPaused = true;
                Active.LastResumedAt = null;
                await database.SaveSession(Active);
            }
        }
        finally
        {
            operationGate.Release();
        }

        Changed?.Invoke();
    }

    public async Task Resume()
    {
        await InitializeAsync();
        await operationGate.WaitAsync();
        try
        {
            if (Day is null)
            {
                throw new InvalidOperationException("Start a new workday by reopening the app.");
            }

            if (!Day.IsPaused)
            {
                return;
            }

            DateTime now = DateTime.Now;
            Day.LastResumedAt = now;
            Day.IsPaused = false;
            await database.SaveWorkday(Day);

            if (Active is not null)
            {
                Active.LastResumedAt = now;
                Active.IsPaused = false;
                await database.SaveSession(Active);
            }
        }
        finally
        {
            operationGate.Release();
        }

        Changed?.Invoke();
    }

    public async Task EndDay()
    {
        await InitializeAsync();
        await operationGate.WaitAsync();
        try
        {
            if (Day is null)
            {
                return;
            }

            DateTime now = DateTime.Now;
            if (!Day.IsPaused)
            {
                AccumulateDay(Day, now);
            }

            List<TimeSession> sessions = await database.OpenSessions(Day.Id);
            foreach (TimeSession session in sessions)
            {
                if (!session.IsPaused)
                {
                    AccumulateCategory(session, now);
                }

                session.EndedAt = now;
                session.LastResumedAt = null;
                session.IsPaused = false;
                await database.SaveSession(session);
            }

            Day.EndedAt = now;
            Day.LastResumedAt = null;
            Day.IsPaused = false;
            Day.CurrentCategoryId = null;
            await database.SaveWorkday(Day);

            Day = null;
            Active = null;
            Category = null;
        }
        finally
        {
            operationGate.Release();
        }

        Changed?.Invoke();
    }

    public async Task Comment(string text)
    {
        await InitializeAsync();
        if (Active is null)
        {
            throw new InvalidOperationException("Choose a category before adding a note.");
        }

        await database.AddComment(Active.Id, text);
        Changed?.Invoke();
    }

    private async Task CreateOrMigrateWorkdayAsync()
    {
        TimeSession? legacySession = await database.ActiveSession();
        DateTime now = DateTime.Now;

        if (legacySession is not null && string.IsNullOrEmpty(legacySession.DayId))
        {
            Day = new Workday
            {
                StartedAt = legacySession.StartedAt,
                LastResumedAt = legacySession.IsPaused
                    ? null
                    : legacySession.LastResumedAt ?? legacySession.StartedAt,
                DurationSeconds = legacySession.DurationSeconds,
                IsPaused = legacySession.IsPaused,
                CurrentCategoryId = legacySession.CategoryId
            };
            await database.StartWorkday(Day);

            legacySession.DayId = Day.Id;
            await database.SaveSession(legacySession);
            Active = legacySession;
            Category = await database.CategoryById(legacySession.CategoryId);
            return;
        }

        Day = new Workday
        {
            StartedAt = now,
            LastResumedAt = now,
            IsPaused = false
        };
        await database.StartWorkday(Day);
    }

    private async Task RepairRecoveredStateAsync()
    {
        if (Day is null)
        {
            return;
        }

        bool saveDay = false;
        if (!Day.IsPaused && Day.LastResumedAt is null)
        {
            Day.LastResumedAt = Day.StartedAt;
            saveDay = true;
        }

        if (Active is not null)
        {
            if (Day.IsPaused && !Active.IsPaused)
            {
                Active.IsPaused = true;
                Active.LastResumedAt = null;
                await database.SaveSession(Active);
            }
            else if (!Day.IsPaused && Active.LastResumedAt is null)
            {
                Active.IsPaused = false;
                Active.LastResumedAt = Active.StartedAt;
                await database.SaveSession(Active);
            }
        }

        if (saveDay)
        {
            await database.SaveWorkday(Day);
        }
    }

    private long CurrentDaySeconds()
    {
        if (Day is null)
        {
            return 0;
        }

        long seconds = Day.DurationSeconds;
        if (!Day.IsPaused)
        {
            DateTime segmentStart = Day.LastResumedAt ?? Day.StartedAt;
            seconds += Math.Max(0, (long)(DateTime.Now - segmentStart).TotalSeconds);
        }

        return seconds;
    }

    private long CurrentCategorySeconds()
    {
        if (Active is null)
        {
            return 0;
        }

        long seconds = Active.DurationSeconds;
        if (!Active.IsPaused)
        {
            DateTime segmentStart = Active.LastResumedAt ?? Active.StartedAt;
            seconds += Math.Max(0, (long)(DateTime.Now - segmentStart).TotalSeconds);
        }

        return seconds;
    }

    private static void AccumulateDay(Workday day, DateTime now)
    {
        DateTime segmentStart = day.LastResumedAt ?? day.StartedAt;
        day.DurationSeconds += Math.Max(0, (long)(now - segmentStart).TotalSeconds);
    }

    private static void AccumulateCategory(TimeSession session, DateTime now)
    {
        DateTime segmentStart = session.LastResumedAt ?? session.StartedAt;
        session.DurationSeconds += Math.Max(0, (long)(now - segmentStart).TotalSeconds);
    }
}

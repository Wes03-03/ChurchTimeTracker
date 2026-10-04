using ChurchTimeTracker.Models;
using ChurchTimeTracker.Services;

namespace ChurchTimeTracker.Pages;

public partial class TimerPage : ContentPage
{
    private readonly DatabaseService database;
    private readonly TimerService timer;
    private readonly GlobalShortcutService shortcuts;
    private readonly IDispatcherTimer clockTimer;
    private readonly Dictionary<int, Button> slotButtons = [];

    public TimerPage(
        DatabaseService databaseService,
        TimerService timerService,
        GlobalShortcutService shortcutService)
    {
        InitializeComponent();
        database = databaseService;
        timer = timerService;
        shortcuts = shortcutService;

        clockTimer = Dispatcher.CreateTimer();
        clockTimer.Interval = TimeSpan.FromSeconds(1);
        clockTimer.Tick += (_, _) => RefreshClocks();
        timer.Changed += OnStateChanged;
        shortcuts.Changed += OnStateChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        clockTimer.Start();
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        clockTimer.Stop();
        base.OnDisappearing();
    }

    private void OnStateChanged()
    {
        Dispatcher.Dispatch(async () => await LoadAsync());
    }

    private async Task LoadAsync()
    {
        await timer.InitializeAsync();
        List<Category> categories = await database.Categories();
        List<SlotAssignment> slots = await database.Slots();

        SlotsPanel.Children.Clear();
        slotButtons.Clear();
        int assignedCount = 0;

        foreach (SlotAssignment slot in slots)
        {
            Category? category = categories.FirstOrDefault(item => item.Id == slot.CategoryId);
            if (category is null)
            {
                continue;
            }

            assignedCount++;
            Button button = new()
            {
                Text = $"{slot.SlotNumber:00}   {category.Name}\n{GlobalShortcutService.LabelForSlot(slot.SlotNumber)}",
                CommandParameter = category,
                WidthRequest = 218,
                HeightRequest = 76,
                Margin = new Thickness(0, 0, 10, 10),
                HorizontalOptions = LayoutOptions.Fill,
                FontSize = 14
            };
            button.Clicked += Slot_Clicked;
            slotButtons[category.Id] = button;
            SlotsPanel.Children.Add(button);
        }

        EmptySlotsLabel.IsVisible = assignedCount == 0;
        ShortcutWarningLabel.Text = shortcuts.Warning;
        ShortcutWarningLabel.IsVisible = !string.IsNullOrWhiteSpace(shortcuts.Warning);
        RefreshState();
    }

    private async void Slot_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Category category })
        {
            return;
        }

        try
        {
            await timer.Switch(category);
        }
        catch (Exception exception)
        {
            await DisplayAlert("Could not switch category", exception.Message, "OK");
        }
    }

    private void RefreshState()
    {
        RefreshClocks();

        bool hasDay = timer.Day is not null;
        bool isPaused = timer.IsPaused;
        ActiveLabel.Text = timer.Category?.Name.ToUpperInvariant() ?? "NO CATEGORY SELECTED";
        DayHintLabel.Text = hasDay
            ? isPaused ? "Both timers are paused" : $"Workday started {timer.Day!.StartedAt:g}"
            : "Workday ended";

        StatusLabel.Text = hasDay ? (isPaused ? "PAUSED" : "RUNNING") : "ENDED";
        StatusBadge.BackgroundColor = Color.FromArgb(hasDay ? (isPaused ? "#FFF1DA" : "#DFF1E9") : "#E6EEEC");
        StatusLabel.TextColor = Color.FromArgb(hasDay ? (isPaused ? "#96601E" : "#2F7D5C") : "#64726F");

        PauseButton.IsEnabled = hasDay;
        PauseButton.Text = isPaused ? "Resume both timers" : "Pause both timers";
        EndDayButton.IsEnabled = hasDay;
        CommentRow.IsEnabled = timer.HasActiveCategory;
        CommentRow.Opacity = timer.HasActiveCategory ? 1 : 0.5;

        foreach ((int categoryId, Button button) in slotButtons)
        {
            bool selected = timer.Category?.Id == categoryId;
            button.IsEnabled = hasDay;
            button.BackgroundColor = Color.FromArgb(selected ? "#D88C35" : "#285E61");
        }
    }

    private void RefreshClocks()
    {
        DayClockLabel.Text = DurationFormatter.Format((long)timer.DayElapsed.TotalSeconds);
        CategoryClockLabel.Text = DurationFormatter.Format((long)timer.CategoryElapsed.TotalSeconds);
    }

    private async void PauseResume_Clicked(object? sender, EventArgs e)
    {
        if (timer.IsPaused)
        {
            await timer.Resume();
        }
        else
        {
            await timer.Pause();
        }
    }

    private async void EndDay_Clicked(object? sender, EventArgs e)
    {
        bool confirmed = await DisplayAlert(
            "End this workday?",
            "This finishes the universal timer and all category timers, then closes the app. The next launch starts a fresh workday.",
            "End day",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        await timer.EndDay();
        if (Window is not null)
        {
            Application.Current?.CloseWindow(Window);
        }
    }

    private async void Comment_Clicked(object? sender, EventArgs e)
    {
        try
        {
            await timer.Comment(CommentEntry.Text ?? string.Empty);
            CommentEntry.Text = string.Empty;
        }
        catch (Exception exception)
        {
            await DisplayAlert("Could not add note", exception.Message, "OK");
        }
    }
}

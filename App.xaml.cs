namespace ChurchTimeTracker;

public partial class App : Application
{
    private readonly AppShell appShell;
    private readonly Services.GlobalShortcutService shortcuts;
    private readonly Services.UpdateService updates;

    public App(
        AppShell shell,
        Services.GlobalShortcutService shortcutService,
        Services.UpdateService updateService)
    {
        InitializeComponent();
        appShell = shell;
        shortcuts = shortcutService;
        updates = updateService;
    }

    protected override Window CreateWindow(
        IActivationState? activationState)
    {
        Window window = new(appShell)
        {
            Title = "Church Time Tracker",
            Width = 1180,
            Height = 780,
            MinimumWidth = 760,
            MinimumHeight = 600
        };

        window.Created += async (_, _) =>
        {
            shortcuts.Register(window);
            updates.Initialize();
#if WINDOWS
            await updates.CheckForUpdatesAsync(userInitiated: false);
#endif
        };
        window.Destroying += (_, _) =>
        {
            shortcuts.Unregister();
            updates.Dispose();
        };
        return window;
    }
}

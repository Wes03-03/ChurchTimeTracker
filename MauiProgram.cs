using ChurchTimeTracker.Pages;
using ChurchTimeTracker.Services;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace ChurchTimeTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                events.AddWindows(windows => windows.OnPlatformMessage((_, args) =>
                {
                    if (args.MessageId == GlobalShortcutService.WindowsHotKeyMessage)
                    {
                        IPlatformApplication.Current?.Services
                            .GetService<GlobalShortcutService>()?
                            .HandleWindowsMessage(args.WParam);
                    }
                }));
#endif
            });

        builder.Services.AddSingleton<AppShell>();

        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<TimerService>();
        builder.Services.AddSingleton<GlobalShortcutService>();
        builder.Services.AddSingleton<ExportService>();
        builder.Services.AddSingleton<UpdateService>();
        builder.Services.AddSingleton<IFileSaver>(FileSaver.Default);

        builder.Services.AddSingleton<TimerPage>();
        builder.Services.AddSingleton<SetupPage>();
        builder.Services.AddSingleton<OverviewPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

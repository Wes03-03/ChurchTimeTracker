using System.Reflection;
#if WINDOWS
using Velopack;
using Velopack.Sources;
#elif MACCATALYST
using UpSparkle;
#endif

namespace ChurchTimeTracker.Services;

public sealed class UpdateService : IDisposable
{
    private const string RepositoryMetadataKey = "UpdateRepositoryUrl";
    private const string SparkleFeedMetadataKey = "SUFeedURL";
    private const string SparkleKeyMetadataKey = "SUPublicEDKey";
    private static readonly TimeSpan InitializationTimeout = TimeSpan.FromSeconds(15);

    private readonly Assembly assembly = Assembly.GetExecutingAssembly();
    private readonly object initializationLock = new();
    private bool initialized;
    private Task? initializationTask;
    private string? initializationError;
#if MACCATALYST
    private bool disposed;
    private UpSparkleUpdater? sparkleUpdater;
#endif

    public bool IsConfigured
    {
        get
        {
#if WINDOWS
            return !string.IsNullOrWhiteSpace(Metadata(RepositoryMetadataKey));
#elif MACCATALYST
            return !string.IsNullOrWhiteSpace(Metadata(SparkleFeedMetadataKey)) &&
                   !string.IsNullOrWhiteSpace(Metadata(SparkleKeyMetadataKey));
#else
            return false;
#endif
        }
    }

    public string StatusText => IsConfigured
        ? "Updates are delivered through the app's release feed."
        : "Update checking is enabled in packaged release builds.";

    public Task InitializeAsync()
    {
        if (initialized || !IsConfigured)
        {
            return Task.CompletedTask;
        }

        lock (initializationLock)
        {
            return initializationTask ??= InitializeCoreAsync();
        }
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
#if MACCATALYST
            UpSparkleUpdater updater = new();
            await updater.InitializeAsync(
                assembly,
                Metadata(SparkleFeedMetadataKey),
                Metadata(SparkleKeyMetadataKey)).ConfigureAwait(false);

            lock (initializationLock)
            {
                if (disposed)
                {
                    updater.Dispose();
                    return;
                }

                sparkleUpdater = updater;
            }
#endif
            initialized = true;
            initializationError = null;
        }
        catch (Exception exception)
        {
            initializationError = exception.Message;
        }
    }

    public async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (!IsConfigured)
        {
            if (userInitiated)
            {
                await ShowAlert("Updates unavailable", "This development build is not connected to a release feed.");
            }
            return;
        }

        Task initialization = InitializeAsync();
        Task completed = await Task.WhenAny(initialization, Task.Delay(InitializationTimeout));
        if (completed != initialization)
        {
            if (userInitiated)
            {
                await ShowAlert(
                    "Updater is taking too long",
                    "The macOS update helper did not respond. The timer is still safe to use; download the newest DMG from the GitHub Releases page instead.");
            }
            return;
        }

        await initialization;
        if (!initialized)
        {
            if (userInitiated)
            {
                await ShowAlert(
                    "Updates unavailable",
                    initializationError ?? "The update helper could not be started on this Mac.");
            }
            return;
        }

        try
        {
#if WINDOWS
            UpdateManager manager = new(new GithubSource(Metadata(RepositoryMetadataKey)!, null, false));
            if (!manager.IsInstalled)
            {
                if (userInitiated)
                {
                    await ShowAlert("Portable build", "Automatic updates become available after installing the app with ChurchTimeTracker-Setup.exe.");
                }
                return;
            }

            UpdateInfo? update = await manager.CheckForUpdatesAsync();
            if (update is null)
            {
                if (userInitiated)
                {
                    await ShowAlert("You're up to date", $"Church Time Tracker {manager.CurrentVersion} is the newest version.");
                }
                return;
            }

            bool install = await AskToInstall(update.TargetFullRelease.Version.ToString());
            if (!install)
            {
                return;
            }

            await manager.DownloadUpdatesAsync(update);
            manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
#elif MACCATALYST
            if (sparkleUpdater?.IsInitialized == true)
            {
                await MainThread.InvokeOnMainThreadAsync(sparkleUpdater.CheckUpdateWithUI);
            }
#endif
        }
        catch (Exception exception)
        {
            if (userInitiated)
            {
                await ShowAlert("Could not check for updates", exception.Message);
            }
        }
    }

    private string? Metadata(string key) => assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == key)?.Value;

    private static async Task ShowAlert(string title, string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlert(title, message, "OK");
            }
        });
    }

    private static async Task<bool> AskToInstall(string version)
    {
        bool install = false;
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is not null)
            {
                install = await Shell.Current.DisplayAlert(
                    "Update available",
                    $"Church Time Tracker {version} is ready. Download it and restart now?",
                    "Update and restart",
                    "Later");
            }
        });
        return install;
    }

    public void Dispose()
    {
#if MACCATALYST
        disposed = true;
        sparkleUpdater?.Dispose();
        sparkleUpdater = null;
#endif
        initialized = false;
    }
}

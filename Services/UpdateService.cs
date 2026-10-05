using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
#if WINDOWS
using Velopack;
using Velopack.Sources;
#endif

namespace ChurchTimeTracker.Services;

public sealed class UpdateService : IDisposable
{
    private const string RepositoryMetadataKey = "UpdateRepositoryUrl";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(12);

    private readonly Assembly assembly = Assembly.GetExecutingAssembly();
    private bool disposed;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Metadata(RepositoryMetadataKey));

    public string StatusText
    {
        get
        {
#if MACCATALYST
            return IsConfigured
                ? "Checks GitHub Releases and opens the newest Mac download."
                : "Update checking is enabled in packaged release builds.";
#else
            return IsConfigured
                ? "Updates are delivered through the app's release feed."
                : "Update checking is enabled in packaged release builds.";
#endif
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (disposed)
        {
            return;
        }

        string? repositoryUrl = Metadata(RepositoryMetadataKey);
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            if (userInitiated)
            {
                await ShowAlert("Updates unavailable", "This development build is not connected to a release feed.");
            }
            return;
        }

        try
        {
#if WINDOWS
            UpdateManager manager = new(new GithubSource(repositoryUrl, null, false));
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

            bool install = await AskToInstall(update.TargetFullRelease.Version.ToString(), windows: true);
            if (!install)
            {
                return;
            }

            await manager.DownloadUpdatesAsync(update);
            manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
#elif MACCATALYST
            await CheckMacReleaseAsync(repositoryUrl, userInitiated);
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

#if MACCATALYST
    private async Task CheckMacReleaseAsync(string repositoryUrl, bool userInitiated)
    {
        Uri repository = new(repositoryUrl.TrimEnd('/'));
        string repositoryPath = repository.AbsolutePath.Trim('/');
        Uri apiUrl = new($"https://api.github.com/repos/{repositoryPath}/releases/latest");

        using HttpClient client = new() { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ChurchTimeTracker", CurrentVersion().ToString()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using HttpResponseMessage response = await client.GetAsync(apiUrl);
        response.EnsureSuccessStatusCode();
        await using Stream payload = await response.Content.ReadAsStreamAsync();
        using JsonDocument release = await JsonDocument.ParseAsync(payload);

        string? tag = release.RootElement.GetProperty("tag_name").GetString();
        string? releasePage = release.RootElement.GetProperty("html_url").GetString();
        if (!TryParseVersion(tag, out Version? latestVersion) || string.IsNullOrWhiteSpace(releasePage))
        {
            throw new InvalidOperationException("GitHub returned an invalid release response.");
        }

        if (latestVersion! <= CurrentVersion())
        {
            if (userInitiated)
            {
                await ShowAlert("You're up to date", $"Church Time Tracker {CurrentVersion(threeParts: true)} is the newest version.");
            }
            return;
        }

        bool download = await AskToInstall(latestVersion.ToString(3), windows: false);
        if (download)
        {
            bool opened = await Launcher.Default.OpenAsync(releasePage);
            if (!opened)
            {
                await ShowAlert("Could not open download", releasePage);
            }
        }
    }

    private static bool TryParseVersion(string? tag, out Version? version) =>
        Version.TryParse(tag?.Trim().TrimStart('v', 'V'), out version);
#endif

    private Version CurrentVersion(bool threeParts = false)
    {
        Version version = assembly.GetName().Version ?? new Version(0, 0, 0);
        return threeParts ? new Version(version.Major, version.Minor, Math.Max(version.Build, 0)) : version;
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

    private static async Task<bool> AskToInstall(string version, bool windows)
    {
        bool install = false;
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is not null)
            {
                string message = windows
                    ? $"Church Time Tracker {version} is ready. Download it and restart now?"
                    : $"Church Time Tracker {version} is ready. Open the GitHub download page? After downloading, drag the new app to Applications and choose Replace.";
                install = await Shell.Current.DisplayAlert(
                    "Update available",
                    message,
                    windows ? "Update and restart" : "Open download",
                    "Later");
            }
        });
        return install;
    }

    public void Dispose() => disposed = true;
}

using ChurchTimeTracker.Models;
using ChurchTimeTracker.Services;
using CommunityToolkit.Maui.Storage;

namespace ChurchTimeTracker.Pages;

public partial class OverviewPage : ContentPage
{
    private readonly DatabaseService database;
    private readonly ExportService exportService;
    private readonly IFileSaver fileSaver;
    private bool isAdjustingDates;
    private bool isBusy;
    private bool isExporting;
    private bool reloadRequested;
    private List<SessionRow> currentRows = [];

    public OverviewPage(DatabaseService databaseService, ExportService export, IFileSaver saver)
    {
        InitializeComponent();
        database = databaseService;
        exportService = export;
        fileSaver = saver;

        DateTime today = DateTime.Today;
        isAdjustingDates = true;
        FromDate.MaximumDate = today;
        ToDate.MaximumDate = today;
        FromDate.Date = today.AddDays(-6);
        ToDate.Date = today;
        isAdjustingDates = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadRowsAsync();
    }

    private async Task LoadRowsAsync()
    {
        if (isBusy)
        {
            reloadRequested = true;
            return;
        }

        isBusy = true;
        SetBusy(true);
        try
        {
            currentRows = await database.Rows(FromDate.Date, ToDate.Date);
            Rows.ItemsSource = currentRows;
            SessionCountLabel.Text = currentRows.Count.ToString();
            TotalTimeLabel.Text = DurationFormatter.Format(currentRows.Sum(row => row.DurationSeconds));
            DateRangeLabel.Text = $"{FromDate.Date:MMM d, yyyy} – {ToDate.Date:MMM d, yyyy}";
            EmptyState.IsVisible = currentRows.Count == 0;
            ExportButton.IsEnabled = currentRows.Count > 0;
        }
        catch (Exception exception)
        {
            await DisplayAlert("Could not load report", exception.Message, "OK");
        }
        finally
        {
            isBusy = false;
            SetBusy(false);

            if (reloadRequested)
            {
                reloadRequested = false;
                await LoadRowsAsync();
            }
        }
    }

    private void SetBusy(bool busy)
    {
        LoadingIndicator.IsVisible = busy;
        LoadingIndicator.IsRunning = busy;
        Rows.Opacity = busy ? 0.45 : 1;
    }

    private async void Date_Selected(object? sender, DateChangedEventArgs e)
    {
        if (isAdjustingDates)
        {
            return;
        }

        isAdjustingDates = true;
        if (FromDate.Date > ToDate.Date)
        {
            if (ReferenceEquals(sender, FromDate))
            {
                ToDate.Date = FromDate.Date;
            }
            else
            {
                FromDate.Date = ToDate.Date;
            }
        }

        FromDate.MaximumDate = ToDate.Date;
        ToDate.MinimumDate = FromDate.Date;
        isAdjustingDates = false;
        await LoadRowsAsync();
    }

    private async Task SetRangeAsync(DateTime from, DateTime to)
    {
        isAdjustingDates = true;
        FromDate.MaximumDate = DateTime.Today;
        ToDate.MinimumDate = DateTime.Today.AddYears(-20);
        FromDate.Date = from.Date;
        ToDate.Date = to.Date;
        FromDate.MaximumDate = ToDate.Date;
        ToDate.MinimumDate = FromDate.Date;
        isAdjustingDates = false;
        await LoadRowsAsync();
    }

    private async void LastSeven_Clicked(object? sender, EventArgs e) =>
        await SetRangeAsync(DateTime.Today.AddDays(-6), DateTime.Today);

    private async void LastThirty_Clicked(object? sender, EventArgs e) =>
        await SetRangeAsync(DateTime.Today.AddDays(-29), DateTime.Today);

    private async void ThisMonth_Clicked(object? sender, EventArgs e) =>
        await SetRangeAsync(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today);

    private async void Export_Clicked(object? sender, EventArgs e)
    {
        if (currentRows.Count == 0 || isExporting)
        {
            return;
        }

        isExporting = true;
        ExportButton.IsEnabled = false;
        ExportButton.Text = "Preparing…";

        try
        {
            ExportDocument document = await exportService.BuildExcel(FromDate.Date, ToDate.Date);
            using Stream content = document.Content;
            FileSaverResult result = await fileSaver.SaveAsync(document.FileName, content, CancellationToken.None);

            if (result.IsSuccessful)
            {
                await DisplayAlert("Export saved", $"Your Excel report was saved to:\n{result.FilePath}", "OK");
            }
            else if (result.Exception is not OperationCanceledException)
            {
                throw result.Exception ?? new IOException("The file could not be saved.");
            }
        }
        catch (OperationCanceledException)
        {
            // Closing the save dialog is a normal cancellation.
        }
        catch (Exception exception)
        {
            await DisplayAlert("Could not save export", exception.Message, "OK");
        }
        finally
        {
            isExporting = false;
            ExportButton.Text = "Save Excel…";
            ExportButton.IsEnabled = currentRows.Count > 0;
        }
    }
}

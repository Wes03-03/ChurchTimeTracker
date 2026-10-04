using ChurchTimeTracker.Models;
using ChurchTimeTracker.Services;
using Microsoft.Maui.Controls.Shapes;

namespace ChurchTimeTracker.Pages;

public partial class SetupPage : ContentPage
{
    private readonly DatabaseService database;
    private readonly UpdateService updates;
    private List<Category> categories = [];

    public SetupPage(DatabaseService databaseService, UpdateService updateService)
    {
        InitializeComponent();
        database = databaseService;
        updates = updateService;
        UpdateStatusLabel.Text = updates.StatusText;
    }

    private async void CheckUpdates_Clicked(object? sender, EventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        try
        {
            await updates.CheckForUpdatesAsync(userInitiated: true);
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        categories = await database.Categories();
        List<SlotAssignment> slots = await database.Slots();
        BuildCategoryRows();
        BuildSlotRows(slots);
    }

    private void BuildCategoryRows()
    {
        CategoryPanel.Children.Clear();
        NoCategoriesLabel.IsVisible = categories.Count == 0;

        foreach (Category category in categories)
        {
            Grid row = new()
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 4,
                Padding = new Thickness(0, 4)
            };

            row.Add(new Label
            {
                Text = category.Name,
                FontFamily = "OpenSansSemibold",
                FontSize = 15,
                VerticalTextAlignment = TextAlignment.Center
            });

            Button renameButton = new()
            {
                Text = "Rename",
                CommandParameter = category,
                Style = (Style)Application.Current!.Resources["QuietButton"]
            };
            renameButton.Clicked += Rename_Clicked;
            row.Add(renameButton, 1);

            Button archiveButton = new()
            {
                Text = "Archive",
                CommandParameter = category,
                Style = (Style)Application.Current!.Resources["QuietButton"],
                TextColor = Color.FromArgb("#B34A4A")
            };
            archiveButton.Clicked += Archive_Clicked;
            row.Add(archiveButton, 2);
            CategoryPanel.Add(row);
        }
    }

    private void BuildSlotRows(IEnumerable<SlotAssignment> slots)
    {
        SlotPanel.Children.Clear();

        foreach (SlotAssignment slot in slots)
        {
            Grid row = new()
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(48)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(115))
                },
                ColumnSpacing = 10
            };

            Border numberBadge = new()
            {
                BackgroundColor = Color.FromArgb("#E3F0EF"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(8, 5),
                Content = new Label
                {
                    Text = slot.SlotNumber.ToString("00"),
                    TextColor = Color.FromArgb("#285E61"),
                    FontFamily = "OpenSansSemibold",
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                }
            };
            row.Add(numberBadge);

            Picker picker = new()
            {
                Title = "Unassigned",
                ItemsSource = categories,
                ItemDisplayBinding = new Binding(nameof(Category.Name)),
                SelectedItem = categories.FirstOrDefault(category => category.Id == slot.CategoryId)
            };
            picker.SelectedIndexChanged += async (_, _) =>
            {
                slot.CategoryId = (picker.SelectedItem as Category)?.Id;
                await database.SaveSlot(slot);
            };

            Border pickerBorder = new()
            {
                Stroke = new SolidColorBrush(Color.FromArgb("#DCE5E2")),
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(10, 0),
                Content = picker
            };
            row.Add(pickerBorder, 1);

            row.Add(new Label
            {
                Text = GlobalShortcutService.LabelForSlot(slot.SlotNumber),
                FontSize = 12,
                FontFamily = "OpenSansSemibold",
                TextColor = Color.FromArgb("#64726F"),
                HorizontalTextAlignment = TextAlignment.End,
                VerticalTextAlignment = TextAlignment.Center
            }, 2);
            SlotPanel.Add(row);
        }
    }

    private async void Add_Clicked(object? sender, EventArgs e)
    {
        try
        {
            await database.AddCategory(NewName.Text ?? string.Empty);
            NewName.Text = string.Empty;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlert("Could not add category", exception.Message, "OK");
        }
    }

    private async void Rename_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Category category })
        {
            return;
        }

        string? newName = await DisplayPromptAsync("Rename category", "Enter a new name", initialValue: category.Name, maxLength: 80);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        try
        {
            await database.Rename(category, newName);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlert("Could not rename category", exception.Message, "OK");
        }
    }

    private async void Archive_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Category category })
        {
            return;
        }

        bool confirmed = await DisplayAlert("Archive category?", $"{category.Name} will be removed from quick-start slots. Existing history is kept.", "Archive", "Cancel");
        if (!confirmed)
        {
            return;
        }

        await database.Archive(category);
        await LoadAsync();
    }
}

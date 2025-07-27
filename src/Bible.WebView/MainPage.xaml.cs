using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Windows.Input;

namespace Bible.WebView;

public partial class MainPage : ContentPage
{
    public record Book(string code, string name);
    public record Language(string Id, string Name, string? NativeName = null);
    public record Version(string Id, string Abbreviation, string Name);

    public ObservableCollection<Book> Books { get; } = new();
    public ObservableCollection<Language> Languages { get; } = new();
    public ObservableCollection<Version> Versions { get; } = new();

    public ICommand OpenBookCommand { get; }

    // Current selected language and version
    private Language? selectedLanguage;
    private Version? selectedVersion;

    // Base path inside resources (updated dynamically)
    private string version = "eng-WEBBE";
    private string name = string.Empty;

    // Data from JSON
    private JsonDocument? dataJson;

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;

        OpenBookCommand = new Command<string>(OnOpenBook);

        _ = LoadDataJsonAsync();
        _ = LoadBooksJsonAsync();
    }

    private void OnShowControlsClicked(object sender, EventArgs e)
    {
        ControlsContainer.IsVisible = true;
    }

    private void OnOpenBook(string code)
    {
        if (string.IsNullOrEmpty(code)) return;
        ControlsContainer.IsVisible = false;
        var path = $"{version}/{code}.html";
        BibleWebView.Source = path;
    }

    private async Task LoadDataJsonAsync()
    {
        var json = await ReadAsync("data.json");
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            dataJson = JsonDocument.Parse(json);

            // Populate languages picker
            var languagesElement = dataJson.RootElement.GetProperty("languages");
            Languages.Clear();

            foreach (var langEl in languagesElement.EnumerateArray())
            {
                var id = langEl.GetProperty("id").GetString() ?? "";
                var name = langEl.GetProperty("name").GetString() ?? "";
                var nativeName = langEl.TryGetProperty("nativeName", out var nativeNameProp) ? nativeNameProp.GetString() : null;

                Languages.Add(new Language(id, name, nativeName));
            }

            LanguagePicker.ItemsSource = Languages.Select(l =>
                string.IsNullOrEmpty(l.NativeName) ? l.Name : $"{l.NativeName}: {l.Name}")
                .ToList();

            // Select default language (English)
            int defaultIndex = Languages.IndexOf(Languages.First(l => l.Id == "eng"));
            if (defaultIndex >= 0)
            {
                LanguagePicker.SelectedIndex = defaultIndex;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load data.json:\n{ex.Message}", "OK");
        }
    }

    private void OnLanguageSelected(object sender, EventArgs e)
    {
        if (LanguagePicker.SelectedIndex < 0 || dataJson == null)
            return;

        selectedLanguage = Languages[LanguagePicker.SelectedIndex];

        // Populate versions for this language
        Versions.Clear();

        var metadataElement = dataJson.RootElement.GetProperty("metadata");

        var versionsElement = dataJson.RootElement.GetProperty("versions");
        if (versionsElement.TryGetProperty(selectedLanguage.Id, out var versionArray))
        {
            foreach (var v in versionArray.EnumerateArray())
            {
                var id = v.GetProperty("id").GetString() ?? "";
                var abbreviation = v.GetProperty("abbreviation").GetString() ?? "";
                if (metadataElement.TryGetProperty(id, out var meta))
                {
                    name = meta.GetProperty("name").GetString() ?? name;
                }
                Versions.Add(new Version(id, abbreviation, name));
            }
        }

        VersionPicker.ItemsSource = Versions.Select(v =>
            $"{v.Abbreviation}: {v.Name}")
            .ToList();

        // Select first version by default if available
        if (Versions.Count > 0)
        {
            VersionPicker.SelectedIndex = -1;
        }
    }

    private void OnVersionSelected(object sender, EventArgs e)
    {
        if (VersionPicker.SelectedIndex < 0 || selectedLanguage == null || dataJson == null)
            return;

        selectedVersion = Versions[VersionPicker.SelectedIndex];

        // Update basePath using metadata
        var metadataElement = dataJson.RootElement.GetProperty("metadata");
        if (metadataElement.TryGetProperty(selectedVersion.Id, out var meta))
        {
            version = meta.GetProperty("path").GetString() ?? version;
        }
    }

    private async Task LoadBooksJsonAsync()
    {
        var books = await LoadAsync<Book[]>("books.json");
        if (books == null) return;

        // Populate FlexLayout with buttons
        foreach (var book in books)
        {
            var button = new Button
            {
                Text = book.code,
                Margin = 2,
                CornerRadius = 4,
                FontAttributes = FontAttributes.Bold,
                Command = OpenBookCommand,
                CommandParameter = book.code,
                WidthRequest = 80,
                HeightRequest = 40,
            };
            BooksFlexLayout.Children.Add(button);
        }
    }

    private async Task<string?> ReadAsync(string path)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(path);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var json = await reader.ReadToEndAsync();
            return json;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load \"{path}\":\n\n{ex.Message}", "OK");
            return null;
        }
    }

    private async Task<T?> LoadAsync<T>(string path)
    {
        var json = await ReadAsync(path);
        if (string.IsNullOrEmpty(json)) return default;
        try
        {
            var result = JsonSerializer.Deserialize<T>(json);
            return result;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load \"{path}\":\n\n{ex.Message}", "OK");
            return default;
        }
    }
}
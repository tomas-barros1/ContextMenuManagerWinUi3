using System.Reflection;
using Microsoft.Windows.Globalization;
using ContextMenuManager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ContextMenuManager.Views;

public sealed partial class SettingsPage : Page
{
    private sealed record LanguageOption(string Tag, string Label)
    {
        public override string ToString() => Label;
    }

    public SettingsPage()
    {
        InitializeComponent();
        VersionText.Text = Loc.Format("VersionFormat",
            (typeof(App).Assembly.GetName().Version ?? new Version(1, 0)).ToString());
        LoadLanguages();
    }

    private void LoadLanguages()
    {
        string? current = AppConfig.Get(App.LanguageOverrideKey);

        List<LanguageOption> languages = new()
        {
            new(string.Empty, Loc.Get("LanguageSystem")),
            new("en-US", "English"),
            new("pt-BR", "Português (Brasil)"),
        };

        LanguageCombo.ItemsSource = languages;
        LanguageCombo.SelectedItem = languages.FirstOrDefault(l => l.Tag == current) ?? languages[0];
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageCombo.SelectedItem is not LanguageOption option) return;

        AppConfig.Set(App.LanguageOverrideKey, string.IsNullOrEmpty(option.Tag) ? null : option.Tag);
        ApplicationLanguages.PrimaryLanguageOverride = option.Tag;

        RestartNote.Visibility = Visibility.Visible;
    }
}

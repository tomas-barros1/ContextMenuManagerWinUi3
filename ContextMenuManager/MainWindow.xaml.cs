using ContextMenuManager.Services;
using ContextMenuManager.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ContextMenuManager;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = Loc.Get("AppTitle");
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is not NavigationViewItem { Tag: string tag }) return;

        if (tag == "Settings")
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (Enum.TryParse<MenuScene>(tag, out MenuScene scene))
        {
            ContentFrame.Navigate(typeof(ShellItemsPage), scene);
        }
    }
}

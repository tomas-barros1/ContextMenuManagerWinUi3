using System.Collections.ObjectModel;
using Windows.Graphics.Imaging;
using ContextMenuManager.Models;
using ContextMenuManager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ContextMenuManager.Views;

public sealed partial class ShellItemsPage : Page
{
    private MenuScene _scene;
    private List<ShellEntry> _allItems = new();
    private readonly ObservableCollection<ShellEntry> _items = new();
    private CancellationTokenSource? _loadCts;
    private bool _suppressToggle;

    public ShellItemsPage()
    {
        InitializeComponent();
        ItemsList.ItemsSource = _items;
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        _scene = (MenuScene)e.Parameter;
        LoadItems();
    }

    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        _loadCts?.Cancel();
    }

    private async void LoadItems()
    {
        _loadCts?.Cancel();
        CancellationTokenSource cts = new();
        _loadCts = cts;

        LoadingRing.IsActive = true;
        ItemsList.IsEnabled = false;

        try
        {
            MenuScene scene = _scene;
            List<ShellEntry> loaded = await Task.Run(() => ShellItemLoader.LoadScene(scene));
            if (cts.IsCancellationRequested) return;

            _allItems = loaded;
            ApplyFilter();

            ItemsList.IsEnabled = true;

            foreach (ShellEntry item in _allItems)
            {
                if (cts.IsCancellationRequested) return;
                if (item.Icon == null) continue;
                SoftwareBitmapSource source = new();
                await source.SetBitmapAsync(item.Icon);
                item.IconSource = source;
            }
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                LoadingRing.IsActive = false;
            }
        }
    }

    private void ApplyFilter()
    {
        string query = SearchBox.Text?.Trim() ?? string.Empty;

        _items.Clear();
        foreach (ShellEntry item in _allItems)
        {
            if (query.Length == 0
                || item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.KeyName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                _items.Add(item);
            }
        }

        CountText.Text = _items.Count == 0
            ? Loc.Get("NoItems")
            : Loc.Format("ItemCount", _items.Count);
    }

    private void OnSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => ApplyFilter();

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        LoadItems();
    }

    private async void OnToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressToggle) return;
        if (sender is not CheckBox checkBox || checkBox.DataContext is not ShellEntry item) return;

        bool target = checkBox.IsChecked == true;
        try
        {
            await Task.Run(() => item.IsVisible = target);
            _suppressToggle = true;
            item.Visible = item.IsVisible;
            checkBox.IsChecked = item.IsVisible;
            _suppressToggle = false;
        }
        catch (Exception ex)
        {
            _suppressToggle = true;
            checkBox.IsChecked = !target;
            _suppressToggle = false;
            await ShowErrorAsync(Loc.Format("OperationFailed", ex.Message));
        }
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ShellEntry item }) return;

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = Loc.Get("DeleteConfirmTitle"),
            Content = Loc.Format("DeleteConfirmMessage", item.Title),
            PrimaryButtonText = Loc.Get("Delete"),
            CloseButtonText = Loc.Get("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            await Task.Run(item.Delete);
            _allItems.Remove(item);
            _items.Remove(item);
            CountText.Text = _items.Count == 0
                ? Loc.Get("NoItems")
                : Loc.Format("ItemCount", _items.Count);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(Loc.Format("OperationFailed", ex.Message));
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = Loc.Get("ErrorTitle"),
            Content = message,
            CloseButtonText = Loc.Get("Cancel"),
        };
        await dialog.ShowAsync();
    }

    // ---- Editing ----

    private static ShellEntry? GetItem(object sender) =>
        sender is FrameworkElement { DataContext: ShellEntry entry } ? entry : null;

    /// <summary>Asks for confirmation when editing the 'open' verb, which Windows relies on.</summary>
    private async Task<bool> ConfirmOpenItemAsync(ShellEntry item)
    {
        if (!item.IsOpenItem) return true;

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = Loc.Get("WarningOpenItemTitle"),
            Content = Loc.Get("WarningOpenItem"),
            PrimaryButtonText = Loc.Get("Yes"),
            CloseButtonText = Loc.Get("Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<string?> ShowInputDialogAsync(string title, string initialText, string? hint = null)
    {
        TextBox input = new()
        {
            Text = initialText,
            Header = hint,
        };

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = input,
            PrimaryButtonText = Loc.Get("OK"),
            CloseButtonText = Loc.Get("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary ? input.Text : null;
    }

    private async Task RunEditAsync(ShellEntry item, Action action, bool refreshIcon = false)
    {
        try
        {
            await Task.Run(action);
            if (refreshIcon) await RefreshItemIconAsync(item);
        }
        catch (Exception ex)
        {
            item.RefreshDisplay();
            await ShowErrorAsync(Loc.Format("OperationFailed", ex.Message));
        }
    }

    private async Task RefreshItemIconAsync(ShellEntry item)
    {
        await Task.Run(item.LoadIcon);
        if (item.Icon == null)
        {
            item.IconSource = null;
        }
        else
        {
            SoftwareBitmapSource source = new();
            await source.SetBitmapAsync(item.Icon);
            item.IconSource = source;
        }
    }

    private async void OnRename(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item) return;

        string? text = await ShowInputDialogAsync(Loc.Get("InputRenameTitle"), item.Title);
        if (text == null) return;
        if (!await ConfirmOpenItemAsync(item)) return;
        await RunEditAsync(item, () => item.Rename(text));
    }

    private async void OnChangeCommand(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item) return;

        string? command = await ShowInputDialogAsync(Loc.Get("InputCommandTitle"), item.ItemCommand);
        if (command == null) return;
        if (!await ConfirmOpenItemAsync(item)) return;
        await RunEditAsync(item, () => item.SetCommand(command), refreshIcon: true);
    }

    private async void OnChangeIcon(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item) return;

        string? location = await ShowInputDialogAsync(
            Loc.Get("InputIconTitle"), item.IconLocation ?? string.Empty, Loc.Get("InputIconHint"));
        if (location == null) return;
        if (!await ConfirmOpenItemAsync(item)) return;
        await RunEditAsync(item, () => item.SetIcon(location), refreshIcon: true);
    }

    private async void OnRemoveIcon(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item) return;
        if (!await ConfirmOpenItemAsync(item)) return;
        await RunEditAsync(item, item.RemoveIcon, refreshIcon: true);
    }

    private async void OnToggleShield(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item
            || sender is not ToggleMenuFlyoutItem toggle) return;
        await RunEditAsync(item, () => item.SetShield(toggle.IsChecked), refreshIcon: true);
    }

    private async void OnSetPositionDefault(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item) return;
        await RunEditAsync(item, () => item.SetPosition(null));
    }

    private async void OnSetPositionTop(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item) return;
        await RunEditAsync(item, () => item.SetPosition("top"));
    }

    private async void OnSetPositionBottom(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item) return;
        await RunEditAsync(item, () => item.SetPosition("bottom"));
    }

    private async void OnToggleOnlyShift(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item
            || sender is not ToggleMenuFlyoutItem toggle) return;
        await RunEditAsync(item, () => item.SetOnlyWithShift(toggle.IsChecked));
    }

    private async void OnToggleOnlyExplorer(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item
            || sender is not ToggleMenuFlyoutItem toggle) return;
        await RunEditAsync(item, () => item.SetOnlyInExplorer(toggle.IsChecked));
    }

    private async void OnToggleNoWorkDir(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item
            || sender is not ToggleMenuFlyoutItem toggle) return;
        await RunEditAsync(item, () => item.SetNoWorkingDirectory(toggle.IsChecked));
    }

    private async void OnToggleNeverDefault(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item
            || sender is not ToggleMenuFlyoutItem toggle) return;
        await RunEditAsync(item, () => item.SetNeverDefault(toggle.IsChecked));
    }
}

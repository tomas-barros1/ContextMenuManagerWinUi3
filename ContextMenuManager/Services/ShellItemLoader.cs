using ContextMenuManager.Models;

namespace ContextMenuManager.Services;

public enum MenuScene
{
    File,
    Folder,
    Directory,
    Background,
    Desktop,
    Drive,
    AllObjects,
    Computer,
    RecycleBin,
    Library,
}

/// <summary>
/// Enumerates context menu entries for a scene. Port of ContextMenuManager.Controls.ShellList (MVP subset).
/// </summary>
public static class ShellItemLoader
{
    private const string ClsidComputer = @"HKEY_CLASSES_ROOT\CLSID\{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    private const string ClsidRecycleBin = @"HKEY_CLASSES_ROOT\CLSID\{645FF040-5081-101B-9F08-00AA002F954E}";

    public static string GetScenePath(MenuScene scene) => scene switch
    {
        MenuScene.File => @"HKEY_CLASSES_ROOT\*",
        MenuScene.Folder => @"HKEY_CLASSES_ROOT\Folder",
        MenuScene.Directory => @"HKEY_CLASSES_ROOT\Directory",
        MenuScene.Background => @"HKEY_CLASSES_ROOT\Directory\Background",
        MenuScene.Desktop => @"HKEY_CLASSES_ROOT\DesktopBackground",
        MenuScene.Drive => @"HKEY_CLASSES_ROOT\Drive",
        MenuScene.AllObjects => @"HKEY_CLASSES_ROOT\AllFilesystemObjects",
        MenuScene.Computer => ClsidComputer,
        MenuScene.RecycleBin => ClsidRecycleBin,
        MenuScene.Library => @"HKEY_CLASSES_ROOT\LibraryFolder",
        _ => throw new ArgumentOutOfRangeException(nameof(scene)),
    };

    private static IEnumerable<string> GetScenePaths(MenuScene scene)
    {
        yield return GetScenePath(scene);
        if (scene == MenuScene.Library)
        {
            yield return @"HKEY_CLASSES_ROOT\LibraryFolder\Background";
            yield return @"HKEY_CLASSES_ROOT\UserLibraryFolder";
        }
    }

    /// <summary>Primary shell registry path of a scene, where new items are created.</summary>
    public static string GetPrimaryShellPath(MenuScene scene) => $@"{GetScenePath(scene)}\shell";

    /// <summary>
    /// Argument suffix appended when a command is built from a picked file,
    /// so the menu entry receives the clicked object (port of the original's BrowseFile logic).
    /// </summary>
    public static string GetArgumentSuffix(MenuScene scene) => scene switch
    {
        MenuScene.File or MenuScene.Folder or MenuScene.AllObjects => " \"%1\"",
        MenuScene.Directory => " \"%V\"",
        _ => string.Empty,
    };

    /// <summary>Creates a new shell context menu entry (port of NewShellDialog.AddNewItem).</summary>
    public static string CreateShellItem(MenuScene scene, string text, string? command, string? icon, bool isMulti)
    {
        string shellPath = GetPrimaryShellPath(scene);
        string keyName = GetNewKeyName(shellPath);
        string regPath = $@"{shellPath}\{keyName}";

        using Microsoft.Win32.RegistryKey shellKey = RegistryEx.GetRegistryKey(shellPath, writable: true, create: true)
            ?? throw new InvalidOperationException($"Cannot open: {shellPath}");
        using Microsoft.Win32.RegistryKey itemKey = shellKey.CreateSubKey(keyName, writable: true);

        itemKey.SetValue("MUIVerb", text);
        if (isMulti)
        {
            itemKey.SetValue("SubCommands", string.Empty);
        }
        else if (!string.IsNullOrEmpty(command))
        {
            itemKey.CreateSubKey("command", writable: true).SetValue(string.Empty, command);
        }
        if (!string.IsNullOrEmpty(icon))
        {
            itemKey.SetValue("Icon", icon);
        }

        return regPath;
    }

    /// <summary>Returns a free key name: "Item", "Item0", "Item1", ...</summary>
    private static string GetNewKeyName(string shellPath)
    {
        if (RegistryEx.GetRegistryKey($@"{shellPath}\Item") == null) return "Item";
        for (int i = 0; ; i++)
        {
            string name = $"Item{i}";
            if (RegistryEx.GetRegistryKey($@"{shellPath}\{name}") == null) return name;
        }
    }

    public static List<ShellEntry> LoadScene(MenuScene scene)
    {
        List<ShellEntry> entries = new();

        foreach (string scenePath in GetScenePaths(scene))
        {
            LoadShellItems($@"{scenePath}\shell", entries);
            LoadShellExItems($@"{scenePath}\ShellEx", entries);
        }

        return entries;
    }

    private static void LoadShellItems(string shellPath, List<ShellEntry> entries)
    {
        using Microsoft.Win32.RegistryKey? shellKey = RegistryEx.GetRegistryKey(shellPath);
        if (shellKey == null) return;

        foreach (string keyName in shellKey.GetSubKeyNames())
        {
            ShellEntry entry = new ShellEntry($@"{shellPath}\{keyName}", isShellEx: false).Initialize();
            entry.LoadIcon();
            entries.Add(entry);
        }
    }

    private static void LoadShellExItems(string shellExPath, List<ShellEntry> entries)
    {
        LoadShellExHandlers($@"{shellExPath}\ContextMenuHandlers", entries);
        LoadShellExHandlers($@"{shellExPath}\-ContextMenuHandlers", entries);
    }

    private static void LoadShellExHandlers(string handlersPath, List<ShellEntry> entries)
    {
        using Microsoft.Win32.RegistryKey? handlersKey = RegistryEx.GetRegistryKey(handlersPath);
        if (handlersKey == null) return;

        foreach (string keyName in handlersKey.GetSubKeyNames())
        {
            string regPath = $@"{handlersPath}\{keyName}";
            if (!GuidEx.TryParseGuid(RegistryEx.GetValue(regPath, "")?.ToString(), out Guid guid)
                && !GuidEx.TryParseGuid(keyName, out guid))
            {
                guid = Guid.Empty;
            }

            ShellEntry entry = new ShellEntry(regPath, isShellEx: true, guid).Initialize();
            entry.LoadIcon();
            entries.Add(entry);
        }
    }
}

internal static class GuidEx
{
    public static bool TryParseGuid(string? value, out Guid guid)
    {
        guid = Guid.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            guid = new Guid(value.Trim().Trim('{', '}'));
            return guid != Guid.Empty;
        }
        catch
        {
            return false;
        }
    }
}

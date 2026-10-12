using System.Diagnostics;
using Windows.Graphics.Imaging;
using ContextMenuManager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ContextMenuManager.Models;

/// <summary>
/// A shell context menu entry loaded from the registry.
/// Port of ContextMenuManager.Controls.ShellItem / ShellExItem (MVP subset).
/// </summary>
public sealed class ShellEntry : ObservableObject
{
    public const string CommandStorePath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell";
    private const string OpenInNewWindowPath = @"HKEY_CLASSES_ROOT\Folder\shell\opennewwindow";

    /// <summary>Special key names whose display names live in windows.storage.dll.</summary>
    private static readonly Dictionary<string, int> DefaultNameIndexes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "open", 8496 }, { "edit", 8516 }, { "print", 8497 }, { "find", 8503 },
        { "play", 8498 }, { "runas", 8505 }, { "explore", 8502 }, { "preview", 8499 },
    };

    private static readonly Version Win10_1703 = new(10, 0, 15063);
    private static readonly Version Vista = new(6, 0);

    private static readonly string[] ClsidRoots =
    {
        @"HKEY_CLASSES_ROOT\CLSID",
        @"HKEY_CLASSES_ROOT\WOW6432Node\CLSID",
    };

    private bool _isVisible;
    private SoftwareBitmapSource? _iconSource;

    public ShellEntry(string regPath, bool isShellEx, Guid guid = default)
    {
        RegPath = regPath;
        IsShellEx = isShellEx;
        Guid = guid;
    }

    public string RegPath { get; private set; }
    public bool IsShellEx { get; }
    public Guid Guid { get; }
    public string KeyName => RegistryEx.GetKeyName(RegPath);
    public string CommandPath => $@"{RegPath}\command";

    private string Command => RegistryEx.GetValue(CommandPath, "")?.ToString() ?? string.Empty;

    public string? ItemFilePath =>
        GetGuidFilePath() ?? ObjectPath.ExtractFilePath(Command);

    public bool IsOpenItem => KeyName.Equals("open", StringComparison.OrdinalIgnoreCase);

    public bool HasSubItems =>
        RegistryEx.GetValue(RegPath, "SubCommands") != null
        || !string.IsNullOrEmpty(RegistryEx.GetValue(RegPath, "ExtendedSubCommandsKey")?.ToString());

    public Visibility HasSubItemsVisibility => HasSubItems ? Visibility.Visible : Visibility.Collapsed;

    public string Title => StripMnemonics(ResolveTitle());

    /// <summary>
    /// Removes menu accelerator markers from display text: "&O" marks the keyboard
    /// shortcut letter shown underlined in Windows menus; "&&" is a literal ampersand.
    /// </summary>
    private static string StripMnemonics(string text) =>
        text.Replace("&&", "\u0000").Replace("&", "").Replace("\u0000", "&");

    public string Tooltip =>
        !string.IsNullOrEmpty(ItemFilePath) ? ItemFilePath
        : IsShellEx ? RegPath
        : !string.IsNullOrEmpty(Command) ? Command
        : RegPath;

    public SoftwareBitmapSource? IconSource
    {
        get => _iconSource;
        set => SetProperty(ref _iconSource, value);
    }

    public SoftwareBitmap? Icon { get; private set; }

    /// <summary>Loads the icon in the background; call before binding to the UI.</summary>
    public void LoadIcon()
    {
        Icon = IsShellEx ? LoadShellExIcon() : LoadShellIcon();
    }

    private SoftwareBitmap? LoadShellIcon()
    {
        string? iconLocation = RegistryEx.GetValue(RegPath, "Icon")?.ToString();
        if (!string.IsNullOrEmpty(iconLocation))
        {
            SoftwareBitmap? bmp = ShellIcons.GetIconFromLocation(iconLocation);
            if (bmp != null) return bmp;
            string? path = iconLocation.Split(',')[0];
            if (Path.GetExtension(path)?.ToLowerInvariant() == ".exe")
            {
                return ShellIcons.GetIconFromFile("imageres.dll", -15);
            }
        }

        if (RegistryEx.GetValue(RegPath, "HasLUAShield") != null)
        {
            return ShellIcons.GetIconFromFile("imageres.dll", -78);
        }

        string? filePath = ItemFilePath;
        if (!string.IsNullOrEmpty(filePath))
        {
            return ShellIcons.GetFileIcon(filePath)
                ?? (Path.HasExtension(filePath) ? ShellIcons.GetExtensionIcon(Path.GetExtension(filePath)) : null)
                ?? ShellIcons.GetIconFromFile("imageres.dll", -2);
        }

        return ShellIcons.GetIconFromFile("imageres.dll", -2);
    }

    private SoftwareBitmap? LoadShellExIcon()
    {
        if (Guid == Guid.Empty) return ShellIcons.GetIconFromFile("imageres.dll", -2);

        string? clsidPath = GetClsidPath();
        if (clsidPath != null)
        {
            string? iconLocation = RegistryEx.GetValue(clsidPath, "DefaultIcon")?.ToString();
            SoftwareBitmap? bmp = ShellIcons.GetIconFromLocation(iconLocation);
            if (bmp != null) return bmp;
        }

        string? filePath = GetGuidFilePath();
        if (!string.IsNullOrEmpty(filePath))
        {
            return ShellIcons.GetFileIcon(filePath)
                ?? ShellIcons.GetIconFromFile("imageres.dll", -2);
        }

        return ShellIcons.GetIconFromFile("imageres.dll", -2);
    }

    private string ResolveTitle()
    {
        if (IsShellEx) return ResolveShellExTitle();

        // Menu name priority: MUIVerb > default value (single items only) > special key name > key name
        List<string> valueNames = new() { "MUIVerb" };
        if (!HasSubItems) valueNames.Add(string.Empty);

        foreach (string valueName in valueNames)
        {
            string? name = RegistryEx.GetValue(RegPath, valueName)?.ToString();
            name = IndirectString.GetDirectString(name);
            if (!string.IsNullOrEmpty(name)) return name;
        }

        if (DefaultNameIndexes.TryGetValue(KeyName.ToLowerInvariant(), out int index))
        {
            string name = IndirectString.GetDirectString($"@windows.storage.dll,-{index}");
            if (!string.IsNullOrEmpty(name)) return name;
        }

        return KeyName;
    }

    private string ResolveShellExTitle()
    {
        if (Guid != Guid.Empty)
        {
            foreach (string clsidRoot in ClsidRoots)
            {
                foreach (string valueName in new[] { "LocalizedString", "InfoTip", "" })
                {
                    string? text = RegistryEx.GetValue($@"{clsidRoot}\{Guid:B}", valueName)?.ToString();
                    text = IndirectString.GetDirectString(text);
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }

            string? filePath = GetGuidFilePath();
            if (File.Exists(filePath))
            {
                try
                {
                    string? description = FileVersionInfo.GetVersionInfo(filePath!).FileDescription;
                    if (!string.IsNullOrEmpty(description)) return description;
                }
                catch
                {
                    // fall through
                }
                return Path.GetFileName(filePath);
            }
        }

        string? defaultValue = RegistryEx.GetValue(RegPath, "")?.ToString();
        return KeyName.Equals($"{Guid:B}", StringComparison.OrdinalIgnoreCase)
            ? (string.IsNullOrEmpty(defaultValue) ? KeyName : defaultValue)
            : KeyName;
    }

    private string? GetClsidPath()
    {
        if (Guid == Guid.Empty) return null;
        foreach (string clsidRoot in ClsidRoots)
        {
            string path = $@"{clsidRoot}\{Guid:B}";
            if (RegistryEx.GetRegistryKey(path) != null) return path;
        }
        return null;
    }

    private string? GetGuidFilePath()
    {
        if (Guid == Guid.Empty) return null;

        foreach (string clsidRoot in ClsidRoots)
        {
            foreach (string serverKey in new[] { "InprocServer32", "LocalServer32" })
            {
                string? codeBase = RegistryEx.GetValue($@"{clsidRoot}\{Guid:B}\{serverKey}", "CodeBase")?
                    .ToString()?.Replace("file:///", "").Replace('/', '\\');
                if (File.Exists(codeBase)) return codeBase!;

                string? serverPath = RegistryEx.GetValue($@"{clsidRoot}\{Guid:B}\{serverKey}", "")?.ToString();
                serverPath = ObjectPath.ExtractFilePath(serverPath);
                if (File.Exists(serverPath)) return serverPath;
            }
        }

        return null;
    }

    // ---- Visibility (port of ShellItem.ItemVisible / ShellExItem.ItemVisible) ----

    private static Version OsVersion => Environment.OSVersion.Version;

    private string ShellExPath => RegistryEx.GetParentPath(RegistryEx.GetParentPath(RegPath));
    private string ParentKeyName => RegistryEx.GetKeyName(RegistryEx.GetParentPath(RegPath));

    public bool IsVisible
    {
        get
        {
            if (IsShellEx)
            {
                return ParentKeyName.Equals("ContextMenuHandlers", StringComparison.OrdinalIgnoreCase);
            }

            if (OsVersion >= Win10_1703)
            {
                if (Convert.ToInt32(RegistryEx.GetValue(RegPath, "HideBasedOnVelocityId") ?? 0) == 0x639bc8) return false;
            }
            if (RegistryEx.GetValue(RegPath, "LegacyDisable") != null) return false;
            if (RegistryEx.GetValue(RegPath, "ProgrammaticAccessOnly") != null) return false;
            if (OsVersion > Vista && Convert.ToInt32(RegistryEx.GetValue(RegPath, "CommandFlags") ?? 0) % 16 >= 8) return false;
            return true;
        }
        set
        {
            if (IsShellEx)
            {
                SetShellExVisible(value);
            }
            else
            {
                SetShellVisible(value);
            }
        }
    }

    private void SetShellVisible(bool visible)
    {
        if (visible)
        {
            RegistryEx.DeleteValue(RegPath, "HideBasedOnVelocityId");
            RegistryEx.DeleteValue(RegPath, "LegacyDisable");
            RegistryEx.DeleteValue(RegPath, "ProgrammaticAccessOnly");
            if (OsVersion > Vista && Convert.ToInt32(RegistryEx.GetValue(RegPath, "CommandFlags") ?? 0) % 16 >= 8)
            {
                RegistryEx.DeleteValue(RegPath, "CommandFlags");
            }
        }
        else
        {
            if (OsVersion >= Win10_1703)
            {
                RegistryEx.SetValue(RegPath, "HideBasedOnVelocityId", 0x639bc8);
            }

            // LegacyDisable on Folder\shell\opennewwindow breaks taskbar Explorer and Win+E,
            // so only ProgrammaticAccessOnly is used there.
            if (!RegPath.StartsWith(OpenInNewWindowPath, StringComparison.OrdinalIgnoreCase))
            {
                RegistryEx.SetValue(RegPath, "LegacyDisable", string.Empty);
            }
            RegistryEx.SetValue(RegPath, "ProgrammaticAccessOnly", string.Empty);
        }
    }

    private void SetShellExVisible(bool visible)
    {
        string keyName = KeyName;
        string backupPath = visible
            ? $@"{ShellExPath}\-ContextMenuHandlers\{keyName}"
            : $@"{ShellExPath}\ContextMenuHandlers\{keyName}";

        if (backupPath.Equals(RegPath, StringComparison.OrdinalIgnoreCase)) return;

        RegistryEx.MoveTo(RegPath, backupPath);
        RegPath = backupPath;
    }

    public bool InitialVisible { get; private set; }

    public ShellEntry Initialize()
    {
        InitialVisible = IsVisible;
        _isVisible = InitialVisible;
        return this;
    }

    public bool Visible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    // ---- Deletion ----

    public void Delete()
    {
        if (IsShellEx)
        {
            // Remove both the active and the disabled ("-") locations.
            string other = IsVisible
                ? $@"{ShellExPath}\-ContextMenuHandlers\{KeyName}"
                : $@"{ShellExPath}\ContextMenuHandlers\{KeyName}";
            RegistryEx.DeleteKeyTree(other);
        }
        RegistryEx.DeleteKeyTree(RegPath);
    }

    // ---- Editing (port of the original's context menu actions) ----

    /// <summary>Shell items can be edited; ShellEx (COM) handlers cannot.</summary>
    public Visibility EditMenuVisibility => IsShellEx ? Visibility.Collapsed : Visibility.Visible;

    public void RefreshDisplay()
    {
        RaisePropertyChanged(nameof(Title));
        RaisePropertyChanged(nameof(Tooltip));
        RaisePropertyChanged(nameof(HasSubItemsVisibility));
        RaisePropertyChanged(nameof(EditMenuVisibility));
        RaisePropertyChanged(nameof(OnlyWithShift));
        RaisePropertyChanged(nameof(OnlyInExplorer));
        RaisePropertyChanged(nameof(NoWorkingDirectory));
        RaisePropertyChanged(nameof(NeverDefault));
        RaisePropertyChanged(nameof(HasShield));
        RaisePropertyChanged(nameof(IsPositionDefault));
        RaisePropertyChanged(nameof(IsPositionTop));
        RaisePropertyChanged(nameof(IsPositionBottom));
    }

    /// <summary>Renames the item by setting its MUIVerb value (MUIVerb wins over the default value).</summary>
    public void Rename(string? text)
    {
        // Windows hides menu items whose resolved name is 80+ characters long.
        if (!string.IsNullOrEmpty(text) && IndirectString.GetDirectString(text).Length >= 80)
        {
            throw new InvalidOperationException(Loc.Get("ErrorTextTooLong"));
        }

        if (string.IsNullOrEmpty(text))
        {
            RegistryEx.DeleteValue(RegPath, "MUIVerb");
        }
        else
        {
            RegistryEx.SetValue(RegPath, "MUIVerb", text);
        }
        RefreshDisplay();
    }

    public string ItemCommand => RegistryEx.GetValue(CommandPath, "")?.ToString() ?? string.Empty;

    public void SetCommand(string command)
    {
        RegistryEx.SetValue(CommandPath, "", command);
    }

    public string? IconLocation => RegistryEx.GetValue(RegPath, "Icon")?.ToString();

    public void SetIcon(string location)
    {
        RegistryEx.SetValue(RegPath, "Icon", location);
        RefreshDisplay();
    }

    public void RemoveIcon()
    {
        RegistryEx.DeleteValue(RegPath, "Icon");
        RegistryEx.DeleteValue(RegPath, "HasLUAShield");
        RefreshDisplay();
    }

    public bool HasShield => RegistryEx.GetValue(RegPath, "HasLUAShield") != null;

    public void SetShield(bool value)
    {
        if (value)
        {
            RegistryEx.SetValue(RegPath, "HasLUAShield", string.Empty);
        }
        else
        {
            RegistryEx.DeleteValue(RegPath, "HasLUAShield");
        }
        RefreshDisplay();
    }

    private string? PositionValue => RegistryEx.GetValue(RegPath, "Position")?.ToString()?.ToLowerInvariant();

    public bool IsPositionDefault => PositionValue is not ("top" or "bottom");
    public bool IsPositionTop => PositionValue == "top";
    public bool IsPositionBottom => PositionValue == "bottom";

    public void SetPosition(string? position)
    {
        if (position is "top" or "bottom")
        {
            RegistryEx.SetValue(RegPath, "Position", position);
        }
        else
        {
            RegistryEx.DeleteValue(RegPath, "Position");
        }
        RefreshDisplay();
    }

    public bool OnlyWithShift => RegistryEx.GetValue(RegPath, "Extended") != null;
    public bool OnlyInExplorer => RegistryEx.GetValue(RegPath, "OnlyInBrowserWindow") != null;
    public bool NoWorkingDirectory => RegistryEx.GetValue(RegPath, "NoWorkingDirectory") != null;
    public bool NeverDefault => RegistryEx.GetValue(RegPath, "NeverDefault") != null;

    private void SetFlagValue(string valueName, bool value)
    {
        if (value)
        {
            RegistryEx.SetValue(RegPath, valueName, string.Empty);
        }
        else
        {
            RegistryEx.DeleteValue(RegPath, valueName);
        }
        RefreshDisplay();
    }

    public void SetOnlyWithShift(bool value) => SetFlagValue("Extended", value);
    public void SetOnlyInExplorer(bool value) => SetFlagValue("OnlyInBrowserWindow", value);
    public void SetNoWorkingDirectory(bool value) => SetFlagValue("NoWorkingDirectory", value);
    public void SetNeverDefault(bool value) => SetFlagValue("NeverDefault", value);
}

using Microsoft.Win32;

namespace ContextMenuManager.Services;

/// <summary>
/// Registry helpers accepting full registry paths such as "HKEY_CLASSES_ROOT\...\shell\open".
/// Port of BluePointLilac.Methods.RegistryEx.
/// </summary>
public static class RegistryEx
{
    private static void GetRootAndSubPath(string regPath, out RegistryKey root, out string subPath)
    {
        string rootName;
        int index = regPath.IndexOf('\\');
        if (index > 0)
        {
            rootName = regPath[..index].ToUpperInvariant();
            subPath = regPath[(index + 1)..];
        }
        else
        {
            rootName = regPath;
            subPath = string.Empty;
        }

        root = rootName switch
        {
            "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
            "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
            "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
            "HKEY_USERS" or "HKU" => Registry.Users,
            "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
            _ => throw new ArgumentException($"Unknown registry root: {regPath}", nameof(regPath)),
        };
    }

    public static string GetParentPath(string regPath) => regPath[..regPath.LastIndexOf('\\')];

    public static string GetKeyName(string regPath) => regPath[(regPath.LastIndexOf('\\') + 1)..];

    public static RegistryKey? GetRegistryKey(string regPath, bool writable = false, bool create = false)
    {
        GetRootAndSubPath(regPath, out RegistryKey root, out string subPath);
        using (root)
        {
            return create
                ? root.CreateSubKey(subPath, writable)
                : root.OpenSubKey(subPath, writable);
        }
    }

    public static object? GetValue(string regPath, string valueName) => Registry.GetValue(regPath, valueName, null);

    public static void SetValue(string regPath, string valueName, object value) =>
        Registry.SetValue(regPath, valueName, value);

    public static void DeleteValue(string regPath, string valueName)
    {
        using RegistryKey? key = GetRegistryKey(regPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    public static void DeleteKeyTree(string regPath)
    {
        string parentPath = GetParentPath(regPath);
        string keyName = GetKeyName(regPath);
        try
        {
            using RegistryKey? parent = GetRegistryKey(parentPath, writable: true);
            parent?.DeleteSubKeyTree(keyName);
        }
        catch (ArgumentException)
        {
            // Subkey does not exist — nothing to delete.
        }
    }

    public static void CopyTo(string srcPath, string dstPath)
    {
        using RegistryKey src = GetRegistryKey(srcPath) ?? throw new InvalidOperationException($"Key not found: {srcPath}");
        using RegistryKey dst = GetRegistryKey(dstPath, writable: true, create: true)
            ?? throw new InvalidOperationException($"Cannot create key: {dstPath}");

        foreach (string valueName in src.GetValueNames())
        {
            dst.SetValue(valueName,
                src.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) ?? string.Empty,
                src.GetValueKind(valueName));
        }

        foreach (string subKeyName in src.GetSubKeyNames())
        {
            CopyTo($"{srcPath}\\{subKeyName}", $"{dstPath}\\{subKeyName}");
        }
    }

    public static void MoveTo(string srcPath, string dstPath)
    {
        CopyTo(srcPath, dstPath);
        DeleteKeyTree(srcPath);
    }
}

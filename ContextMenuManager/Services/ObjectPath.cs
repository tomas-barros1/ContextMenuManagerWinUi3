using Microsoft.Win32;

namespace ContextMenuManager.Services;

/// <summary>
/// Extracts an existing file path out of a command line.
/// Port of ContextMenuManager.Methods.ObjectPath (ExtractFilePath / GetFullFilePath).
/// </summary>
public static class ObjectPath
{
    private const string RegAppPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
    private const string ShellExecuteCommand = "mshta vbscript:createobject(\"shell.application\").shellexecute(\"";

    private static readonly char[] IllegalChars = { '/', '*', '?', '"', '<', '>', '|' };
    private static readonly string[] IgnoreCommandParts = { "", "%1", "%v" };

    private static readonly Dictionary<string, string?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tries to resolve a bare file name (as usable in Win+R / registry) to a full existing path.</summary>
    public static bool GetFullFilePath(string? fileName, out string? fullPath)
    {
        fullPath = null;
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        foreach (string name in new[] { fileName, $"{fileName}.exe" })
        {
            foreach (string dir in new[] { "", @"%SystemRoot%\System32\", @"%SystemRoot%\" })
            {
                if (dir.Length > 0 && (name.Contains('\\') || name.Contains(':'))) return false;
                string candidate = Environment.ExpandEnvironmentVariables($@"{dir}{name}");
                if (File.Exists(candidate))
                {
                    fullPath = candidate;
                    return true;
                }
            }

            string? appPath = Registry.GetValue($@"{RegAppPath}\{name}", "", null)?.ToString();
            if (File.Exists(appPath))
            {
                fullPath = appPath;
                return true;
            }
        }

        return false;
    }

    /// <summary>Extracts an existing file path from a command string; returns null on failure.</summary>
    public static string? ExtractFilePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        if (Cache.TryGetValue(command, out string? cached)) return cached;

        string? filePath = null;
        string partCmd = Environment.ExpandEnvironmentVariables(command).Replace(@"\\", @"\");

        if (partCmd.StartsWith(ShellExecuteCommand, StringComparison.OrdinalIgnoreCase))
        {
            partCmd = partCmd[ShellExecuteCommand.Length..];
            string[] arr = partCmd.Split("\",\"", StringSplitOptions.None);
            if (arr.Length > 0)
            {
                if (GetFullFilePath(arr[0], out filePath))
                {
                    Cache[command] = filePath;
                    return filePath;
                }
                if (arr.Length > 1)
                {
                    filePath = ExtractFilePath(arr[1]);
                    if (filePath != null) return filePath;
                }
            }
        }

        string[] strs = Array.FindAll(partCmd.Split(IllegalChars),
            str => !IgnoreCommandParts.Any(part => part.Equals(str.Trim()))).Reverse().ToArray();

        foreach (string str1 in strs)
        {
            string str2 = str1;
            int index = -1;
            do
            {
                List<string> paths = new();
                string path1 = str2[(index + 1)..];
                paths.Add(path1);
                if (index > 0) paths.Add(str2[..index]);

                int count = paths.Count;
                for (int i = 0; i < count; i++)
                {
                    foreach (char c in new[] { ',', '-' })
                    {
                        if (paths[i].Contains(c)) paths.AddRange(paths[i].Split(c));
                    }
                }

                foreach (string path in paths)
                {
                    if (GetFullFilePath(path, out filePath))
                    {
                        Cache[command] = filePath;
                        return filePath;
                    }
                }

                str2 = path1;
                index = str2.IndexOf(' ');
            }
            while (index != -1);
        }

        Cache[command] = null;
        return null;
    }
}

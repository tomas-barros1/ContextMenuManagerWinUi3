using System.Globalization;

namespace ContextMenuManager.Services;

/// <summary>
/// Resolves localized strings from the app's PRI resources.
/// Default language is en-US; pt-BR is also provided. Falls back to English automatically.
/// </summary>
public static class Loc
{
    private static readonly Microsoft.Windows.ApplicationModel.Resources.ResourceManager Manager = new();

    public static string Get(string name)
    {
        try
        {
            string? value = Manager.MainResourceMap.TryGetValue($"Resources/{name}")?.ValueAsString;
            return string.IsNullOrEmpty(value) ? name : value;
        }
        catch
        {
            return name;
        }
    }

    public static string Format(string name, params object[] args) =>
        string.Format(CultureInfo.CurrentUICulture, Get(name), args);
}

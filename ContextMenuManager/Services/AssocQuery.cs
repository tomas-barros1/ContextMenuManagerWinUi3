using System.Runtime.InteropServices;
using System.Text;

namespace ContextMenuManager.Services;

/// <summary>
/// Queries the shell association database (port of the FileExtension/AssocStr lookup used by the original).
/// </summary>
public static class AssocQuery
{
    private const uint AssocStrExecutable = 1;

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint AssocQueryString(uint flags, uint str, string pszAssoc, string? pszExtra,
        [Out] StringBuilder? pszOut, ref uint pcchOut);

    /// <summary>Returns the full path of the executable associated with an extension (e.g. ".jpg"), or null.</summary>
    public static string? GetExecutableForExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return null;
        uint size = 0;
        AssocQueryString(0, AssocStrExecutable, extension, null, null, ref size);
        if (size == 0) return null;

        StringBuilder buffer = new((int)size);
        return AssocQueryString(0, AssocStrExecutable, extension, null, buffer, ref size) == 0
            ? buffer.ToString()
            : null;
    }
}

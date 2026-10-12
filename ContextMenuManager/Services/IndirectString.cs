using System.Runtime.InteropServices;
using System.Text;

namespace ContextMenuManager.Services;

/// <summary>
/// Resolves indirect resource strings ("@shell32.dll,-123", "@{package?ms-resource://...}").
/// Port of BluePointLilac.Methods.ResourceString.
/// </summary>
public static class IndirectString
{
    [DllImport("shlwapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode,
        ExactSpelling = true, SetLastError = false, ThrowOnUnmappableChar = true)]
    private static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, uint cchOutBuf, IntPtr ppvReserved);

    public static string GetDirectString(string? resStr)
    {
        if (string.IsNullOrEmpty(resStr)) return string.Empty;
        if (resStr[0] != '@') return resStr;

        StringBuilder buffer = new(1024);
        int hr = SHLoadIndirectString(resStr, buffer, 1024, IntPtr.Zero);
        return hr == 0 && buffer.Length > 0 ? buffer.ToString() : string.Empty;
    }
}

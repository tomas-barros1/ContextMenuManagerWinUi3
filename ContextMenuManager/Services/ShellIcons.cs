using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;

namespace ContextMenuManager.Services;

/// <summary>
/// Extracts shell icons (from files, icon locations like "imageres.dll,-78", or by file type)
/// and converts them to SoftwareBitmap for display in WinUI.
/// Port of BluePointLilac.Methods.ResourceIcon.
/// </summary>
public static class ShellIcons
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int ExtractIconEx(string lpFileName, int nIconIndex, IntPtr[]? phIconLarge, IntPtr[]? phIconSmall, uint nIcons);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr LoadLibrary(string lpLibFileName);

    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr hLibModule);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hInst, string lpFileName, uint uType, int cx, int cy, uint fuLoad);

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

    /// <summary>Gets the associated icon for a file type/extension (works even for nonexistent paths).</summary>
    public static SoftwareBitmap? GetExtensionIcon(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return null;
        return GetShellFileInfoIcon(extension, SHGFI_USEFILEATTRIBUTES);
    }

    /// <summary>Gets the default icon of an existing file or folder.</summary>
    public static SoftwareBitmap? GetFileIcon(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        return GetShellFileInfoIcon(filePath, 0);
    }

    private static SoftwareBitmap? GetShellFileInfoIcon(string path, uint extraFlags)
    {
        SHFILEINFO info = new();
        IntPtr result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON | extraFlags);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;

        try
        {
            using Icon icon = Icon.FromHandle(info.hIcon);
            return ToSoftwareBitmap(icon);
        }
        catch
        {
            return null;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    /// <summary>Resolves an icon location string such as "C:\path\file.dll,3" or "file.dll,-78".</summary>
    public static SoftwareBitmap? GetIconFromLocation(string? iconLocation)
    {
        if (string.IsNullOrWhiteSpace(iconLocation)) return null;

        string location = Environment.ExpandEnvironmentVariables(iconLocation).Replace("\"", "");
        int index = location.LastIndexOf(',');
        string iconPath;
        int iconIndex = 0;

        if (index == -1) iconPath = location;
        else if (File.Exists(location)) iconPath = location;
        else
        {
            bool flag = int.TryParse(location[(index + 1)..], out iconIndex);
            iconPath = flag ? location[..index] : location;
        }

        return GetIconFromFile(iconPath, iconIndex);
    }

    /// <summary>Extracts the icon at the given index from a file (negative index = resource identifier).</summary>
    public static SoftwareBitmap? GetIconFromFile(string? iconPath, int iconIndex)
    {
        if (string.IsNullOrWhiteSpace(iconPath)) return null;
        iconPath = Environment.ExpandEnvironmentVariables(iconPath).Replace("\"", "");

        IntPtr hIcon = IntPtr.Zero;
        IntPtr hInst = IntPtr.Zero;
        try
        {
            if (iconIndex == -1)
            {
                hInst = LoadLibrary(iconPath);
                hIcon = LoadImage(hInst, "#1", 1, 32, 32, 0);
            }
            else
            {
                IntPtr[] icons = new IntPtr[1];
                int count = ExtractIconEx(iconPath, iconIndex, icons, null, 1);
                if (count > 0) hIcon = icons[0];
            }

            if (hIcon == IntPtr.Zero) return null;
            using Icon icon = Icon.FromHandle(hIcon);
            return ToSoftwareBitmap(icon);
        }
        catch
        {
            return null;
        }
        finally
        {
            DestroyIcon(hIcon);
            if (hInst != IntPtr.Zero) FreeLibrary(hInst);
        }
    }

    private static SoftwareBitmap? ToSoftwareBitmap(Icon icon)
    {
        using Bitmap bitmap = icon.ToBitmap();
        if (bitmap.Width <= 0 || bitmap.Height <= 0) return null;

        Rectangle rect = new(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int stride = data.Stride;
            byte[] bytes = new byte[stride * height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

            // Convert ARGB (straight alpha, padded scanlines) to tightly packed, premultiplied BGRA.
            byte[] tight = new byte[width * 4 * height];
            for (int y = 0; y < height; y++)
            {
                int src = y * stride;
                int dst = y * width * 4;
                for (int x = 0; x < width; x++)
                {
                    int s = src + x * 4;
                    int d = dst + x * 4;
                    byte a = bytes[s + 3];
                    tight[d] = (byte)(bytes[s] * a / 255);        // B
                    tight[d + 1] = (byte)(bytes[s + 1] * a / 255); // G
                    tight[d + 2] = (byte)(bytes[s + 2] * a / 255); // R
                    tight[d + 3] = a;                              // A
                }
            }

            return SoftwareBitmap.CreateCopyFromBuffer(tight.AsBuffer(), BitmapPixelFormat.Bgra8,
                width, height, BitmapAlphaMode.Premultiplied);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}

using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AntiPilot.Interop;

/// <summary>One entry of the shell's Apps folder (everything the Start menu "All apps" list shows).</summary>
public sealed record ShellAppEntry(string Name, string ParsingName);

public static class ShellApps
{
    /// <summary>
    /// Enumerates "shell:AppsFolder". Must run on an STA thread; use <see cref="EnumerateAsync"/>
    /// from UI code.
    /// </summary>
    public static List<ShellAppEntry> Enumerate()
    {
        var result = new List<ShellAppEntry>();

        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null)
        {
            Log.Write("Shell.Application is not registered; cannot list installed apps.");
            return result;
        }

        object? shell = Activator.CreateInstance(shellType);
        if (shell is null)
        {
            return result;
        }

        try
        {
            object? folder = Invoke(shell, "NameSpace", "shell:AppsFolder");
            if (folder is null)
            {
                return result;
            }

            object? items = Invoke(folder, "Items");
            if (items is null)
            {
                return result;
            }

            int count = Convert.ToInt32(Invoke(items, "Count") ?? 0);
            for (int i = 0; i < count; i++)
            {
                object? item = null;
                try
                {
                    item = Invoke(items, "Item", i);
                    if (item is null)
                    {
                        continue;
                    }

                    var name = Invoke(item, "Name") as string;
                    var path = Invoke(item, "Path") as string;
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(path) && !IsSelf(name!, path!))
                    {
                        result.Add(new ShellAppEntry(name!, path!));
                    }
                }
                catch (Exception ex)
                {
                    Log.Write($"Skipping Apps folder item {i}: {ex.Message}");
                }
                finally
                {
                    if (item is not null && Marshal.IsComObject(item))
                    {
                        Marshal.FinalReleaseComObject(item);
                    }
                }
            }
        }
        finally
        {
            if (Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        result.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return result;
    }

    public static Task<List<ShellAppEntry>> EnumerateAsync()
    {
        var tcs = new TaskCompletionSource<List<ShellAppEntry>>();
        var thread = new Thread(() =>
        {
            try
            {
                tcs.SetResult(Enumerate());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        })
        {
            IsBackground = true,
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    private static object? Invoke(object target, string member, params object[] args) =>
        target.GetType().InvokeMember(member, BindingFlags.InvokeMethod | BindingFlags.GetProperty, null, target, args);

    /// <summary>The Start-menu names the manifest gives this app's own entries. Not localised, so they can be matched.</summary>
    private static readonly string[] OwnNames = ["AntiPilot", "AntiPilot Settings", "AntiPilot tray icon"];

    /// <summary>
    /// A package family of ours, whichever publisher it was signed under: the Store one is
    /// "5676LambrosVasiliou.AntiPilot_ry1r8aenh16n2", a sideload is "AntiPilot_" and a different
    /// hash, and the thirteen characters after the underscore are always the publisher hash.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex OwnFamilyPattern =
        new(@"(^|\.)AntiPilot_[a-z0-9]{13}!", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// True for this app's own Apps-folder entries, which are left out of every list the user
    /// picks from. Pointing the key at AntiPilot starts AntiPilot, which reads the config and
    /// points the key at AntiPilot: a loop with a very short fuse. The running package's family
    /// is the exact test; the name and pattern checks cover a debug build, which has no family
    /// but is looking at the same Start menu as the installed copy.
    /// </summary>
    internal static bool IsSelf(string name, string parsingName)
    {
        var family = NativeMethods.GetCurrentPackageFamilyName();
        if (family is not null && parsingName.StartsWith(family + "!", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return OwnFamilyPattern.IsMatch(parsingName) ||
            OwnNames.Any(own => own.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The shell's icon for a file, folder or program, at the given size. Null for anything the
    /// shell cannot resolve, which includes URLs — those get a glyph from the caller instead.
    /// </summary>
    public static Bitmap? TryGetFileIcon(string path, int size)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var expanded = Environment.ExpandEnvironmentVariables(path).Trim();
        if (Uri.TryCreate(expanded, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return null;
        }

        uint flags = SHGFI_ICON | (size > 16 ? SHGFI_LARGEICON : SHGFI_SMALLICON);
        var info = new SHFILEINFO();

        // A path that no longer exists still has an extension, and the extension still has an
        // icon; asking by attributes gets that rather than nothing.
        bool exists = File.Exists(expanded) || Directory.Exists(expanded);
        nint result = exists
            ? SHGetFileInfoW(expanded, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags)
            : SHGetFileInfoW(expanded, FILE_ATTRIBUTE_NORMAL, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags | SHGFI_USEFILEATTRIBUTES);

        if (result == 0 || info.hIcon == 0)
        {
            return null;
        }

        try
        {
            using var icon = Icon.FromHandle(info.hIcon);
            using var source = icon.ToBitmap();
            if (source.Width == size && source.Height == size)
            {
                return new Bitmap(source);
            }

            var scaled = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(scaled);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.DrawImage(source, 0, 0, size, size);
            return scaled;
        }
        catch (Exception ex)
        {
            Log.Write($"Could not read the icon for '{path}': {ex.Message}");
            return null;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_SMALLICON = 0x1;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public nint hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfoW(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint hIcon);

    /// <summary>
    /// True when the shell can still resolve this Apps-folder entry, i.e. the app is installed.
    /// Cheaper than enumerating the whole folder, which is what makes it usable on a Save.
    /// </summary>
    public static bool Exists(string parsingName)
    {
        if (string.IsNullOrWhiteSpace(parsingName))
        {
            return false;
        }

        object? item = null;
        try
        {
            var riid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName($"shell:AppsFolder\\{parsingName}", 0, ref riid, out item);
            return item is not null;
        }
        catch (Exception)
        {
            // SHCreateItemFromParsingName throws rather than returning a failed HRESULT here,
            // because the import is declared PreserveSig = false.
            return false;
        }
        finally
        {
            if (item is not null && Marshal.IsComObject(item))
            {
                Marshal.FinalReleaseComObject(item);
            }
        }
    }

    // ---- icons -------------------------------------------------------------

    /// <summary>Asks the shell for the icon of an Apps-folder entry. Returns null when unavailable.</summary>
    public static Bitmap? TryGetIcon(string parsingName, int size)
    {
        nint hBitmap = 0;
        try
        {
            var riid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName($"shell:AppsFolder\\{parsingName}", 0, ref riid, out object factoryObj);
            var factory = (IShellItemImageFactory)factoryObj;

            const int SIIGBF_ICONONLY = 0x4;
            const int SIIGBF_BIGGERSIZEOK = 0x1;
            int hr = factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, out hBitmap);
            Marshal.FinalReleaseComObject(factory);

            if (hr < 0 || hBitmap == 0)
            {
                return null;
            }

            return BitmapFromHBitmap(hBitmap, size);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hBitmap != 0)
            {
                DeleteObject(hBitmap);
            }
        }
    }

    /// <summary>
    /// Copies an HBITMAP into a managed 32bpp ARGB bitmap. Image.FromHbitmap would drop the
    /// alpha channel and leave black fringes around every icon.
    /// </summary>
    private static Bitmap? BitmapFromHBitmap(nint hBitmap, int requestedSize)
    {
        var info = new BITMAP();
        if (GetObject(hBitmap, Marshal.SizeOf<BITMAP>(), ref info) == 0)
        {
            return null;
        }

        if (info.bmBits == 0 || info.bmBitsPixel != 32)
        {
            // Not a 32bpp DIB section: fall back to the lossy conversion.
            using var plain = Image.FromHbitmap(hBitmap);
            return new Bitmap(plain);
        }

        // The shell hands back a top-down DIB, so the stride is positive and row 0 is the top row.
        using var source = new Bitmap(info.bmWidth, info.bmHeight, info.bmWidthBytes, PixelFormat.Format32bppArgb, info.bmBits);
        var copy = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(copy))
        {
            g.Clear(Color.Transparent);
            g.DrawImageUnscaled(source, 0, 0);
        }

        if (copy.Width != requestedSize || copy.Height != requestedSize)
        {
            var scaled = new Bitmap(requestedSize, requestedSize, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(copy, 0, 0, requestedSize, requestedSize);
            }

            copy.Dispose();
            return scaled;
        }

        return copy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public nint bmBits;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, int flags, out nint phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string pszPath,
        nint pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(nint hObject, int nCount, ref BITMAP lpObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint hObject);
}

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
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(path))
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

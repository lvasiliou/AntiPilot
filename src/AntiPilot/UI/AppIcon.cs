namespace AntiPilot.UI;

/// <summary>
/// Finds the packaged logo files. The MSIX asset set has no plain "Square44x44Logo.png" — only
/// scale- and targetsize-qualified variants that Windows resolves through resources.pri — so code
/// that wants a bitmap has to pick a concrete file itself.
/// </summary>
internal static class AppIcon
{
    private static readonly int[] TargetSizes = [16, 24, 32, 48, 256];

    private static string ImagesDirectory => Path.Combine(AppContext.BaseDirectory, "Images");

    /// <summary>Path of the logo closest to (and preferably not smaller than) the wanted size.</summary>
    public static string? FindLogo(int pixels)
    {
        var dir = ImagesDirectory;
        if (!Directory.Exists(dir))
        {
            return null;
        }

        var order = TargetSizes.Where(t => t >= pixels)
            .Concat(TargetSizes.Reverse())
            .Distinct();

        foreach (var size in order)
        {
            var path = Path.Combine(dir, $"Square44x44Logo.targetsize-{size}.png");
            if (File.Exists(path))
            {
                return path;
            }
        }

        return Directory.EnumerateFiles(dir, "Square44x44Logo*.png").FirstOrDefault();
    }

    /// <summary>Window and notification-area icon at the given pixel size.</summary>
    public static Icon? Load(int size)
    {
        try
        {
            var path = FindLogo(size);
            if (path is null)
            {
                return SystemIcons.Application;
            }

            using var source = new Bitmap(path);
            using var scaled = new Bitmap(size, size);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(source, 0, 0, size, size);
            }

            return Icon.FromHandle(scaled.GetHicon());
        }
        catch (Exception ex)
        {
            Log.Write($"Could not load the app icon: {ex.Message}");
            return SystemIcons.Application;
        }
    }
}

using System.Drawing.Drawing2D;

namespace AntiPilot.UI.Fluent;

/// <summary>Drawing helpers shared by every control in this folder.</summary>
internal static class FluentPaint
{
    /// <summary>A rounded rectangle. Radius is clamped so a short control cannot fold in on itself.</summary>
    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        radius = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));

        var path = new GraphicsPath();

        if (radius == 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        int diameter = radius * 2;
        var corner = new Rectangle(bounds.X, bounds.Y, diameter, diameter);

        path.AddArc(corner, 180, 90);
        corner.X = bounds.Right - diameter;
        path.AddArc(corner, 270, 90);
        corner.Y = bounds.Bottom - diameter;
        path.AddArc(corner, 0, 90);
        corner.X = bounds.X;
        path.AddArc(corner, 90, 90);
        path.CloseFigure();

        return path;
    }

    /// <summary>Fills and outlines a rounded rectangle in one go.</summary>
    public static void DrawSurface(Graphics g, Rectangle bounds, int radius, Color fill, Color? stroke = null)
    {
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Half a pixel in, or the antialiased outline is clipped by the control's own edge.
        var outline = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

        using (var path = RoundedRect(outline, radius))
        using (var brush = new SolidBrush(fill))
        {
            g.FillPath(brush, path);

            if (stroke is { } strokeColour)
            {
                using var pen = new Pen(strokeColour);
                g.DrawPath(pen, path);
            }
        }

        g.SmoothingMode = previous;
    }

    /// <summary>
    /// Text formatting used everywhere: no prefix handling, because a stray ampersand in an app
    /// name or a file path should not silently become an access-key underline.
    /// </summary>
    public const TextFormatFlags Text =
        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

    public const TextFormatFlags TextLeft = Text | TextFormatFlags.VerticalCenter;

    public const TextFormatFlags TextCentre = Text | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter;

    /// <summary>Scales a design-time pixel measurement to the control's DPI.</summary>
    public static int Dpi(Control control, int pixels) => (int)Math.Round(pixels * control.DeviceDpi / 96.0);

    /// <summary>True when the UI language runs right to left, and the layout below should mirror.</summary>
    public static bool Rtl => Theme.IsRightToLeft;

    /// <summary>
    /// Text flags for the leading edge — left in English, right in Arabic — plus the reading-order
    /// flag GDI needs to lay the run out in the right direction.
    /// </summary>
    public static TextFormatFlags Leading(TextFormatFlags flags) => Rtl
        ? flags | TextFormatFlags.Right | TextFormatFlags.RightToLeft
        : flags | TextFormatFlags.Left;

    /// <summary>Mirrors a rectangle inside its container when the language runs right to left.</summary>
    public static Rectangle Mirror(Rectangle bounds, int containerWidth) => Rtl
        ? bounds with { X = containerWidth - bounds.Right }
        : bounds;
}

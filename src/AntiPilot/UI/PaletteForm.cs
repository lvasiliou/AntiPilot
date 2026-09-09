using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using AntiPilot.Interop;
using AntiPilot.UI.Fluent;

namespace AntiPilot.UI;

/// <summary>
/// The quick-launch palette: one key press, a short list, and the thing you wanted.
///
/// Keyboard-first on purpose. The user has just pressed a key, so their hands are already in the
/// right place — 1-9 runs an entry outright, typing searches, Enter takes the top match and Esc
/// leaves without doing anything. Clicking works too, but nothing here requires the mouse.
///
/// Acrylic, which decides how the window is built. A backdrop shows through wherever the client
/// area's alpha is zero, and everything GDI draws — every standard control, every TextRenderer
/// call — lands with alpha zero and turns into a hole. So there are no child controls at all: the
/// search field, its caret and the list are painted by the form through GDI+ into a bitmap that
/// keeps its alpha, and blitted in one go. Typing goes to the form, which is what has focus anyway.
/// </summary>
public sealed class PaletteForm : Form
{
    private const int MaxNumbered = 9;
    private const int MaxVisibleRows = 8;

    // Every measurement here is at the design DPI and taken to the display's through FluentPaint.Dpi.
    private const int PaletteWidth = 520;
    private const int Inset = 12;
    private const int SearchHeight = 40;
    private const int RowHeight = 48;
    private const int RowGap = 2;
    private const int IconSize = 24;
    private const int BottomPadding = 10;

    private readonly List<KeyAction> _entries;
    private readonly List<KeyAction> _shown = [];
    private readonly Dictionary<KeyAction, Bitmap> _icons = [];
    private readonly CancellationTokenSource _cancel = new();
    private readonly System.Windows.Forms.Timer _caret = new() { Interval = 530 };

    private readonly Font _searchFont;
    private readonly Font _secondaryFont = Typography.Caption;

    private string _query = string.Empty;
    private int _selected;
    private int _hover = -1;
    private int _scroll;
    private bool _caretOn = true;
    private bool _glass;
    private KeyAction? _chosen;

    private PaletteForm(List<KeyAction> entries)
    {
        _entries = entries;

        Text = Strings.PaletteWindowTitle;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        TopMost = true;
        Theme.ScaleFromDesignDpi(this);
        Font = Typography.Body;
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Cursor = Cursors.Default;
        Icon = AppIcon.Load(32);

        // Nothing is erased and nothing is painted twice: OnPaint produces the whole window.
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque |
                 ControlStyles.ResizeRedraw, true);

        _searchFont = new Font(Typography.Body.FontFamily, 12f, FontStyle.Regular, GraphicsUnit.Point);

        ClientSize = new Size(PaletteWidth, 0);
        ApplyFilter();

        _caret.Tick += (_, _) =>
        {
            _caretOn = !_caretOn;
            Invalidate(SearchField());
        };

        Theme.ApplyDirection(this);
        Deactivate += (_, _) => Close();
    }

    /// <summary>
    /// Opens the palette for the entries in <paramref name="config"/>. False means it could not be
    /// shown — which, when nothing has been added to the palette yet, is worth saying out loud.
    /// </summary>
    public static bool Show(AppConfig config, ActionFeedback feedback)
    {
        // A palette entry that opens the palette would recurse; drop those rather than police it later.
        var entries = config.Palette
            .Where(action => action.IsConfigured && action.Kind != ActionKind.Palette)
            .ToList();

        if (entries.Count == 0)
        {
            Log.Write("Palette requested but no entries are configured.");
            Notifier.ShowError(Strings.PaletteShort, Strings.PaletteEmptyWarning, feedback);
            return false;
        }

        // A key press has not touched WinForms until now, and this has to happen before the first
        // control exists or the palette comes up unthemed and at the wrong DPI.
        WinFormsHost.Ensure();

        var form = new PaletteForm(entries);

        if (Application.MessageLoop)
        {
            // Inside the tray process: hand it to the loop that is already running.
            form.Show();
            form.Activate();
            return true;
        }

        Application.Run(form);
        return true;
    }

    // ---- window ------------------------------------------------------------

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        _glass = Theme.ApplyAcrylic(this);
        _caret.Start();
        StartIconLoad();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Deliberately empty. The background is the acrylic, and painting anything here through
        // GDI would punch an alpha-zero hole exactly where the tint is meant to go.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        int width = Math.Max(1, ClientSize.Width);
        int height = Math.Max(1, ClientSize.Height);

        using var buffer = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(buffer))
        {
            Render(g);
        }

        if (!_glass)
        {
            e.Graphics.DrawImageUnscaled(buffer, 0, 0);
            return;
        }

        // A straight BitBlt is the one path that carries the alpha channel to the window intact.
        // GDI+ drawing onto the window's own device context would go through GDI and lose it.
        nint target = e.Graphics.GetHdc();
        nint memory = CreateCompatibleDC(target);
        nint bitmap = buffer.GetHbitmap(Color.FromArgb(0));
        nint previous = SelectObject(memory, bitmap);

        BitBlt(target, 0, 0, width, height, memory, 0, 0, SRCCOPY);

        SelectObject(memory, previous);
        DeleteObject(bitmap);
        DeleteDC(memory);
        e.Graphics.ReleaseHdc(target);
    }

    private bool _released;

    protected override void Dispose(bool disposing)
    {
        // WinForms disposes a closed form more than once, and a cancelled-then-disposed token
        // source throws on the second visit.
        if (disposing && !_released)
        {
            _released = true;
            _cancel.Cancel();
            _cancel.Dispose();
            _caret.Dispose();
            _searchFont.Dispose();

            foreach (var icon in _icons.Values)
            {
                icon.Dispose();
            }

            _icons.Clear();
        }

        base.Dispose(disposing);
    }

    // ---- painting ----------------------------------------------------------

    private int Dpi(int pixels) => FluentPaint.Dpi(this, pixels);

    private Rectangle SearchField() =>
        new(Dpi(Inset), Dpi(Inset), ClientSize.Width - 2 * Dpi(Inset), Dpi(SearchHeight));

    private int RowsTop() => SearchField().Bottom + Dpi(8);

    private Rectangle RowRect(int visibleIndex) =>
        new(Dpi(Inset), RowsTop() + visibleIndex * (Dpi(RowHeight) + Dpi(RowGap)), ClientSize.Width - 2 * Dpi(Inset), Dpi(RowHeight));

    private int VisibleRows() => Math.Clamp(_shown.Count, 1, MaxVisibleRows);

    private void Render(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // ClearType is designed for an opaque background it knows the colour of. Over a blur it
        // leaves colour fringes on every letter; greyscale antialiasing does not.
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        if (_glass)
        {
            // The acrylic supplies the blur and the noise; this is only the card colour laid over it
            // thinly enough that the desktop still reads through.
            g.Clear(Color.Transparent);
            using var tint = new SolidBrush(Color.FromArgb(Theme.IsDark ? 0x8C : 0xA6, Theme.Card));
            g.FillRectangle(tint, 0, 0, ClientSize.Width, ClientSize.Height);
        }
        else
        {
            g.Clear(Theme.Card);
        }

        PaintSearchField(g, SearchField());

        if (_shown.Count == 0)
        {
            DrawText(g, Strings.PaletteNoMatch, Font, RowRect(0), Theme.SecondaryText, FluentPaint.TextCentre);
            return;
        }

        int visible = VisibleRows();
        for (int i = 0; i < visible && _scroll + i < _shown.Count; i++)
        {
            PaintRow(g, _scroll + i, RowRect(i));
        }

        if (_shown.Count > MaxVisibleRows)
        {
            PaintScrollHint(g);
        }
    }

    private void PaintSearchField(Graphics g, Rectangle field)
    {
        bool rtl = FluentPaint.Rtl;
        int glyphWidth = Dpi(28);

        // The field: a faint fill so it reads as a control, and the accent underline Fluent gives a
        // text box that has focus — which this one always has.
        using (var path = FluentPaint.RoundedRect(field, Dpi(Theme.ControlRadius)))
        using (var fill = new SolidBrush(Color.FromArgb(Theme.IsDark ? 0x1E : 0x14, Theme.IsDark ? Color.White : Color.Black)))
        {
            g.FillPath(fill, path);
        }

        using (var stroke = new Pen(Theme.ControlStroke, Dpi(1)))
        {
            g.DrawLine(stroke, field.Left + Dpi(2), field.Bottom - 1, field.Right - Dpi(2), field.Bottom - 1);
        }

        using (var accent = new Pen(Theme.Accent, Dpi(2)))
        {
            g.DrawLine(accent, field.Left + Dpi(2), field.Bottom - 1, field.Right - Dpi(2), field.Bottom - 1);
        }

        var glyphRect = FluentPaint.Mirror(new Rectangle(field.Left + Dpi(10), field.Top, glyphWidth, field.Height), ClientSize.Width);
        DrawGlyph(g, Typography.Glyphs.Search, Typography.SmallIcon, glyphRect, Theme.SecondaryText);

        var textRect = new Rectangle(field.Left + Dpi(10) + glyphWidth + Dpi(4), field.Top, field.Width - glyphWidth - Dpi(24), field.Height);
        textRect = FluentPaint.Mirror(textRect, ClientSize.Width);

        bool placeholder = _query.Length == 0;
        string text = placeholder ? Strings.PaletteFilterPlaceholder : _query;
        var colour = placeholder ? Theme.SecondaryText : Theme.Text;

        var flags = FluentPaint.Leading(FluentPaint.Text | TextFormatFlags.VerticalCenter);
        DrawText(g, text, _searchFont, textRect, colour, flags);

        if (_caretOn)
        {
            int textWidth = placeholder ? 0 : MeasureText(g, _query, _searchFont).Width;
            int caretX = rtl ? textRect.Right - textWidth - Dpi(1) : textRect.Left + textWidth + Dpi(1);
            int caretHeight = _searchFont.Height;
            using var caret = new Pen(Theme.Text, Dpi(1));
            g.DrawLine(caret, caretX, field.Top + (field.Height - caretHeight) / 2, caretX, field.Top + (field.Height + caretHeight) / 2);
        }
    }

    private void PaintRow(Graphics g, int index, Rectangle row)
    {
        var entry = _shown[index];
        bool selected = index == _selected;
        bool hovered = index == _hover;

        if (selected || hovered)
        {
            int alpha = selected ? (Theme.IsDark ? 0x2E : 0x1A) : (Theme.IsDark ? 0x16 : 0x0C);
            using var fill = new SolidBrush(Color.FromArgb(alpha, Theme.IsDark ? Color.White : Color.Black));
            using var path = FluentPaint.RoundedRect(row, Dpi(Theme.ControlRadius));
            g.FillPath(fill, path);
        }

        if (selected)
        {
            // The accent pill on the leading edge is how Windows 11 marks the selected item in a
            // list, and the whole row stays readable because the text never inverts.
            var pill = FluentPaint.Mirror(new Rectangle(row.Left + Dpi(2), row.Top + (row.Height - Dpi(18)) / 2, Dpi(3), Dpi(18)), ClientSize.Width);
            using var brush = new SolidBrush(Theme.Accent);
            using var path = FluentPaint.RoundedRect(pill, pill.Width / 2);
            g.FillPath(brush, path);
        }

        int iconSize = Dpi(IconSize);
        var iconRect = FluentPaint.Mirror(new Rectangle(row.Left + Dpi(14), row.Top + (row.Height - iconSize) / 2, iconSize, iconSize), ClientSize.Width);
        PaintIcon(g, entry, iconRect);

        // 1-9 only mean anything while nothing has been typed, so the hints go away when they stop being true.
        int badgeWidth = index < MaxNumbered && _query.Length == 0 ? Dpi(26) : 0;
        if (badgeWidth > 0)
        {
            var badge = FluentPaint.Mirror(new Rectangle(row.Right - Dpi(12) - badgeWidth, row.Top + (row.Height - Dpi(22)) / 2, badgeWidth, Dpi(22)), ClientSize.Width);
            using var outline = new Pen(Theme.ControlStroke, Dpi(1));
            using var path = FluentPaint.RoundedRect(badge, Dpi(Theme.ControlRadius));
            g.DrawPath(outline, path);
            DrawText(g, (index + 1).ToString(), _secondaryFont, badge, Theme.SecondaryText, FluentPaint.TextCentre);
        }

        int textLeft = row.Left + Dpi(14) + iconSize + Dpi(12);
        int textWidth = row.Width - (textLeft - row.Left) - Dpi(12) - (badgeWidth > 0 ? badgeWidth + Dpi(10) : 0);
        string primary = entry.Describe();
        string? secondary = SecondaryLine(entry);

        var textFlags = FluentPaint.Leading(FluentPaint.Text | TextFormatFlags.VerticalCenter);
        if (secondary is null)
        {
            var single = FluentPaint.Mirror(new Rectangle(textLeft, row.Top, textWidth, row.Height), ClientSize.Width);
            DrawText(g, primary, Font, single, Theme.Text, textFlags);
            return;
        }

        int primaryHeight = Font.Height;
        int secondaryHeight = _secondaryFont.Height;
        int top = row.Top + (row.Height - primaryHeight - secondaryHeight) / 2;
        var first = FluentPaint.Mirror(new Rectangle(textLeft, top, textWidth, primaryHeight), ClientSize.Width);
        var second = FluentPaint.Mirror(new Rectangle(textLeft, top + primaryHeight, textWidth, secondaryHeight), ClientSize.Width);
        DrawText(g, primary, Font, first, Theme.Text, textFlags);
        DrawText(g, secondary, _secondaryFont, second, Theme.SecondaryText, textFlags);
    }

    private void PaintIcon(Graphics g, KeyAction entry, Rectangle rect)
    {
        if (_icons.TryGetValue(entry, out var bitmap))
        {
            g.DrawImage(bitmap, rect);
            return;
        }

        // No shell icon, or not loaded yet: a glyph that says what kind of thing this is.
        string glyph = entry.Kind switch
        {
            ActionKind.ShellApp => Typography.Glyphs.AppIcon,
            ActionKind.File when IsUrl(entry.Path) => Typography.Glyphs.Globe,
            ActionKind.File when Directory.Exists(Environment.ExpandEnvironmentVariables(entry.Path ?? string.Empty)) => Typography.Glyphs.Folder,
            ActionKind.File => Typography.Glyphs.Page,
            _ => Typography.Glyphs.Keyboard,
        };

        DrawGlyph(g, glyph, Typography.Icon, rect, Theme.SecondaryText);
    }

    private void PaintScrollHint(Graphics g)
    {
        // A thin track on the trailing edge, proportional, so it is clear there is more below.
        int trackTop = RowsTop();
        int trackHeight = VisibleRows() * (Dpi(RowHeight) + Dpi(RowGap)) - Dpi(RowGap);
        int thumbHeight = Math.Max(Dpi(24), trackHeight * VisibleRows() / _shown.Count);
        int thumbTop = trackTop + (trackHeight - thumbHeight) * _scroll / Math.Max(1, _shown.Count - VisibleRows());

        var thumb = FluentPaint.Mirror(new Rectangle(ClientSize.Width - Dpi(Inset) + Dpi(4), thumbTop, Dpi(3), thumbHeight), ClientSize.Width);
        using var brush = new SolidBrush(Color.FromArgb(0x60, Theme.SecondaryText));
        using var path = FluentPaint.RoundedRect(thumb, thumb.Width / 2);
        g.FillPath(brush, path);
    }

    /// <summary>
    /// What the entry actually runs, shown under a user-given name so the name does not have to
    /// carry it. Nothing when the entry has no name of its own, since the first line already says.
    /// </summary>
    private static string? SecondaryLine(KeyAction entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Label))
        {
            return null;
        }

        // Describe() shortens a path to its file name, which for a link is the last segment of the
        // URL: "AntiPilot" under "AntiPilot on GitHub" says nothing. The link itself does.
        if (entry.Kind == ActionKind.File && IsUrl(entry.Path))
        {
            return entry.Path!.Trim();
        }

        var unnamed = entry.Clone();
        unnamed.Label = null;
        var description = unnamed.Describe();
        return description.Equals(entry.Label, StringComparison.CurrentCultureIgnoreCase) ? null : description;
    }

    private static bool IsUrl(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        Uri.TryCreate(Environment.ExpandEnvironmentVariables(path!), UriKind.Absolute, out var uri) && !uri.IsFile;

    /// <summary>
    /// Text through GDI+, not TextRenderer. GDI text on this window has alpha zero and vanishes
    /// into the backdrop; GDI+ writes real alpha. Layout flags are honoured the way the rest of the
    /// Fluent folder expects them.
    /// </summary>
    private static void DrawText(Graphics g, string text, Font font, Rectangle bounds, Color colour, TextFormatFlags flags)
    {
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.LineLimit,
            LineAlignment = StringAlignment.Center,
            Alignment = (flags & TextFormatFlags.HorizontalCenter) != 0 ? StringAlignment.Center
                : (flags & TextFormatFlags.Right) != 0 ? StringAlignment.Far
                : StringAlignment.Near,
        };

        if ((flags & TextFormatFlags.RightToLeft) != 0)
        {
            format.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
        }

        using var brush = new SolidBrush(colour);
        g.DrawString(text, font, brush, bounds, format);
    }

    private static Size MeasureText(Graphics g, string text, Font font)
    {
        using var format = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.NoWrap };
        var size = g.MeasureString(text, font, int.MaxValue, format);
        return new Size((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
    }

    private static void DrawGlyph(Graphics g, string glyph, Font font, Rectangle bounds, Color colour)
    {
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var brush = new SolidBrush(colour);
        g.DrawString(glyph, font, brush, bounds, format);
    }

    // ---- icons -------------------------------------------------------------

    /// <summary>
    /// Icons come from the shell one at a time and that is slow, so the list shows up with glyphs
    /// and the real icons fill in as they arrive.
    /// </summary>
    private void StartIconLoad()
    {
        var entries = _entries.Where(e => e.Kind is ActionKind.ShellApp or ActionKind.File).ToList();
        if (entries.Count == 0)
        {
            return;
        }

        int size = Dpi(IconSize);
        var token = _cancel.Token;

        var thread = new Thread(() =>
        {
            foreach (var entry in entries)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                Bitmap? bitmap = entry.Kind == ActionKind.ShellApp
                    ? ShellApps.TryGetIcon(entry.Aumid!, size)
                    : ShellApps.TryGetFileIcon(entry.Path!, size);

                if (bitmap is null)
                {
                    continue;
                }

                try
                {
                    BeginInvoke(() =>
                    {
                        if (token.IsCancellationRequested || IsDisposed || _icons.ContainsKey(entry))
                        {
                            bitmap.Dispose();
                            return;
                        }

                        _icons[entry] = bitmap;
                        Invalidate();
                    });
                }
                catch (Exception)
                {
                    bitmap.Dispose();
                    return; // The window went away first.
                }
            }
        })
        { IsBackground = true };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    // ---- searching ---------------------------------------------------------

    private void ApplyFilter()
    {
        var needle = _query.Trim();

        _shown.Clear();
        _shown.AddRange(string.IsNullOrEmpty(needle)
            ? _entries
            : _entries.Where(entry => entry.Describe().Contains(needle, StringComparison.CurrentCultureIgnoreCase)));

        _selected = 0;
        _scroll = 0;
        _hover = -1;
        ResizeToContent();
        Invalidate();
    }

    private void ResizeToContent()
    {
        int height = RowsTop() + VisibleRows() * (Dpi(RowHeight) + Dpi(RowGap)) - Dpi(RowGap) + Dpi(BottomPadding);
        ClientSize = new Size(Dpi(PaletteWidth), height);
    }

    private void SetQuery(string query)
    {
        if (query == _query)
        {
            return;
        }

        _query = query;
        _caretOn = true;
        ApplyFilter();
    }

    private void MoveSelection(int delta)
    {
        if (_shown.Count == 0)
        {
            return;
        }

        _selected = Math.Clamp(_selected + delta, 0, _shown.Count - 1);
        if (_selected < _scroll)
        {
            _scroll = _selected;
        }
        else if (_selected >= _scroll + MaxVisibleRows)
        {
            _scroll = _selected - MaxVisibleRows + 1;
        }

        Invalidate();
    }

    // ---- keyboard ----------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.KeyCode)
        {
            case Keys.Escape:
                Close();
                e.Handled = true;
                return;

            case Keys.Enter:
                RunSelected();
                e.Handled = true;
                return;

            case Keys.Down:
                MoveSelection(+1);
                e.Handled = true;
                return;

            case Keys.Up:
                MoveSelection(-1);
                e.Handled = true;
                return;

            case Keys.Back when _query.Length > 0:
                // Ctrl+Backspace takes the last word, as every text box on Windows does.
                SetQuery(e.Control ? _query.TrimEnd()[..Math.Max(0, _query.TrimEnd().LastIndexOf(' ') + 1)].TrimEnd() : _query[..^1]);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;

            case Keys.V when e.Control:
            case Keys.Insert when e.Shift:
                if (Clipboard.ContainsText())
                {
                    SetQuery(_query + Clipboard.GetText().Replace("\r", string.Empty).Replace("\n", " "));
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
        }

        // 1-9 run an entry outright, but only while nothing has been typed: once a search has
        // started a digit is part of it, and "7-Zip" has to be reachable. Only without modifiers,
        // too — Alt+4 and friends belong to the entry the user is about to launch, not to us.
        if (e.Modifiers == Keys.None && _query.Length == 0 && e.KeyCode is >= Keys.D1 and <= Keys.D9)
        {
            Run(e.KeyCode - Keys.D1);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);

        // Composed characters from an IME arrive here too, which is what makes the field usable in
        // Japanese, Korean and Chinese without a real edit control behind it.
        if (!char.IsControl(e.KeyChar))
        {
            SetQuery(_query + e.KeyChar);
            e.Handled = true;
        }
    }

    protected override void WndProc(ref Message m)
    {
        // Put the IME's composition window at the caret rather than at the window's corner.
        if (m.Msg == WM_IME_STARTCOMPOSITION)
        {
            PositionCompositionWindow();
        }

        base.WndProc(ref m);
    }

    private void PositionCompositionWindow()
    {
        nint context = ImmGetContext(Handle);
        if (context == 0)
        {
            return;
        }

        try
        {
            var field = SearchField();
            int textWidth;
            using (var g = CreateGraphics())
            {
                textWidth = _query.Length == 0 ? 0 : MeasureText(g, _query, _searchFont).Width;
            }

            var form = new COMPOSITIONFORM
            {
                dwStyle = CFS_POINT,
                ptCurrentPos = new Point(field.Left + Dpi(42) + textWidth, field.Top + (field.Height - _searchFont.Height) / 2),
            };

            ImmSetCompositionWindow(context, ref form);
        }
        finally
        {
            ImmReleaseContext(Handle, context);
        }
    }

    // ---- mouse -------------------------------------------------------------

    private int RowAt(Point location)
    {
        int visible = VisibleRows();
        for (int i = 0; i < visible; i++)
        {
            int index = _scroll + i;
            if (index < _shown.Count && RowRect(i).Contains(location))
            {
                return index;
            }
        }

        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        int hover = RowAt(e.Location);
        Cursor = hover >= 0 ? Cursors.Hand : SearchField().Contains(e.Location) ? Cursors.IBeam : Cursors.Default;

        if (hover != _hover)
        {
            _hover = hover;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (_hover != -1)
        {
            _hover = -1;
            Invalidate();
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        if (e.Button == MouseButtons.Left)
        {
            int index = RowAt(e.Location);
            if (index >= 0)
            {
                Run(index);
            }
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (_shown.Count <= MaxVisibleRows)
        {
            return;
        }

        int step = e.Delta > 0 ? -1 : 1;
        _scroll = Math.Clamp(_scroll + step, 0, _shown.Count - MaxVisibleRows);
        _hover = RowAt(e.Location);
        Invalidate();
    }

    // ---- running -----------------------------------------------------------

    private void RunSelected() => Run(_selected);

    private void Run(int index)
    {
        if (index < 0 || index >= _shown.Count)
        {
            return;
        }

        _chosen = _shown[index];
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);

        if (_chosen is null)
        {
            return;
        }

        // Run after the window has gone: a Menu-key or shortcut entry has to land on whatever the
        // user was using before, not on the palette.
        var action = _chosen;
        _chosen = null;
        Log.Write($"Palette entry chosen: {action.Describe()}");
        ActionRunner.Run(action, ActionFeedback.Balloon);
    }

    // ---- native ------------------------------------------------------------

    private const uint SRCCOPY = 0x00CC0020;
    private const int WM_IME_STARTCOMPOSITION = 0x010D;
    private const uint CFS_POINT = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct COMPOSITIONFORM
    {
        public uint dwStyle;
        public Point ptCurrentPos;
        public Rectangle rcArea;
    }

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint hObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(nint hdc, int x, int y, int cx, int cy, nint hdcSrc, int x1, int y1, uint rop);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint hObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint hdc);

    [DllImport("imm32.dll")]
    private static extern nint ImmGetContext(nint hWnd);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmReleaseContext(nint hWnd, nint hImc);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmSetCompositionWindow(nint hImc, ref COMPOSITIONFORM form);
}

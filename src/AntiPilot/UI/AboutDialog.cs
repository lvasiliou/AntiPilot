using System.Reflection;

namespace AntiPilot.UI;

public sealed class AboutDialog : Form
{
    private readonly TextBox _licence = new();
    private readonly Button _close = new();

    private const string Fallback =
        "MIT License\r\n\r\nCopyright (c) 2026 Lambros Vasiliou\r\n\r\n" +
        "Permission is hereby granted, free of charge, to any person obtaining a copy of this " +
        "software and associated documentation files (the \"Software\"), to deal in the Software " +
        "without restriction, including without limitation the rights to use, copy, modify, merge, " +
        "publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons " +
        "to whom the Software is furnished to do so, subject to the following conditions:\r\n\r\n" +
        "The above copyright notice and this permission notice shall be included in all copies or " +
        "substantial portions of the Software.\r\n\r\n" +
        "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, " +
        "INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR " +
        "PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE " +
        "FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR " +
        "OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER " +
        "DEALINGS IN THE SOFTWARE.";

    public AboutDialog()
    {
        Text = Strings.AboutTitle;
        ClientSize = new Size(520, 480);
        MinimumSize = new Size(460, 420);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Theme.ScaleFromDesignDpi(this);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Icon = AppIcon.Load(32);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16, 14, 16, 12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(BuildIdentity(), 0, 0);
        layout.Controls.Add(new Label
        {
            Text = Strings.AboutTagline,
            AutoSize = true,
            ForeColor = Theme.SecondaryText,
            Tag = Theme.SecondaryTag,
            Margin = new Padding(0, 10, 0, 8),
        }, 0, 1);

        _licence.Dock = DockStyle.Fill;
        _licence.Multiline = true;
        _licence.ReadOnly = true;
        _licence.ScrollBars = ScrollBars.Vertical;
        _licence.WordWrap = true;
        _licence.Text = ReadLicence();
        _licence.BackColor = Theme.ListBackground;
        _licence.ForeColor = Theme.Text;
        _licence.Margin = new Padding(0, 0, 0, 12);
        layout.Controls.Add(_licence, 0, 2);

        _close.Text = Strings.Close;
        _close.AutoSize = true;
        _close.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _close.Padding = new Padding(18, 5, 18, 5);
        _close.Anchor = AnchorStyles.Right;
        _close.DialogResult = DialogResult.OK;

        layout.Controls.Add(_close, 0, 3);
        Controls.Add(layout);

        AcceptButton = _close;
        CancelButton = _close;

        Theme.Watch(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // A read-only multiline TextBox grabs focus and shows up entirely selected otherwise.
        _licence.Select(0, 0);
        _licence.ScrollToCaret();
        _close.Focus();
    }

    private Control BuildIdentity()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var logo = new PictureBox
        {
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 12, 0),
        };

        try
        {
            var png = AppIcon.FindLogo(256);
            if (png is not null)
            {
                logo.Image = new Bitmap(png);
            }
        }
        catch (Exception ex)
        {
            Log.Write($"About: could not load the logo: {ex.Message}");
        }

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var identity = CopilotKeyStatus.OwnAumid;

        panel.Controls.Add(logo, 0, 0);
        panel.SetRowSpan(logo, 3);
        panel.Controls.Add(new Label
        {
            Text = Strings.AppName,
            Font = new Font(Font.FontFamily, 15f),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
        }, 1, 0);

        panel.Controls.Add(new Label
        {
            Text = version is null ? Strings.AboutVersionUnknown : Strings.Format(Strings.AboutVersion, $"{version.Major}.{version.Minor}.{version.Build}"),
            AutoSize = true,
            ForeColor = Theme.SecondaryText,
            Tag = Theme.SecondaryTag,
        }, 1, 1);

        panel.Controls.Add(new Label
        {
            Text = identity ?? Strings.AboutUnpackaged,
            AutoSize = true,
            ForeColor = Theme.SecondaryText,
            Tag = Theme.SecondaryTag,
        }, 1, 2);

        return panel;
    }

    private static string ReadLicence()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "LICENSE");
            if (File.Exists(path))
            {
                return File.ReadAllText(path).ReplaceLineEndings("\r\n");
            }
        }
        catch (Exception ex)
        {
            Log.Write($"About: could not read LICENSE: {ex.Message}");
        }

        return Fallback;
    }
}

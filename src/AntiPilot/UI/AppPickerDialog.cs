using AntiPilot.Interop;

namespace AntiPilot.UI;

/// <summary>Lets the user pick any entry from the shell's Apps folder.</summary>
public sealed class AppPickerDialog : Form
{
    private readonly TextBox _search = new();
    private readonly ListView _list = new();
    private readonly ImageList _icons = new();
    private readonly Label _status = new();
    private readonly Button _ok = new();

    private readonly string? _preselect;
    private readonly CancellationTokenSource _cancel = new();
    private List<ShellAppEntry> _all = new();

    public ShellAppEntry? SelectedApp { get; private set; }

    public AppPickerDialog(string? preselectParsingName)
    {
        _preselect = preselectParsingName;

        Text = Strings.PickerTitle;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        ClientSize = new Size(520, 560);
        MinimumSize = new Size(420, 400);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Icon = AppIcon.Load(32);

        int iconSize = (int)(24 * DeviceDpi / 96.0);
        _icons.ImageSize = new Size(iconSize, iconSize);
        _icons.ColorDepth = ColorDepth.Depth32Bit;

        _search.PlaceholderText = Strings.PickerSearch;
        _search.Dock = DockStyle.Top;
        _search.Margin = new Padding(0, 0, 0, 8);
        _search.TextChanged += (_, _) => ApplyFilter();

        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.HeaderStyle = ColumnHeaderStyle.None;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        _list.SmallImageList = _icons;
        _list.BackColor = Theme.ListBackground;
        _list.ForeColor = Theme.Text;
        _list.BorderStyle = BorderStyle.FixedSingle;
        _list.Columns.Add(Strings.PickerColumnApp);
        _list.Resize += (_, _) => FitColumn();
        _list.DoubleClick += (_, _) => Accept();
        _list.SelectedIndexChanged += (_, _) => _ok.Enabled = _list.SelectedItems.Count > 0;

        _status.Dock = DockStyle.Bottom;
        _status.Height = 24;
        _status.Text = Strings.PickerLoading;
        _status.ForeColor = Theme.SecondaryText;

        _ok.Text = Strings.PickerSelect;
        _ok.DialogResult = DialogResult.None;
        _ok.Enabled = false;
        _ok.Size = new Size(100, 30);
        _ok.Click += (_, _) => Accept();

        var cancel = new Button
        {
            Text = Strings.Cancel,
            DialogResult = DialogResult.Cancel,
            Size = new Size(100, 30),
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 46,
            Padding = new Padding(0, 8, 0, 0),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_ok);

        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        root.Controls.Add(_list);
        root.Controls.Add(_status);
        root.Controls.Add(buttons);
        root.Controls.Add(_search);

        Controls.Add(root);
        Theme.Watch(this);
        AcceptButton = _ok;
        CancelButton = cancel;

        Load += async (_, _) => await LoadAppsAsync();
        FormClosed += (_, _) => _cancel.Cancel();
    }

    private async Task LoadAppsAsync()
    {
        try
        {
            _all = await ShellApps.EnumerateAsync();
        }
        catch (Exception ex)
        {
            Log.Write($"Enumerating apps failed: {ex}");
            _status.Text = Strings.PickerFailed;
            return;
        }

        ApplyFilter();
        _status.Text = Strings.Format(Strings.PickerCount, _all.Count);
        StartIconLoad();
    }

    private void ApplyFilter()
    {
        var filter = _search.Text.Trim();

        _list.BeginUpdate();
        _list.Items.Clear();

        foreach (var app in _all)
        {
            if (filter.Length > 0 && app.Name.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) < 0)
            {
                continue;
            }

            var item = new ListViewItem(app.Name)
            {
                Tag = app,
                ImageKey = app.ParsingName,
            };

            _list.Items.Add(item);

            if (app.ParsingName.Equals(_preselect, StringComparison.OrdinalIgnoreCase))
            {
                item.Selected = true;
                item.EnsureVisible();
            }
        }

        FitColumn();
        _list.EndUpdate();
    }

    /// <summary>Keeps the single column exactly as wide as the view so no horizontal scrollbar shows up.</summary>
    private void FitColumn()
    {
        if (_list.Columns.Count > 0)
        {
            _list.Columns[0].Width = Math.Max(60, _list.ClientSize.Width - 4);
        }
    }

    /// <summary>
    /// Icons come from the shell one at a time and that is slow, so the list shows up first and
    /// fills in afterwards.
    /// </summary>
    private void StartIconLoad()
    {
        var entries = _all.ToList();
        int size = _icons.ImageSize.Width;
        var token = _cancel.Token;

        var thread = new Thread(() =>
        {
            foreach (var entry in entries)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var bitmap = ShellApps.TryGetIcon(entry.ParsingName, size);
                if (bitmap is null)
                {
                    continue;
                }

                try
                {
                    BeginInvoke(() =>
                    {
                        if (token.IsCancellationRequested || IsDisposed)
                        {
                            bitmap.Dispose();
                            return;
                        }

                        if (!_icons.Images.ContainsKey(entry.ParsingName))
                        {
                            _icons.Images.Add(entry.ParsingName, bitmap);

                            // The item already carries the key; nudge it so the new image shows.
                            foreach (ListViewItem item in _list.Items)
                            {
                                if (item.ImageKey == entry.ParsingName)
                                {
                                    item.ImageKey = entry.ParsingName;
                                    _list.RedrawItems(item.Index, item.Index, false);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            bitmap.Dispose();
                        }
                    });
                }
                catch (Exception)
                {
                    bitmap.Dispose();
                    return; // Window is gone.
                }
            }
        })
        { IsBackground = true };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private void Accept()
    {
        if (_list.SelectedItems.Count == 0)
        {
            return;
        }

        SelectedApp = _list.SelectedItems[0].Tag as ShellAppEntry;
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancel.Cancel();
            _cancel.Dispose();
            _icons.Dispose();
        }

        base.Dispose(disposing);
    }
}

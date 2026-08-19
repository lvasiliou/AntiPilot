using System.ComponentModel;

namespace AntiPilot.UI;

/// <summary>
/// Captures a chord by having the user press it.
///
/// Ctrl, Alt and Shift are read from the keyboard directly. The Windows key is not: pressing it
/// opens the Start menu before any application sees it, and only a low-level keyboard hook could
/// swallow that — a resident hook is a lot of machinery to add to an app whose whole point is not
/// staying resident. It gets a checkbox instead, which is also easier to discover.
/// </summary>
internal sealed class HotkeyBox : UserControl
{
    private readonly TextBox _display = new();
    private readonly CheckBox _windows = new();
    private readonly Button _clear = new();

    private HotkeyDefinition? _value;
    private bool _updating;

    public event EventHandler? HotkeyChanged;

    public HotkeyBox()
    {
        AutoScaleMode = AutoScaleMode.Font;
        Height = 30;
        BackColor = Theme.Window;
        ForeColor = Theme.Text;

        _display.ReadOnly = true;
        _display.Dock = DockStyle.Fill;
        _display.TextAlign = HorizontalAlignment.Center;
        _display.Cursor = Cursors.Hand;
        _display.PlaceholderText = Strings.HotkeyPlaceholder;
        _display.ShortcutsEnabled = false;
        _display.GotFocus += (_, _) => Repaint();
        _display.LostFocus += (_, _) => Repaint();
        _display.KeyDown += OnKeyDown;

        // A read-only box still shows a caret, which invites typing that is then ignored.
        _display.Enter += (_, _) => _display.HideSelection = true;

        _windows.Text = "Win";
        _windows.AutoSize = true;
        _windows.Dock = DockStyle.Right;
        _windows.Margin = new Padding(8, 0, 0, 0);
        _windows.CheckedChanged += (_, _) =>
        {
            if (_updating)
            {
                return;
            }

            if (_value is not null)
            {
                _value = new HotkeyDefinition(_value.VirtualKey, _value.Control, _value.Alt, _value.Shift, _windows.Checked);
            }

            Repaint();
            HotkeyChanged?.Invoke(this, EventArgs.Empty);
        };

        _clear.Text = Strings.HotkeyClear;
        _clear.AutoSize = true;
        _clear.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _clear.Dock = DockStyle.Right;
        _clear.Padding = new Padding(6, 2, 6, 2);
        _clear.Click += (_, _) => Value = null;

        Controls.Add(_display);
        Controls.Add(_clear);
        Controls.Add(_windows);
    }

    /// <summary>The captured chord, or null when nothing has been captured yet.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public HotkeyDefinition? Value
    {
        get => _value;
        set
        {
            _value = value;
            _updating = true;
            _windows.Checked = value?.Windows ?? false;
            _updating = false;
            Repaint();
            HotkeyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Repaint()
    {
        _display.Text = _value?.Format() ?? string.Empty;

        if (_display.Focused && _value is null)
        {
            _display.Text = Strings.HotkeyRecording;
        }

        _display.BackColor = _display.Focused ? Theme.Card : Theme.ListBackground;
        _display.ForeColor = Theme.Text;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;

        var key = e.KeyCode;

        // Esc abandons the capture rather than becoming the shortcut; a shortcut of Esc alone would
        // make the box impossible to leave without setting one.
        if (key == Keys.Escape && e.Modifiers == Keys.None)
        {
            Parent?.SelectNextControl(this, forward: true, tabStopOnly: true, nested: true, wrap: true);
            return;
        }

        if (HotkeyDefinition.IsModifierKey((int)key))
        {
            return;
        }

        _value = new HotkeyDefinition(
            (int)key,
            control: e.Control,
            alt: e.Alt,
            shift: e.Shift,
            windows: _windows.Checked);

        Repaint();
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Menu-activating and navigation keys never reach KeyDown by the usual route, so they are
    /// taken here — otherwise Alt+F or Tab would move the focus instead of being captured.
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!_display.Focused)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        const int WM_KEYDOWN = 0x0100;
        const int WM_SYSKEYDOWN = 0x0104;

        if (msg.Msg is not (WM_KEYDOWN or WM_SYSKEYDOWN))
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        var key = keyData & Keys.KeyCode;
        if (key is Keys.Escape or Keys.None || HotkeyDefinition.IsModifierKey((int)key))
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        _value = new HotkeyDefinition(
            (int)key,
            control: (keyData & Keys.Control) != 0,
            alt: (keyData & Keys.Alt) != 0,
            shift: (keyData & Keys.Shift) != 0,
            windows: _windows.Checked);

        Repaint();
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
}

using Microsoft.Win32;

namespace CalendarWeekTray;

// PROTOTYPE — ticket 06. THROWAWAY. The switcher: the same designs as the contact sheet, but in
// the real taskbar. An image viewer at 1:1 still lies — it has no taskbar background, no
// neighbouring icons and no DPI.

internal sealed class Prototype06Lab : ApplicationContext
{
    private static readonly Design[] Designs = Enum.GetValues<Design>();

    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _designsMenu;
    private nint _iconHandle;
    private int _index;
    private int _week = 11;
    private bool _padded = false;

    public Prototype06Lab()
    {
        ContextMenuStrip menu = new();

        menu.Items.Add("Next design", null, (_, _) => Move(+1));
        menu.Items.Add("Previous design", null, (_, _) => Move(-1));
        menu.Items.Add(new ToolStripSeparator());

        _designsMenu = new ToolStripMenuItem("Jump to design");
        for (int i = 0; i < Designs.Length; i++)
        {
            int captured = i;
            _designsMenu.DropDownItems.Add(Designs[i].ToString(), null, (_, _) => Show(captured));
        }
        menu.Items.Add(_designsMenu);

        ToolStripMenuItem corners = new("Corner treatment");
        foreach (FrameStyle style in FrameStyle.All)
        {
            FrameStyle captured = style;
            corners.DropDownItems.Add(style.Name, null, (_, _) =>
            {
                PrototypeGlyph.Style = captured;
                Show(_index);
            });
        }
        menu.Items.Add(corners);

        ToolStripMenuItem centring = new("Centring");
        foreach (Centring mode in Enum.GetValues<Centring>())
        {
            Centring captured = mode;
            centring.DropDownItems.Add(mode.ToString(), null, (_, _) =>
            {
                PrototypeGlyph.Centre = captured;
                Show(_index);
            });
        }
        menu.Items.Add(centring);

        ToolStripMenuItem weeks = new("Week shown");
        weeks.DropDownItems.Add("1  (worst case for centring)", null, (_, _) => SetWeek(1, false));
        weeks.DropDownItems.Add("11 (narrowest pair)", null, (_, _) => SetWeek(11, false));
        weeks.DropDownItems.Add("14", null, (_, _) => SetWeek(14, false));
        weeks.DropDownItems.Add("32", null, (_, _) => SetWeek(32, false));
        weeks.DropDownItems.Add("44 (widest label)", null, (_, _) => SetWeek(44, false));
        weeks.DropDownItems.Add("01 (padded, for comparison)", null, (_, _) => SetWeek(1, true));
        menu.Items.Add(weeks);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());

        _notifyIcon = new NotifyIcon { ContextMenuStrip = menu, Visible = true };

        // Left-click cycles, so flipping through designs doesn't need the menu at all.
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) Move(+1);
        };

        Show(0);
    }

    private void SetWeek(int week, bool padded)
    {
        _week = week;
        _padded = padded;
        Show(_index);
    }

    private void Move(int delta) => Show((_index + delta + Designs.Length) % Designs.Length);

    private void Show(int index)
    {
        _index = index;
        Design design = Designs[index];
        int box = SystemInformation.SmallIconSize.Width;

        using Bitmap bitmap = PrototypeGlyph.Render(design, box, GlyphColour(), _week, _padded);

        nint handle = bitmap.GetHicon();
        Icon? previous = _notifyIcon.Icon;
        nint previousHandle = _iconHandle;

        _notifyIcon.Icon = Icon.FromHandle(handle);
        _iconHandle = handle;
        previous?.Dispose();
        if (previousHandle != 0) NativeMethods.DestroyIcon(previousHandle);

        // NotifyIcon.Text caps at 63 chars, so this is deliberately terse.
        _notifyIcon.Text = $"{design} KW{_week} {box}px | {PrototypeGlyph.Centre} | "
            + $"{PrototypeGlyph.Style.Name.Split("  ")[^1].Trim('(', ')')}";
    }

    /// <summary>
    /// A light taskbar wants a dark glyph and vice versa. 07 decides how this is watched for
    /// changes; the prototype only needs it read once so the glyph is judged in the right colour.
    /// </summary>
    private static Color GlyphColour()
    {
        object? value = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme", 0);
        bool lightTaskbar = value is int i && i != 0;
        return lightTaskbar ? Color.FromArgb(26, 26, 26) : Color.White;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
            _designsMenu.Dispose();
            if (_iconHandle != 0)
            {
                NativeMethods.DestroyIcon(_iconHandle);
                _iconHandle = 0;
            }
        }

        base.Dispose(disposing);
    }
}

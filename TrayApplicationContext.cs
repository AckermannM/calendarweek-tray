using System.Globalization;

namespace CalendarWeekTray;

/// <summary>
/// The applet has no window: an <see cref="ApplicationContext"/> owning a <see cref="NotifyIcon"/>
/// is the whole shape (01, Q2).
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private nint _iconHandle;

    public TrayApplicationContext()
    {
        ContextMenuStrip menu = new();
        menu.Items.Add("Quit", image: null, (_, _) => ExitThread());

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Text = "calendarweek-tray",
            Visible = true,
        };

        SetGlyph(RenderPlaceholder());
    }

    /// <summary>
    /// Assigns a freshly rendered bitmap as the tray icon and destroys the HICON it replaces.
    /// The real pipeline — measurement, hinting, alpha, trigger set — is ticket 07's job.
    /// </summary>
    private void SetGlyph(Bitmap bitmap)
    {
        using (bitmap)
        {
            nint handle = bitmap.GetHicon();
            Icon? previous = _notifyIcon.Icon;
            nint previousHandle = _iconHandle;

            _notifyIcon.Icon = Icon.FromHandle(handle);
            _iconHandle = handle;

            previous?.Dispose();
            if (previousHandle != 0)
            {
                NativeMethods.DestroyIcon(previousHandle);
            }
        }
    }

    /// <summary>
    /// A deliberately crude glyph, present only so there is something in the tray to hang 06's
    /// prototype off. It settles nothing about prefix, layout, font or padding.
    /// </summary>
    private static Bitmap RenderPlaceholder()
    {
        Size size = SystemInformation.SmallIconSize;
        Bitmap bitmap = new(size.Width, size.Height);

        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);

        string week = ISOWeek.GetWeekOfYear(DateTime.Now).ToString("00", CultureInfo.InvariantCulture);
        using Font font = new("Segoe UI", size.Height * 0.62f, GraphicsUnit.Pixel);
        using StringFormat format = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        graphics.DrawString(week, font, Brushes.White, new RectangleF(Point.Empty, size), format);
        return bitmap;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Without this the shell keeps drawing the icon until the user hovers over it.
            _notifyIcon.Visible = false;
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();

            if (_iconHandle != 0)
            {
                NativeMethods.DestroyIcon(_iconHandle);
                _iconHandle = 0;
            }
        }

        base.Dispose(disposing);
    }
}

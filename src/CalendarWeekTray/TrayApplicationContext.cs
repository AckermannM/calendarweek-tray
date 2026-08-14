using Microsoft.Win32;

namespace CalendarWeekTray;

/// <summary>
/// The applet has no window: an <see cref="ApplicationContext"/> owning a <see cref="NotifyIcon"/>
/// is the whole shape (01, Q2).
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string PersonalizeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string PersonalizeValue = "SystemUsesLightTheme";

    private readonly NotifyIcon notifyIcon;
    private nint iconHandle;

    public TrayApplicationContext()
    {
        ContextMenuStrip menu = new();
        menu.Items.Add("Quit", image: null, (_, _) => this.ExitThread());

        this.notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
        };

        // Config-fault surfacing (spec §9) belongs to the Reconcile() pipeline, not this ticket —
        // this render passes no fault through, even if config.json failed to parse.
        ConfigLoadResult configResult = ConfigLoader.Load();
        DesiredState state = TrayState.Compute(
            now: DateTime.Now,
            sizePx: SystemInformation.SmallIconSize.Width,
            highContrast: SystemInformation.HighContrast,
            systemUsesLightTheme: ReadSystemUsesLightTheme(),
            config: configResult.Config,
            configError: null);

        this.notifyIcon.Text = state.Tooltip;
        this.SetGlyph(GlyphRenderer.Render(new GlyphSpec(state.Week, state.SizePx, state.Ink)));

        // Setting Visible before an icon exists shows a blank frame (spec §8.1).
        this.notifyIcon.Visible = true;
    }

    /// <summary>Spec §5.5's <c>lightTaskbar</c> registry read for the <c>auto</c> theme. Null means
    /// the value is absent, which <see cref="TrayState"/> treats as light.</summary>
    private static bool? ReadSystemUsesLightTheme() =>
        Registry.GetValue(PersonalizeKey, PersonalizeValue, defaultValue: null) is int value ? value != 0 : null;

    /// <summary>
    /// Assigns a freshly rendered bitmap as the tray icon and destroys the HICON it replaces.
    /// The real pipeline — measurement, hinting, alpha, trigger set — is ticket 07's job.
    /// </summary>
    private void SetGlyph(Bitmap bitmap)
    {
        using (bitmap)
        {
            nint handle = bitmap.GetHicon();
            Icon? previous = this.notifyIcon.Icon;
            nint previousHandle = this.iconHandle;

            this.notifyIcon.Icon = Icon.FromHandle(handle);
            this.iconHandle = handle;

            previous?.Dispose();
            if (previousHandle != 0)
            {
                NativeMethods.DestroyIcon(previousHandle);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Without this the shell keeps drawing the icon until the user hovers over it.
            this.notifyIcon.Visible = false;
            this.notifyIcon.Icon?.Dispose();
            this.notifyIcon.Dispose();

            if (this.iconHandle != 0)
            {
                NativeMethods.DestroyIcon(this.iconHandle);
                this.iconHandle = 0;
            }
        }

        base.Dispose(disposing);
    }
}

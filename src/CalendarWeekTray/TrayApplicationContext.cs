using Microsoft.Win32;

namespace CalendarWeekTray;

/// <summary>
/// The applet has no window: an <see cref="ApplicationContext"/> owning a <see cref="NotifyIcon"/>
/// is the whole shape (01, Q2). This is the only stateful type — the <see cref="NotifyIcon"/>, the
/// reconcile timer, the <see cref="SystemEvents"/> subscriptions, and the last-applied
/// <see cref="DesiredState"/> all live here (spec §5.1).
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string PersonalizeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string PersonalizeValue = "SystemUsesLightTheme";

    private readonly NotifyIcon notifyIcon;
    private readonly System.Windows.Forms.Timer timer;

    // Reload (ticket 07) mutates this; ticket 06 loads it once and never touches it again.
    private readonly AppConfig config;

    private GlyphIcon? glyphIcon;
    private DesiredState? lastApplied;
    private SynchronizationContext? uiContext;
    private bool timerCalibrated;
    private bool renderFaultBalloonShown;

    public TrayApplicationContext()
    {
        ContextMenuStrip menu = new();
        menu.Items.Add("Quit", image: null, (_, _) => this.ExitThread());

        this.notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
        };

        // Config-fault surfacing (spec §9) belongs to Reconcile()'s configError argument, but
        // turning a ConfigFault into a displayed diagnostic is ticket 07's job — Reconcile() below
        // always passes configError: null, even though configResult.Fault may be sitting unread.
        ConfigLoadResult configResult = ConfigLoader.Load();
        this.config = configResult.Config;

        // Step 4 of spec §8.1: render and assign the first icon, then make it visible. This goes
        // through Reconcile() rather than a separate direct render — "nothing re-renders directly"
        // (§6.2) applies to the very first render too, not just the ones that follow. Setting
        // Visible before an icon exists shows a blank frame, so it comes after.
        this.Reconcile();
        this.notifyIcon.Visible = true;

        // Step 5 of §8.1. Interval starts at 1 because of the §6.4 SynchronizationContext trap:
        // SynchronizationContext.Current is a plain thread-pool context here, inside the
        // constructor — it only becomes a WindowsFormsSynchronizationContext once Application.Run
        // pumps. Starting at 1 ms defers the capture (and the SystemEvents subscriptions that
        // depend on it) to the first tick, which cannot fire before the pump exists.
        this.timer = new() { Interval = 1 };
        this.timer.Tick += this.OnTimerTick;
        this.timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!this.timerCalibrated)
        {
            this.timerCalibrated = true;

            if (SynchronizationContext.Current is not WindowsFormsSynchronizationContext context)
            {
                // Fail loudly: capturing the wrong context here would silently marshal every future
                // reconcile onto a pool thread and touch NotifyIcon cross-thread — a failure that is
                // rare, mysterious, and survives casual testing (spec §6.4).
                throw new InvalidOperationException(
                    "SynchronizationContext.Current was not a WindowsFormsSynchronizationContext on " +
                    "the reconcile timer's first tick — Application.Run must be pumping messages by now.");
            }

            this.uiContext = context;

            // TaskbarCreated is deliberately not handled here (spec §6.3): NotifyIcon re-adds its
            // own icon on an Explorer restart, unaided, and a message-only window to catch it would
            // add a real window to a process whose whole shape is not having one.
            SystemEvents.UserPreferenceChanged += this.OnUserPreferenceChanged;
            SystemEvents.DisplaySettingsChanged += this.OnSystemEvent;
            SystemEvents.TimeChanged += this.OnSystemEvent;
            SystemEvents.PowerModeChanged += this.OnPowerModeChanged;

            this.timer.Interval = 60000;
        }

        this.Reconcile();
    }

    // --- triggers (spec §6.3) — every one of these ends in a Reconcile() on the UI thread --------

    /// <summary>Fires on a background thread with the uninformative <c>category=General</c>;
    /// filtering by category is pointless, so every preference change reconciles (spec §6.3).</summary>
    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e) => this.PostReconcile();

    private void OnSystemEvent(object? sender, EventArgs e) => this.PostReconcile();

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            this.PostReconcile();
        }
    }

    /// <summary><see cref="SystemEvents"/> handlers fire on a background thread; marshal onto the UI
    /// thread before touching <see cref="NotifyIcon"/> (spec §6.3). Subscriptions are only added
    /// once <see cref="uiContext"/> is set, so it is always non-null here.</summary>
    private void PostReconcile() => this.uiContext!.Post(_ => this.Reconcile(), state: null);

    // --- Reconcile (spec §6.2) ---------------------------------------------------------------

    /// <summary>
    /// The one code path every trigger calls; nothing re-renders directly. Compares the rendered
    /// result against the last applied state and returns early when they match, so a trigger that
    /// changed nothing observable correctly does nothing — no generation counter, no dirty flags.
    /// The whole body is wrapped in one catch: a failure keeps the last good icon, marks the
    /// tooltip, and shows one balloon the first time only. A timer tick must never take the process
    /// down.
    /// </summary>
    private void Reconcile()
    {
        DesiredState? desired = null;
        try
        {
            desired = TrayState.Compute(
                now: DateTime.Now,
                sizePx: SystemInformation.SmallIconSize.Width,
                highContrast: SystemInformation.HighContrast,
                systemUsesLightTheme: ReadSystemUsesLightTheme(),
                config: this.config,
                configError: null);

            if (desired == this.lastApplied)
            {
                return;
            }

            this.SetGlyph(GlyphRenderer.Render(new GlyphSpec(desired.Value.Week, desired.Value.SizePx, desired.Value.Ink)));
            this.notifyIcon.Text = desired.Value.Tooltip;
            this.lastApplied = desired;
        }
        catch (Exception)
        {
            try
            {
                this.HandleReconcileFailure(desired?.Tooltip);
            }
            catch (Exception)
            {
                // The failure-handling path touches NotifyIcon too, and a posted reconcile can lose
                // a race against shutdown's Dispose() — it must not be able to take the process down
                // either, for exactly the reason the outer catch exists.
            }
        }
    }

    private void HandleReconcileFailure(string? freshTooltip)
    {
        Language language = ConfigLoader.ResolveLanguage(this.config.Language);
        string fault = Strings.RenderFault(language);
        string? baseTooltip = freshTooltip ?? this.lastApplied?.Tooltip;

        this.notifyIcon.Text = Strings.AppendFault(baseTooltip, fault);

        // Guarded on Visible too: the very first Reconcile() runs before Visible is set (§8.1), and
        // a balloon on a not-yet-visible NotifyIcon is unreliable. Leaving the flag unset in that
        // case means a later failure still gets its one balloon once the icon is actually showing.
        if (!this.renderFaultBalloonShown && this.notifyIcon.Visible)
        {
            this.renderFaultBalloonShown = true;
            this.notifyIcon.ShowBalloonTip(0, Strings.BalloonTitle, Strings.BalloonBody(fault), ToolTipIcon.Warning);
        }
    }

    /// <summary>Assigns the new icon to <see cref="NotifyIcon"/> before disposing the previous
    /// <see cref="GlyphIcon"/> (spec §6.5) — never the other way round.</summary>
    private void SetGlyph(Bitmap bitmap)
    {
        GlyphIcon nextIcon = GlyphIcon.FromBitmap(bitmap);
        try
        {
            GlyphIcon? previousIcon = this.glyphIcon;

            this.notifyIcon.Icon = nextIcon.Icon;
            this.glyphIcon = nextIcon;

            previousIcon?.Dispose();
        }
        catch
        {
            // nextIcon never made it into this.glyphIcon, so nothing else will ever dispose its
            // HICON — exactly the leak GlyphIcon exists to rule out (spec §6.5).
            nextIcon.Dispose();
            throw;
        }
    }

    /// <summary>Spec §5.5's <c>lightTaskbar</c> registry read for the <c>auto</c> theme. Null means
    /// the value is absent, which <see cref="TrayState"/> treats as light.</summary>
    private static bool? ReadSystemUsesLightTheme() =>
        Registry.GetValue(PersonalizeKey, PersonalizeValue, defaultValue: null) is int value ? value != 0 : null;

    // --- shutdown (spec §8.3), strictly in order ----------------------------------------------

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Without this the shell keeps drawing the icon until the user hovers over it.
            this.notifyIcon.Visible = false;

            // SystemEvents handlers are static events: a subscription outlives this object and can
            // fire during shutdown unless it is explicitly removed here.
            SystemEvents.UserPreferenceChanged -= this.OnUserPreferenceChanged;
            SystemEvents.DisplaySettingsChanged -= this.OnSystemEvent;
            SystemEvents.TimeChanged -= this.OnSystemEvent;
            SystemEvents.PowerModeChanged -= this.OnPowerModeChanged;

            this.timer.Stop();
            this.timer.Dispose();

            this.glyphIcon?.Dispose();
            this.notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}

namespace CalendarWeekTray;

/// <summary>
/// Owns an <see cref="System.Drawing.Icon"/> and the HICON it was built from as one unit (spec
/// §6.5). <see cref="Icon.FromHandle"/> does not own the handle it wraps, and each
/// <c>Bitmap.GetHicon()</c> call costs 3 GDI + 1 USER object that the GC never reclaims — no
/// <see cref="Dispose"/>, no finalizer, no full collect. Pairing the handle with the managed
/// <see cref="Icon"/> in one disposable type is what keeps that discipline from rotting into a raw
/// <see cref="nint"/> field shadowing it.
/// </summary>
internal sealed class GlyphIcon : IDisposable
{
    private readonly nint handle;
    private bool disposed;

    private GlyphIcon(Icon icon, nint handle)
    {
        this.Icon = icon;
        this.handle = handle;
    }

    internal Icon Icon { get; }

    /// <summary>Builds a <see cref="GlyphIcon"/> from a freshly rendered glyph bitmap, taking
    /// ownership of it — the bitmap is disposed here, once its HICON copy has been taken.</summary>
    internal static GlyphIcon FromBitmap(Bitmap bitmap)
    {
        using (bitmap)
        {
            nint handle = bitmap.GetHicon();
            return new GlyphIcon(Icon.FromHandle(handle), handle);
        }
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.Icon.Dispose();
        NativeMethods.DestroyIcon(this.handle);
    }
}

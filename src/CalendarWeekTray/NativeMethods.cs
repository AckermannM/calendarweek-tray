using System.Runtime.InteropServices;

namespace CalendarWeekTray;

internal static partial class NativeMethods
{
    /// <summary>
    /// <see cref="Icon.FromHandle"/> does not own the HICON it wraps, so every icon built from a
    /// <see cref="Bitmap"/> leaks a GDI handle unless this is called on it. Ownership discipline is
    /// decided properly in ticket 07; this declaration exists so the scaffold does not leak.
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(nint hIcon);
}

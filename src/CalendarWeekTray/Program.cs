namespace CalendarWeekTray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using Mutex mutex = new(initiallyOwned: true, @"Local\CalendarWeekTray", out bool acquired);
        if (!acquired)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());

        GC.KeepAlive(mutex);
    }
}

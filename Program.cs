namespace CalendarWeekTray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // PROTOTYPE — ticket 06. Both branches are throwaway and leave with the prototype.
        switch (args.FirstOrDefault())
        {
            case "sheet":
                Prototype06Sheet.WriteAll(Path.Combine(
                    Directory.GetCurrentDirectory(), ".scratch", "calendarweek-tray-v1", "prototype-06"));
                return;

            case "debug":
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), ".scratch",
                    "calendarweek-tray-v1", "prototype-06", "fit-debug.txt"), PrototypeGlyph.DumpFits());
                return;

            case "lab":
                Application.Run(new Prototype06Lab());
                return;

            default:
                Application.Run(new TrayApplicationContext());
                return;
        }
    }
}

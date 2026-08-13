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

            // PROTOTYPE — ticket 12.
            case "sheet12":
                Prototype12Sheet.WriteAll(Path.Combine(
                    Directory.GetCurrentDirectory(), ".scratch", "calendarweek-tray-v1", "prototype-12"));
                return;

            // PROTOTYPE — ticket 13. Accessibility is live system state, not something a contact
            // sheet can hold: the lab sits in the tray while the user flips settings, and logs.
            case "lab13":
                Application.Run(new Prototype13Lab());
                return;

            case "probe13":
                Prototype13Probe.WriteOnce(Path.Combine(
                    Directory.GetCurrentDirectory(), ".scratch", "calendarweek-tray-v1", "prototype-13"));
                return;

            case "sheet13":
                Prototype13Sheet.WriteAll(Path.Combine(
                    Directory.GetCurrentDirectory(), ".scratch", "calendarweek-tray-v1", "prototype-13"));
                return;

            // Pins the two load-bearing claims exactly, rather than by inference from the matrix.
            case "verify13":
                Prototype13Verify.Run(Path.Combine(
                    Directory.GetCurrentDirectory(), ".scratch", "calendarweek-tray-v1", "prototype-13"));
                return;

            // Walks the whole accessibility matrix unattended and restores every setting it touched.
            case "experiment13":
                Prototype13Experiment.Run(Path.Combine(
                    Directory.GetCurrentDirectory(), ".scratch", "calendarweek-tray-v1", "prototype-13"));
                return;

            default:
                Application.Run(new TrayApplicationContext());
                return;
        }
    }
}

using System.Globalization;

namespace HexGrid.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Set explicitly rather than via the generated ApplicationConfiguration.Initialize(),
        // so the entry point does not depend on the source generator running.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(defaultValue: false);

        // Anything that escapes gets shown and written to a log next to the exe, rather than
        // vanishing behind the generic Windows crash dialog.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Report(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception);

        try
        {
            using var form = new MainForm();
            Application.Run(form);
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }

    internal static void Report(Exception? ex)
    {
        if (ex is null)
        {
            return;
        }

        string detail = WriteCrashLog(ex);

        MessageBox.Show(
            detail,
            "HexGrid Generator - unhandled error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    /// <summary>
    /// Writes the exception to its own timestamped file under a "logs" folder next to the exe, and
    /// returns the formatted detail text. Used both for truly unhandled exceptions (<see cref="Report"/>)
    /// and for recoverable failures elsewhere in the app that only surface <c>ex.Message</c> in the UI.
    /// The full exception still needs to land somewhere diagnosable. One file per incident rather than
    /// one shared file, so an earlier crash isn't overwritten by a later, unrelated one.
    /// </summary>
    internal static string WriteCrashLog(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        // SS002 wants a locale/timezone-independent value. DateTime.Now on its own is ambiguous
        // during a DST fall-back (the same wall-clock time can occur twice) and doesn't record which
        // offset applied. UtcNow would remove the ambiguity but force whoever reads this local crash
        // log to mentally convert back to their own time. DateTimeOffset.Now keeps the human-readable
        // local time while also recording the UTC offset, so the timestamp is unambiguous either way.
        DateTimeOffset now = DateTimeOffset.Now;
        string detail = string.Create(CultureInfo.InvariantCulture, $"{now:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}{ex}");

        try
        {
            string logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);

            // Milliseconds in the name, not just the second, so two failures in the same message-loop
            // tick (e.g. ThreadException and a chained UnhandledException) don't collide.
            string fileName = string.Create(CultureInfo.InvariantCulture, $"crash-{now:yyyy-MM-dd_HHmmss-fff}.log");
            File.WriteAllText(Path.Combine(logDir, fileName), detail);
        }
        catch (Exception logEx) when (logEx is IOException or UnauthorizedAccessException)
        {
            // A log we cannot write must not become a second failure.
        }

        return detail;
    }
}

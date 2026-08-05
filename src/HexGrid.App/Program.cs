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
        Application.SetCompatibleTextRenderingDefault(false);

        // Anything that escapes gets shown and written to a log next to the exe, rather than
        // vanishing behind the generic Windows crash dialog.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Report(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception);

        try
        {
            Application.Run(new MainForm());
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

        string detail = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{ex}";

        try
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "hexgrid-crash.log"), detail);
        }
        catch (IOException)
        {
            // A log we cannot write must not become a second failure.
        }
        catch (UnauthorizedAccessException)
        {
        }

        MessageBox.Show(
            detail,
            "HexGrid Generator - unhandled error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}

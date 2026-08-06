using System.Runtime.ExceptionServices;

namespace HexGrid.App.Tests;

/// <summary>
/// Runs a test body on a dedicated STA thread. WinForms controls are only supported on STA (window
/// handle creation, drag/drop and clipboard all assume it), but xUnit's own worker threads are MTA —
/// any test that touches a real Control has to hop onto its own thread rather than adding a test
/// framework package just for STA support.
/// </summary>
internal static class StaThread
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public static void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        // A bare Join() has no bound - a test body that never returns (an accidental blocking wait,
        // a ShowDialog() call) would hang this thread, the test run and the CI job forever with no
        // diagnosable failure. Timing out turns that into a loud, attributable test failure instead.
        if (!thread.Join(Timeout))
        {
            throw new TimeoutException($"StaThread test body did not complete within {Timeout}.");
        }

        failure?.Throw();
    }
}

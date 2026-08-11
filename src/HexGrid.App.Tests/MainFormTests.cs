using System.Reflection;

namespace HexGrid.App.Tests;

public class MainFormTests
{
    [Fact]
    public void Show_Close_DisposesCleanlyWithoutThrowing() => StaThread.Run(() =>
    {
        // Arrange
        using MainForm form = NewOffscreenForm();

        // Act
        Exception? exception = Record.Exception(() =>
        {
            ShowAndPump(form);
            form.Close();
        });

        // Assert
        Assert.Null(exception);
    });

    [Fact]
    public void Show_AppliesThePreferredSplitterDistance() => StaThread.Run(() =>
    {
        // Arrange: regression guard for the SplitContainer construction-order crash BuildSplit() and
        // TrySetPreferredSplit document in MainForm.cs (SplitterDistance was computed before the
        // control was parented/sized). TrySetPreferredSplit's own catch swallows the exception silently by design
        // (a too-narrow window must never crash the app), so a regression there would NOT surface as
        // a thrown exception - only as the distance silently never landing, which is what this checks.
        using MainForm form = NewOffscreenForm();

        // Act
        ShowAndPump(form);

        // Assert
        SplitContainer split = FindDescendant<SplitContainer>(form)
            ?? throw new InvalidOperationException("SplitContainer not found in MainForm's control tree.");
        Assert.Equal(430, split.SplitterDistance);
    });

    [Fact]
    public void Show_DefaultSettings_RendersPreviewWithoutError() => StaThread.Run(() =>
    {
        // Arrange: exercises the full app-level wiring (default GridSettings → HexLayoutEngine →
        // SceneBuilder → SceneRasterizer → PreviewPanel) the same way GenerationPipelineTests
        // exercises the Core-only pipeline - a broken default here would route to the preview's error
        // message instead of an image, which is what Rebuild()'s catch block does on failure.
        using MainForm form = NewOffscreenForm();

        // Act
        ShowAndPump(form);

        // Assert
        var preview = (PreviewPanel)GetInstanceField(form, "_preview")!;
        var message = (string?)GetInstanceField(preview, "_message");
        Assert.Null(message);
    });

    private static MainForm NewOffscreenForm() => new()
    {
        StartPosition = FormStartPosition.Manual,
        Location = new Point(-3000, -3000),
        ShowInTaskbar = false,
    };

    // Form.Shown is not raised synchronously inside Show() - WinForms posts it for the message loop
    // to deliver on the next idle cycle, so MainForm's own Shown handlers (TrySetPreferredSplit,
    // Rebuild) have not run yet the instant Show() returns. DoEvents() pumps that queued event.
    private static void ShowAndPump(Form form)
    {
        form.Show();
        Application.DoEvents();
    }

    private static T? FindDescendant<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match)
            {
                return match;
            }

            T? nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static object? GetInstanceField(object instance, string name) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance);
}

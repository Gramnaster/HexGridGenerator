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

    [Fact]
    public void AdjustZoom_Notch_ZoomsThePanelWithoutWaitingForARender() => StaThread.Run(() =>
    {
        // Arrange
        using MainForm form = NewOffscreenForm();
        ShowAndPump(form);
        var preview = (PreviewPanel)GetInstanceField(form, "_preview")!;
        double before = preview.Zoom;

        // Act
        RaiseFromMessageLoop(form, () => InvokePrivate(form, "AdjustZoom", 1, Point.Empty));

        // Assert: the frame takes milliseconds to render, far longer than one pass of the message loop,
        // so the zoom cannot have waited for it.
        Assert.Equal(before * 1.25, preview.Zoom, precision: 9);
    });

    [Fact]
    public void AdjustZoom_Notch_RendersAnExactFrameForTheNewViewInTheBackground() => StaThread.Run(() =>
    {
        // Arrange
        using MainForm form = NewOffscreenForm();
        ShowAndPump(form);
        var preview = (PreviewPanel)GetInstanceField(form, "_preview")!;

        // Act
        RaiseFromMessageLoop(form, () => InvokePrivate(form, "AdjustZoom", 1, Point.Empty));
        double scale = PreviewPanel.RenderScaleFor(preview.Zoom);
        bool covered = PumpUntil(() => preview.HasFrameCovering(scale, preview.VisibleRegion(scale, marginPx: 0)));

        // Assert
        Assert.True(covered);
    });

    [Fact]
    public void AdjustZoom_ManyNotches_StopsAt3200PercentOfTheCanvasPixels() => StaThread.Run(() =>
    {
        // Arrange
        using MainForm form = NewOffscreenForm();
        ShowAndPump(form);
        var preview = (PreviewPanel)GetInstanceField(form, "_preview")!;

        // Act
        for (int i = 0; i < 100; i++)
        {
            RaiseFromMessageLoop(form, () => InvokePrivate(form, "AdjustZoom", 1, Point.Empty));
        }

        // Assert
        Assert.Equal(32.0, preview.Zoom, precision: 9);
    });

    [Fact]
    public void SaveInBackgroundAsync_WhileSaving_DisablesFileWritingButtonsAndShowsTheTag() => StaThread.Run(() =>
    {
        // Arrange
        using MainForm form = NewOffscreenForm();
        ShowAndPump(form);
        using var gate = new ManualResetEventSlim();

        // Act
        Task saving = form.SaveInBackgroundAsync("grid.png", () =>
        {
            gate.Wait();
            return ["grid.png"];
        });

        // Assert
        Assert.False(FindButton(form, "Export PNG…").Enabled);
        Assert.False(FindButton(form, "Export SVG…").Enabled);
        Assert.False(FindButton(form, "Export both…").Enabled);
        Assert.False(FindButton(form, "Save preset…").Enabled);
        Assert.True(FindButton(form, "Load preset…").Enabled);
        var tag = (Label)GetInstanceField(form, "_savingTag")!;
        Assert.True(tag.Visible);
        Assert.Contains("grid.png", tag.Text, StringComparison.Ordinal);

        gate.Set();
        PumpUntil(() => saving.IsCompleted);
    });

    [Fact]
    public void SaveInBackgroundAsync_Finished_ReenablesButtonsAndHidesTheTag() => StaThread.Run(() =>
    {
        // Arrange
        using MainForm form = NewOffscreenForm();
        ShowAndPump(form);

        // Act
        Task saving = form.SaveInBackgroundAsync("grid.png", () => ["grid.png"]);
        bool finished = PumpUntil(() => saving.IsCompleted);

        // Assert
        Assert.True(finished);
        Assert.True(FindButton(form, "Export PNG…").Enabled);
        Assert.True(FindButton(form, "Save preset…").Enabled);
        Assert.False(((Label)GetInstanceField(form, "_savingTag")!).Visible);
    });

    [Fact]
    public void SaveInBackgroundAsync_Save_RunsOffTheUiThread() => StaThread.Run(() =>
    {
        // Arrange
        using MainForm form = NewOffscreenForm();
        ShowAndPump(form);
        int uiThread = Environment.CurrentManagedThreadId;
        int saveThread = uiThread;

        // Act
        Task saving = form.SaveInBackgroundAsync("grid.png", () =>
        {
            saveThread = Environment.CurrentManagedThreadId;
            return ["grid.png"];
        });
        PumpUntil(() => saving.IsCompleted);

        // Assert
        Assert.NotEqual(uiThread, saveThread);
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

    // Wheel and menu events reach MainForm from inside the message loop, where WinForms keeps its
    // SynchronizationContext installed, so the preview render loop resumes on the UI thread. Called
    // straight from the test body a handler would run outside that loop, so it is posted to the loop
    // instead, the way a real event arrives.
    private static void RaiseFromMessageLoop(Control control, Action handler)
    {
        control.BeginInvoke(handler);
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

    /// <summary>Pumps the message loop until <paramref name="condition"/> holds or five seconds pass.</summary>
    private static bool PumpUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                return false;
            }

            Application.DoEvents();
            Thread.Sleep(10);
        }

        return true;
    }

    private static Button FindButton(Control root, string text) =>
        FindButtonOrNull(root, text) ?? throw new InvalidOperationException($"Button '{text}' not found.");

    private static Button? FindButtonOrNull(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button button && string.Equals(button.Text, text, StringComparison.Ordinal))
            {
                return button;
            }

            if (FindButtonOrNull(child, text) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static void InvokePrivate(object instance, string name, params object[] args) =>
        instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, args);

    private static object? GetInstanceField(object instance, string name) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance);
}

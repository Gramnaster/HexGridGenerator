using System.Reflection;

namespace HexGrid.App.Tests;

public class PreviewPanelTests
{
    [Fact]
    public void SetView_FractionalCanvas_SetsAutoScrollMinSizeToTheCanvasAtThatZoomRoundedUp() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();

        // Act: 21 x 11 at half size is 10.5 x 5.5, and the last partial pixel must stay reachable.
        panel.SetView(21, 11, zoom: 0.5);

        // Assert
        Assert.Equal(new Size(11, 6), panel.AutoScrollMinSize);
    });

    [Fact]
    public void SetView_ZoomAtAnchor_KeepsTheCanvasPointUnderTheAnchor() => StaThread.Run(() =>
    {
        // Arrange: scrolled to (300, 400) at 100%, the anchor (50, 40) sits over canvas point (350, 440).
        using PreviewPanel panel = NewCreatedPanel();
        panel.SetView(1000, 1000, zoom: 1.0);
        panel.AutoScrollPosition = new Point(300, 400);

        // Act
        panel.SetView(1000, 1000, zoom: 2.0, anchor: new Point(50, 40));

        // Assert: at 200% that point is at (700, 880), so the scroll must be the anchor's offset less.
        Assert.Equal(new Point(-650, -840), panel.AutoScrollPosition);
    });

    [Fact]
    public void VisibleRegion_PastTheCanvasResolution_IsInCanvasPixels() => StaThread.Run(() =>
    {
        // Arrange: at 400% the panel shows a quarter of its own size in canvas pixels.
        using PreviewPanel panel = NewCreatedPanel();
        panel.SetView(1000, 1000, zoom: 4.0);
        panel.AutoScrollPosition = new Point(400, 800);

        // Act
        Rectangle region = panel.VisibleRegion(panel.RenderScale, marginPx: 0);

        // Assert
        Size client = panel.ClientSize;
        Assert.Equal(
            Rectangle.FromLTRB(100, 200, (int)Math.Ceiling((400 + client.Width) / 4.0), (int)Math.Ceiling((800 + client.Height) / 4.0)),
            region);
    });

    [Fact]
    public void RenderScale_PastTheCanvasResolution_IsTheCanvasResolution() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();

        // Act
        panel.SetView(1000, 1000, zoom: 4.0);

        // Assert
        Assert.Equal(1.0, panel.RenderScale);
    });

    [Fact]
    public void RenderScale_SmoothZoomPastTheCanvasResolution_IsTheZoom() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();
        panel.SetView(1000, 1000, zoom: 4.0);

        // Act
        panel.SmoothZoom = true;

        // Assert
        Assert.Equal(4.0, panel.RenderScale);
    });

    [Fact]
    public void VisibleRegion_MarginBeyondTheCanvas_IsCutToTheCanvas() => StaThread.Run(() =>
    {
        // Arrange: a canvas smaller than the panel, so all of it is in view with room around it.
        using PreviewPanel panel = NewCreatedPanel();
        panel.SetView(100, 50, zoom: 1.0);

        // Act
        Rectangle region = panel.VisibleRegion(renderScale: 1.0, marginPx: 128);

        // Assert
        Assert.Equal(new Rectangle(0, 0, 100, 50), region);
    });

    [Fact]
    public void HasFrameCovering_FrameAtThatScaleContainingTheRegion_IsTrue() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();
        panel.SetFrame(new Bitmap(40, 40), renderScale: 0.5, new Rectangle(10, 10, 40, 40));

        // Act
        bool covered = panel.HasFrameCovering(0.5, new Rectangle(20, 20, 10, 10));

        // Assert
        Assert.True(covered);
    });

    [Fact]
    public void HasFrameCovering_AfterMarkFrameStale_IsFalse() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();
        panel.SetFrame(new Bitmap(40, 40), renderScale: 0.5, new Rectangle(10, 10, 40, 40));

        // Act
        panel.MarkFrameStale();

        // Assert
        Assert.False(panel.HasFrameCovering(0.5, new Rectangle(20, 20, 10, 10)));
    });

    [Fact]
    public void SetFrame_ReplacingFrame_DisposesThePreviousOne() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();
        var first = new Bitmap(4, 4);
        var second = new Bitmap(4, 4);

        // Act
        panel.SetFrame(first, renderScale: 1.0, new Rectangle(0, 0, 4, 4));
        panel.SetFrame(second, renderScale: 1.0, new Rectangle(0, 0, 4, 4));

        // Assert: a disposed Bitmap throws ArgumentException from any property access - there is no
        // public IsDisposed flag to check directly.
        Assert.Throws<ArgumentException>(() => _ = first.Width);
    });

    [Fact]
    public void SetFrame_SameReferenceTwice_DoesNotDisposeIt() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();
        var frame = new Bitmap(4, 4);
        panel.SetFrame(frame, renderScale: 1.0, new Rectangle(0, 0, 4, 4));

        // Act
        panel.SetFrame(frame, renderScale: 1.0, new Rectangle(0, 0, 4, 4));

        // Assert
        Assert.Equal(4, frame.Width);
    });

    [Fact]
    public void ClearFrame_DisposesTheFrame() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();
        var frame = new Bitmap(4, 4);
        panel.SetFrame(frame, renderScale: 1.0, new Rectangle(0, 0, 4, 4));

        // Act
        panel.ClearFrame();

        // Assert
        Assert.Throws<ArgumentException>(() => _ = frame.Width);
    });

    [Fact]
    public void Dispose_DisposesTheCurrentFrame()
    {
        // Arrange
        Bitmap? frame = null;

        // Act
        StaThread.Run(() =>
        {
            var panel = new PreviewPanel();
            frame = new Bitmap(4, 4);
            panel.SetFrame(frame, renderScale: 1.0, new Rectangle(0, 0, 4, 4));
            panel.Dispose();
        });

        // Assert
        Assert.Throws<ArgumentException>(() => _ = frame!.Width);
    }

    [Theory]
    [InlineData(120, 1)]
    [InlineData(-120, -1)]
    public void MouseWheel_Notch_RaisesZoomRequestedWithSignOfDeltaAtTheCursor(int delta, int expectedDirection) =>
        StaThread.Run(() =>
        {
            // Arrange
            using var panel = new PreviewPanel();
            PreviewPanel.ZoomRequestedEventArgs? received = null;
            panel.ZoomRequested += (_, e) => received = e;

            // Act
            InvokeProtected(panel, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 30, 20, delta));

            // Assert
            Assert.NotNull(received);
            Assert.Equal(expectedDirection, received!.Direction);
            Assert.Equal(new Point(30, 20), received.Location);
        });

    [Fact]
    public void MiddleButtonDown_EntersPanModeAndCapturesTheMouse() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();
        panel.CreateControl();

        // Act
        InvokeProtected(panel, "OnMouseDown", new MouseEventArgs(MouseButtons.Middle, 1, 10, 10, 0));

        // Assert
        Assert.True(panel.Capture);
        Assert.Equal(Cursors.Hand, panel.Cursor);
    });

    [Fact]
    public void LeftButtonDown_DoesNotEnterPanMode() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();
        panel.CreateControl();

        // Act
        InvokeProtected(panel, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));

        // Assert
        Assert.False(panel.Capture);
    });

    [Fact]
    public void MiddleButtonUp_EndsPanModeAndReleasesCapture() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();
        panel.CreateControl();
        InvokeProtected(panel, "OnMouseDown", new MouseEventArgs(MouseButtons.Middle, 1, 10, 10, 0));

        // Act
        InvokeProtected(panel, "OnMouseUp", new MouseEventArgs(MouseButtons.Middle, 1, 10, 10, 0));

        // Assert
        Assert.False(panel.Capture);
        Assert.Equal(Cursors.Default, panel.Cursor);
    });

    [Fact]
    public void MouseCaptureChanged_WithoutMouseUp_EndsPanMode() => StaThread.Run(() =>
    {
        // Arrange: simulates capture being lost mid-drag (e.g. alt-tab) without a MouseUp ever firing.
        using var panel = new PreviewPanel();
        panel.CreateControl();
        InvokeProtected(panel, "OnMouseDown", new MouseEventArgs(MouseButtons.Middle, 1, 10, 10, 0));

        // Act
        InvokeProtected(panel, "OnMouseCaptureChanged", EventArgs.Empty);

        // Assert
        Assert.Equal(Cursors.Default, panel.Cursor);
    });

    private static PreviewPanel NewCreatedPanel()
    {
        // Scrolling needs a window handle, and a fixed size makes the client area predictable.
        var panel = new PreviewPanel { Size = new Size(200, 100) };
        panel.CreateControl();
        return panel;
    }

    private static void InvokeProtected(Control control, string methodName, object arg)
    {
        MethodInfo method = control.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(control.GetType().Name, methodName);
        method.Invoke(control, [arg]);
    }
}

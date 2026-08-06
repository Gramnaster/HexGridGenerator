using System.Reflection;

namespace HexGrid.App.Tests;

public class PreviewPanelTests
{
    [Fact]
    public void SetImage_WithBitmap_SetsAutoScrollMinSizeToImageSize() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();

        // Act
        panel.SetImage(new Bitmap(30, 20));

        // Assert
        Assert.Equal(new Size(30, 20), panel.AutoScrollMinSize);
    });

    [Fact]
    public void SetImage_Null_ResetsAutoScrollMinSizeToEmpty() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();
        panel.SetImage(new Bitmap(30, 20));

        // Act
        panel.SetImage(image: null);

        // Assert
        Assert.Equal(Size.Empty, panel.AutoScrollMinSize);
    });

    [Fact]
    public void SetImage_ReplacingImage_DisposesThePreviousOne() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();
        var first = new Bitmap(4, 4);
        var second = new Bitmap(4, 4);

        // Act
        panel.SetImage(first);
        panel.SetImage(second);

        // Assert: a disposed Bitmap throws ArgumentException from any property access - there is no
        // public IsDisposed flag to check directly.
        Assert.Throws<ArgumentException>(() => _ = first.Width);
    });

    [Fact]
    public void SetImage_SameReferenceTwice_DoesNotDisposeIt() => StaThread.Run(() =>
    {
        // Arrange
        using var panel = new PreviewPanel();
        var image = new Bitmap(4, 4);
        panel.SetImage(image);

        // Act
        panel.SetImage(image);

        // Assert
        Assert.Equal(4, image.Width);
    });

    [Fact]
    public void Dispose_DisposesTheCurrentImage()
    {
        // Arrange
        Bitmap? image = null;

        // Act
        StaThread.Run(() =>
        {
            var panel = new PreviewPanel();
            image = new Bitmap(4, 4);
            panel.SetImage(image);
            panel.Dispose();
        });

        // Assert
        Assert.Throws<ArgumentException>(() => _ = image!.Width);
    }

    [Theory]
    [InlineData(120, 1)]
    [InlineData(-120, -1)]
    public void MouseWheel_Notch_RaisesZoomRequestedWithSignOfDelta(int delta, int expectedDirection) =>
        StaThread.Run(() =>
        {
            // Arrange
            using var panel = new PreviewPanel();
            PreviewPanel.ZoomRequestedEventArgs? received = null;
            panel.ZoomRequested += (_, e) => received = e;

            // Act
            InvokeProtected(panel, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 0, 0, delta));

            // Assert
            Assert.NotNull(received);
            Assert.Equal(expectedDirection, received!.Direction);
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

    private static void InvokeProtected(Control control, string methodName, object arg)
    {
        MethodInfo method = control.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(control.GetType().Name, methodName);
        method.Invoke(control, [arg]);
    }
}

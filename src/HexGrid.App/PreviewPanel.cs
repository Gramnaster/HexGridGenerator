using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace HexGrid.App;

/// <summary>
/// Shows the rendered preview on a checkerboard, so transparent areas are obviously transparent.
/// </summary>
/// <remarks>
/// The panel scrolls over the whole canvas at the current zoom but holds only one frame: a rendered
/// region around what is in view. Until a frame for the current zoom arrives, the last one is shown
/// stretched to fit, so zooming never waits on a render.
/// </remarks>
public sealed class PreviewPanel : Panel
{
    private static readonly Color CheckerA = Color.FromArgb(0xE8, 0xE8, 0xE8);
    private static readonly Color CheckerB = Color.FromArgb(0xF8, 0xF8, 0xF8);

    private double _canvasWidthPx;
    private double _canvasHeightPx;
    private double _zoom;
    private bool _smoothZoom;

    private Bitmap? _frame;
    private double _frameScale;
    private Rectangle _frameRegion;

    // False once the scene changes. The frame is still shown until its replacement arrives, but no
    // longer counts as covering anything.
    private bool _frameCurrent;

    private string? _message;
    private bool _isPanning;
    private Point _panMouseOrigin;
    private Point _panScrollOrigin;

    /// <summary>Raised on every wheel notch; <see cref="ZoomRequestedEventArgs.Direction"/> is the sign.</summary>
    public event EventHandler<ZoomRequestedEventArgs>? ZoomRequested;

    /// <summary>Raised when scrolling or panning brings a different part of the canvas into view.</summary>
    public event EventHandler? ViewportChanged;

    public PreviewPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, value: true);
        BackColor = Color.FromArgb(0x50, 0x50, 0x50);
        AutoScroll = true;
    }

    /// <summary>Panel pixels per canvas pixel. Zero until the first <see cref="SetView"/>.</summary>
    public double Zoom => _zoom;

    /// <summary>
    /// How frames are drawn past the canvas's own resolution. Off, they are rendered at scale 1 and
    /// magnified, so the user sees the exported PNG's pixels themselves. On, they are rendered at the
    /// zoom, so lines and text stay sharp, as the SVG export draws them.
    /// </summary>
    [DefaultValue(false)]
    public bool SmoothZoom
    {
        get => _smoothZoom;
        set
        {
            if (_smoothZoom == value)
            {
                return;
            }

            // The frame shown no longer matches the render scale, so it is smoothed as a stand-in
            // until its replacement arrives.
            _smoothZoom = value;
            Invalidate();
        }
    }

    /// <summary>The scale frames are rendered at for the current zoom. See <see cref="SmoothZoom"/>.</summary>
    public double RenderScale => _smoothZoom ? _zoom : Math.Min(_zoom, 1.0);

    // Deliberately does not call base.OnMouseWheel: ScrollableControl's default handling would pan
    // the image on every notch, fighting with wheel-to-zoom. Panning when zoomed past fit still
    // works via the scrollbars AutoScroll adds.
    protected override void OnMouseWheel(MouseEventArgs e) =>
        ZoomRequested?.Invoke(this, new ZoomRequestedEventArgs(Math.Sign(e.Delta), e.Location));

    // Middle-button drag-to-pan, matching the press-and-drag behaviour of the spacebar in Photoshop
    // or the middle button in most image viewers.
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Middle)
        {
            return;
        }

        _isPanning = true;
        _panMouseOrigin = e.Location;
        _panScrollOrigin = new Point(-AutoScrollPosition.X, -AutoScrollPosition.Y);
        Cursor = Cursors.Hand;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isPanning)
        {
            return;
        }

        int dx = e.Location.X - _panMouseOrigin.X;
        int dy = e.Location.Y - _panMouseOrigin.Y;
        AutoScrollPosition = new Point(_panScrollOrigin.X - dx, _panScrollOrigin.Y - dy);
        Invalidate();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Middle)
        {
            EndPan();
        }
    }

    // Capture can be lost without a MouseUp (e.g. alt-tab mid-drag). Release the pan state so the
    // cursor doesn't get stuck as a hand.
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        EndPan();
    }

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EndPan()
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        Cursor = Cursors.Default;
        Capture = false;
    }

    /// <summary>
    /// Sets the canvas size and the zoom. The canvas point under <paramref name="anchor"/> (the
    /// panel's centre when null) stays under it, as far as the scroll range allows.
    /// </summary>
    public void SetView(double canvasWidthPx, double canvasHeightPx, double zoom, Point? anchor = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(canvasWidthPx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(canvasHeightPx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoom);

        PointF fixedAt = anchor ?? new PointF(ClientSize.Width / 2f, ClientSize.Height / 2f);
        PointF? canvasPoint = _zoom > 0 ? ClientToCanvas(fixedAt) : null;

        _canvasWidthPx = canvasWidthPx;
        _canvasHeightPx = canvasHeightPx;
        _zoom = zoom;
        AutoScrollMinSize = VirtualSize();

        if (canvasPoint is { } point)
        {
            // Scroll by however far the point drifted from the anchor at the new zoom.
            Point origin = CanvasOrigin();
            double driftX = origin.X + (point.X * zoom) - fixedAt.X;
            double driftY = origin.Y + (point.Y * zoom) - fixedAt.Y;
            AutoScrollPosition = new Point(
                (int)Math.Round(-AutoScrollPosition.X + driftX), (int)Math.Round(-AutoScrollPosition.Y + driftY));
        }

        Invalidate();
    }

    /// <summary>
    /// The pixels in view, widened by <paramref name="marginPx"/> panel pixels on every side, in the
    /// coordinates of the whole canvas rendered at <paramref name="renderScale"/>. Empty when no
    /// canvas is set or none of it is in view.
    /// </summary>
    public Rectangle VisibleRegion(double renderScale, int marginPx)
    {
        if (_zoom <= 0 || renderScale <= 0)
        {
            return Rectangle.Empty;
        }

        double magnification = _zoom / renderScale;
        Point origin = CanvasOrigin();
        var region = Rectangle.FromLTRB(
            (int)Math.Floor((-origin.X - marginPx) / magnification),
            (int)Math.Floor((-origin.Y - marginPx) / magnification),
            (int)Math.Ceiling((ClientSize.Width - origin.X + marginPx) / magnification),
            (int)Math.Ceiling((ClientSize.Height - origin.Y + marginPx) / magnification));
        var canvas = new Rectangle(
            0, 0, (int)Math.Ceiling(_canvasWidthPx * renderScale), (int)Math.Ceiling(_canvasHeightPx * renderScale));
        return Rectangle.Intersect(region, canvas);
    }

    /// <summary>True when the current frame was rendered at <paramref name="renderScale"/> for the current scene and contains <paramref name="region"/>.</summary>
    public bool HasFrameCovering(double renderScale, Rectangle region) =>
        _frame is not null && _frameCurrent && Math.Abs(_frameScale - renderScale) < 1e-9 && _frameRegion.Contains(region);

    /// <summary>
    /// Takes ownership of a rendered frame and disposes the previous one.
    /// </summary>
    /// <param name="region">Where the frame sits on the whole canvas rendered at <paramref name="renderScale"/>.</param>
    public void SetFrame(Bitmap frame, double renderScale, Rectangle region)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ReplaceFrame(frame);
        _frameScale = renderScale;
        _frameRegion = region;
        _frameCurrent = true;
        Invalidate();
    }

    /// <summary>The scene changed: keep showing the frame until its replacement arrives, but stop treating it as current.</summary>
    public void MarkFrameStale() => _frameCurrent = false;

    /// <summary>Drops the frame, used when there is no scene to show.</summary>
    public void ClearFrame()
    {
        ReplaceFrame(frame: null);
        _frameCurrent = false;
        Invalidate();
    }

    /// <summary>Message shown instead of the image, used for layout errors.</summary>
    public void SetMessage(string? message)
    {
        _message = message;
        Invalidate();
    }

    private void ReplaceFrame(Bitmap? frame)
    {
        if (ReferenceEquals(_frame, frame))
        {
            return;
        }

        // IDISP007: _frame was originally handed in through SetFrame, so the analyzer treats it as a
        // caller-owned "injected" value. It isn't: SetFrame's contract is that ownership transfers to
        // PreviewPanel on every call, which is why the previous frame is disposed here rather than
        // left for its original caller to clean up.
        // RCS1146 wants this collapsed back to "_frame?.Dispose();". Deliberately not applied:
        // SharpSource's SS066 (disposable field must be disposed) does not recognize the
        // null-conditional form as a disposal call, so that shape re-triggers a build-breaking SS066
        // "not disposed" false positive. The explicit if is the one form that satisfies both analyzers.
#pragma warning disable IDISP007, RCS1146
        if (_frame is not null)
        {
            _frame.Dispose();
        }
#pragma warning restore IDISP007, RCS1146

        _frame = frame;
    }

    private Size VirtualSize() =>
        new((int)Math.Ceiling(_canvasWidthPx * _zoom), (int)Math.Ceiling(_canvasHeightPx * _zoom));

    /// <summary>Where the canvas's top-left corner is in client coordinates, scrolling included.</summary>
    private Point CanvasOrigin()
    {
        Size virtualSize = VirtualSize();

        // Zoomed past fit, the canvas exceeds the viewport on one or both axes; AutoScroll adds
        // scrollbars for that axis, so that axis anchors at 0 instead of centering. The axis that
        // still fits stays centred.
        // SS003: integer division is intentional: GDI+ draws at integer pixel offsets, so a
        // fractional centre would just get truncated right back anyway. Off-by-at-most-one-pixel
        // centering in a preview-only widget, with no effect on exported output.
#pragma warning disable SS003
        int x = virtualSize.Width < ClientSize.Width ? (ClientSize.Width - virtualSize.Width) / 2 : 0;
        int y = virtualSize.Height < ClientSize.Height ? (ClientSize.Height - virtualSize.Height) / 2 : 0;
#pragma warning restore SS003
        return new Point(x + AutoScrollPosition.X, y + AutoScrollPosition.Y);
    }

    private PointF ClientToCanvas(PointF client)
    {
        Point origin = CanvasOrigin();
        return new PointF((float)((client.X - origin.X) / _zoom), (float)((client.Y - origin.Y) / _zoom));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.Clear(BackColor);

        if (_message is not null)
        {
            TextRenderer.DrawText(g, _message, Font, ClientRectangle, Color.Gainsboro,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        if (_zoom <= 0 || ClientSize.Width < 4 || ClientSize.Height < 4)
        {
            return;
        }

        Point origin = CanvasOrigin();
        var canvas = new Rectangle(origin, VirtualSize());
        Rectangle visible = Rectangle.Intersect(canvas, ClientRectangle);
        if (visible.IsEmpty)
        {
            return;
        }

        DrawChecker(g, visible, origin);
        if (_frame is not null)
        {
            DrawFrame(g, _frame, origin, visible);
        }

        using var border = new Pen(Color.FromArgb(0x30, 0x30, 0x30));
        g.DrawRectangle(border, canvas.X - 1, canvas.Y - 1, canvas.Width + 1, canvas.Height + 1);
    }

    private void DrawFrame(Graphics g, Bitmap frame, Point origin, Rectangle visible)
    {
        double factor = _zoom / _frameScale; // panel pixels per frame pixel
        if (Math.Abs(factor - 1) < 1e-9)
        {
            g.DrawImageUnscaled(frame, origin.X + _frameRegion.X, origin.Y + _frameRegion.Y);
            return;
        }

        // Only the frame pixels in view are scaled, so a deep zoom never scales the whole frame.
        double left = Math.Max(0, Math.Floor(((visible.Left - origin.X) / factor) - _frameRegion.X));
        double top = Math.Max(0, Math.Floor(((visible.Top - origin.Y) / factor) - _frameRegion.Y));
        double right = Math.Min(_frameRegion.Width, Math.Ceiling(((visible.Right - origin.X) / factor) - _frameRegion.X));
        double bottom = Math.Min(_frameRegion.Height, Math.Ceiling(((visible.Bottom - origin.Y) / factor) - _frameRegion.Y));
        if (right <= left || bottom <= top)
        {
            return;
        }

        var source = new RectangleF((float)left, (float)top, (float)(right - left), (float)(bottom - top));
        var target = new RectangleF(
            (float)(origin.X + ((_frameRegion.X + left) * factor)),
            (float)(origin.Y + ((_frameRegion.Y + top) * factor)),
            (float)((right - left) * factor),
            (float)((bottom - top) * factor));

        // An exact frame magnified past the canvas's resolution shows its pixels as hard squares, as
        // any image editor does. A frame from another zoom is only a stand-in, so it is smoothed.
        bool exact = _frameCurrent && Math.Abs(_frameScale - RenderScale) < 1e-9;
        g.InterpolationMode = exact ? InterpolationMode.NearestNeighbor : InterpolationMode.Bilinear;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(frame, target, source, GraphicsUnit.Pixel);
    }

    private static void DrawChecker(Graphics g, Rectangle visible, Point origin)
    {
        const int cell = 12;
        using var brushA = new SolidBrush(CheckerA);
        using var brushB = new SolidBrush(CheckerB);

        g.FillRectangle(brushA, visible);

        // Cells count from the canvas's top-left corner, so the pattern scrolls with the canvas, and
        // only the cells in view are drawn, however large the zoomed canvas is.
        // SS003: integer division is intentional. Both offsets are non-negative (visible lies inside
        // the canvas), so it floors to the first cell in view.
#pragma warning disable SS003
        int firstColumn = (visible.Left - origin.X) / cell;
        int firstRow = (visible.Top - origin.Y) / cell;
#pragma warning restore SS003
        for (int row = firstRow; origin.Y + (row * cell) < visible.Bottom; row++)
        {
            for (int column = firstColumn; origin.X + (column * cell) < visible.Right; column++)
            {
                if ((row + column) % 2 == 0)
                {
                    continue;
                }

                g.FillRectangle(brushB, Rectangle.Intersect(
                    new Rectangle(origin.X + (column * cell), origin.Y + (row * cell), cell, cell), visible));
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ReplaceFrame(frame: null);
        }

        base.Dispose(disposing);
    }

    public sealed class ZoomRequestedEventArgs(int direction, Point location) : EventArgs
    {
        public int Direction { get; } = direction;

        /// <summary>Where the wheel turned, in client coordinates: the point the zoom keeps still.</summary>
        public Point Location { get; } = location;
    }
}

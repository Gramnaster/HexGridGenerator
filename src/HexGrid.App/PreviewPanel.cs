using System.Drawing;
using System.Drawing.Drawing2D;

namespace HexGrid.App;

/// <summary>
/// Shows the rendered preview centred on a checkerboard, so transparent areas are obviously transparent.
/// </summary>
public sealed class PreviewPanel : Panel
{
    private static readonly Color CheckerA = Color.FromArgb(0xE8, 0xE8, 0xE8);
    private static readonly Color CheckerB = Color.FromArgb(0xF8, 0xF8, 0xF8);

    private Bitmap? _image;
    private string? _message;
    private bool _isPanning;
    private Point _panMouseOrigin;
    private Point _panScrollOrigin;

    /// <summary>Raised on every wheel notch; <see cref="ZoomRequestedEventArgs.Direction"/> is the sign.</summary>
    public event EventHandler<ZoomRequestedEventArgs>? ZoomRequested;

    public PreviewPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, value: true);
        BackColor = Color.FromArgb(0x50, 0x50, 0x50);
        AutoScroll = true;
    }

    // Deliberately does not call base.OnMouseWheel: ScrollableControl's default handling would pan
    // the image on every notch, fighting with wheel-to-zoom. Panning when zoomed past fit still
    // works via the scrollbars AutoScroll adds.
    protected override void OnMouseWheel(MouseEventArgs e) =>
        ZoomRequested?.Invoke(this, new ZoomRequestedEventArgs(Math.Sign(e.Delta)));

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

    /// <summary>Takes ownership of the bitmap and disposes the previous one.</summary>
    public void SetImage(Bitmap? image)
    {
        if (ReferenceEquals(_image, image))
        {
            return;
        }

        // IDISP007: _image was originally handed in through this same method, so the analyzer treats
        // it as a caller-owned "injected" value. It isn't: this method's own contract (above) is that
        // ownership transfers to PreviewPanel on every call, which is why the previous image is
        // disposed here rather than left for its original caller to clean up.
        // RCS1146 wants this collapsed back to "_image?.Dispose();". Deliberately not applied:
        // SharpSource's SS066 (disposable field must be disposed) does not recognize the
        // null-conditional form as a disposal call, so that shape re-triggers a build-breaking SS066
        // "not disposed" false positive. The explicit if is the one form that satisfies both analyzers.
#pragma warning disable IDISP007, RCS1146
        if (_image is not null)
        {
            _image.Dispose();
        }
#pragma warning restore IDISP007, RCS1146

        _image = image;
        AutoScrollMinSize = image?.Size ?? Size.Empty;
        Invalidate();
    }

    /// <summary>Message shown instead of the image, used for layout errors.</summary>
    public void SetMessage(string? message)
    {
        _message = message;
        Invalidate();
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

        if (_image is null || ClientSize.Width < 4 || ClientSize.Height < 4)
        {
            return;
        }

        // Zoomed past fit, the image exceeds the viewport on one or both axes; AutoScroll adds
        // scrollbars for that axis and the translate below pans the content, so that axis anchors
        // at 0 instead of centering. The axis that still fits stays centred as before.
        g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

        // SS003: integer division is intentional: GDI+ draws at integer pixel offsets
        // (DrawImageUnscaled below takes int x/y), so a fractional centre would just get truncated
        // right back anyway. Off-by-at-most-one-pixel centering in a preview-only widget, with no
        // effect on exported output.
#pragma warning disable SS003
        int x = _image.Width < ClientSize.Width ? (ClientSize.Width - _image.Width) / 2 : 0;
        int y = _image.Height < ClientSize.Height ? (ClientSize.Height - _image.Height) / 2 : 0;
#pragma warning restore SS003
        var target = new Rectangle(x, y, _image.Width, _image.Height);

        DrawChecker(g, target);

        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImageUnscaled(_image, x, y);

        using var frame = new Pen(Color.FromArgb(0x30, 0x30, 0x30));
        g.DrawRectangle(frame, target.X - 1, target.Y - 1, target.Width + 1, target.Height + 1);
    }

    private static void DrawChecker(Graphics g, Rectangle area)
    {
        const int cell = 12;
        using var brushA = new SolidBrush(CheckerA);
        using var brushB = new SolidBrush(CheckerB);

        g.FillRectangle(brushA, area);
        for (int row = 0; row * cell < area.Height; row++)
        {
            for (int col = 0; col * cell < area.Width; col++)
            {
                if ((row + col) % 2 == 0)
                {
                    continue;
                }

                var r = new Rectangle(
                    area.X + (col * cell), area.Y + (row * cell),
                    Math.Min(cell, area.Width - (col * cell)), Math.Min(cell, area.Height - (row * cell)));
                g.FillRectangle(brushB, r);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Same ownership contract as SetImage above. See the IDISP007 and RCS1146 notes there.
#pragma warning disable IDISP007, RCS1146
            if (_image is not null)
            {
                _image.Dispose();
            }
#pragma warning restore IDISP007, RCS1146

            _image = null;
        }

        base.Dispose(disposing);
    }

    public sealed class ZoomRequestedEventArgs(int direction) : EventArgs
    {
        public int Direction { get; } = direction;
    }
}

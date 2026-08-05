using System.Drawing;
using System.Drawing.Drawing2D;

namespace HexGrid.App;

/// <summary>
/// Shows the rendered preview centred on a checkerboard, so transparent areas are obviously transparent.
/// </summary>
public sealed class PreviewPanel : Control
{
    private static readonly Color CheckerA = Color.FromArgb(0xE8, 0xE8, 0xE8);
    private static readonly Color CheckerB = Color.FromArgb(0xF8, 0xF8, 0xF8);

    private Bitmap? _image;
    private string? _message;

    public PreviewPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(0x50, 0x50, 0x50);
    }

    /// <summary>Takes ownership of the bitmap and disposes the previous one.</summary>
    public void SetImage(Bitmap? image)
    {
        if (ReferenceEquals(_image, image))
        {
            return;
        }

        _image?.Dispose();
        _image = image;
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

        int x = (ClientSize.Width - _image.Width) / 2;
        int y = (ClientSize.Height - _image.Height) / 2;
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
                    area.X + col * cell, area.Y + row * cell,
                    Math.Min(cell, area.Width - col * cell), Math.Min(cell, area.Height - row * cell));
                g.FillRectangle(brushB, r);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _image?.Dispose();
            _image = null;
        }

        base.Dispose(disposing);
    }
}

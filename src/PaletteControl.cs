using System.Drawing.Drawing2D;

namespace Nitemare3D.ImgEditor;

public sealed class PaletteControl : Control
{
    private GamePalette _palette = new();
    private byte _selected;

    public event EventHandler? SelectedIndexChanged;

    public GamePalette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            Invalidate();
        }
    }

    public byte SelectedIndex
    {
        get => _selected;
        set
        {
            if (_selected == value)
                return;
            _selected = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public PaletteControl()
    {
        DoubleBuffered = true;
        MinimumSize = new Size(256, 256);
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;

        float cw = ClientSize.Width / 16f;
        float ch = ClientSize.Height / 16f;

        for (int i = 0; i < 256; i++)
        {
            int x = i % 16;
            int y = i / 16;
            var rect = RectangleF.FromLTRB(x * cw, y * ch, (x + 1) * cw, (y + 1) * ch);

            using var brush = new SolidBrush(_palette.Colors[i]);
            e.Graphics.FillRectangle(brush, rect);

            if (i == _selected)
            {
                using var pen1 = new Pen(Color.White, 2);
                using var pen2 = new Pen(Color.Black, 1);
                e.Graphics.DrawRectangle(pen1, rect.X + 1, rect.Y + 1, rect.Width - 3, rect.Height - 3);
                e.Graphics.DrawRectangle(pen2, rect.X + 3, rect.Y + 3, rect.Width - 7, rect.Height - 7);
            }
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int x = Math.Clamp(e.X * 16 / Math.Max(1, ClientSize.Width), 0, 15);
        int y = Math.Clamp(e.Y * 16 / Math.Max(1, ClientSize.Height), 0, 15);
        SelectedIndex = (byte)(y * 16 + x);
    }
}

using System.Drawing.Drawing2D;

namespace Nitemare3D.ImgEditor;

public sealed class PixelCanvas : Control
{
    private ImgEntry? _entry;
    private GamePalette _palette = new();
    private int _zoom = 6;
    private bool _drawing;
    private bool _strokeChanged;

    public event EventHandler? StrokeStarted;
    public event EventHandler? StrokeFinished;
    public event EventHandler? PixelChanged;
    public event Action<byte>? ColorPicked;

    public byte SelectedIndex { get; set; }

    public ImgEntry? Entry
    {
        get => _entry;
        set
        {
            _entry = value;
            UpdateCanvasSize();
            Invalidate();
        }
    }

    public GamePalette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            Invalidate();
        }
    }

    public int Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, 1, 24);
            UpdateCanvasSize();
            Invalidate();
        }
    }

    public PixelCanvas()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(48, 48, 48);
        Cursor = Cursors.Cross;
    }

    private void UpdateCanvasSize()
    {
        if (_entry is null)
        {
            Size = new Size(64, 64);
            return;
        }

        Size = new Size(
            Math.Max(1, _entry.Width * _zoom),
            Math.Max(1, _entry.Height * _zoom));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_entry is null)
            return;

        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

        int check = Math.Max(4, _zoom);
        for (int y = 0; y < Height; y += check)
        {
            for (int x = 0; x < Width; x += check)
            {
                bool odd = ((x / check) + (y / check)) % 2 != 0;
                using var b = new SolidBrush(
                    odd ? Color.FromArgb(80, 80, 80)
                        : Color.FromArgb(110, 110, 110));
                e.Graphics.FillRectangle(b, x, y, check, check);
            }
        }

        bool transparent = _entry.Kind != "Wall";

        for (int y = 0; y < _entry.Height; y++)
        {
            for (int x = 0; x < _entry.Width; x++)
            {
                byte idx = _entry.GetPixel(x, y);
                if (transparent && idx == GamePalette.TransparentIndex)
                    continue;

                using var b = new SolidBrush(_palette.Colors[idx]);
                e.Graphics.FillRectangle(
                    b, x * _zoom, y * _zoom, _zoom, _zoom);
            }
        }

        if (_zoom >= 6)
        {
            using var pen = new Pen(Color.FromArgb(35, Color.Black), 1);
            for (int x = 0; x <= _entry.Width; x++)
                e.Graphics.DrawLine(
                    pen, x * _zoom, 0, x * _zoom, _entry.Height * _zoom);
            for (int y = 0; y <= _entry.Height; y++)
                e.Graphics.DrawLine(
                    pen, 0, y * _zoom, _entry.Width * _zoom, y * _zoom);
        }

        if (_entry.Width == 128 && _entry.Height == 64)
        {
            using var split = new Pen(Color.Lime, 2);
            e.Graphics.DrawLine(
                split, 64 * _zoom, 0,
                64 * _zoom, 64 * _zoom);
        }
    }

    private bool ToPixel(Point p, out int x, out int y)
    {
        x = p.X / _zoom;
        y = p.Y / _zoom;
        return _entry is not null &&
               x >= 0 && x < _entry.Width &&
               y >= 0 && y < _entry.Height;
    }

    private void PaintPixel(int x, int y)
    {
        if (_entry is null)
            return;

        int pos = y * _entry.Width + x;
        if (_entry.Pixels[pos] == SelectedIndex)
            return;

        _entry.Pixels[pos] = SelectedIndex;
        _strokeChanged = true;
        Invalidate(new Rectangle(
            x * _zoom, y * _zoom, _zoom + 1, _zoom + 1));
        PixelChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!ToPixel(e.Location, out int x, out int y))
            return;

        if (e.Button == MouseButtons.Right && _entry is not null)
        {
            ColorPicked?.Invoke(_entry.GetPixel(x, y));
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            _drawing = true;
            _strokeChanged = false;
            StrokeStarted?.Invoke(this, EventArgs.Empty);
            PaintPixel(x, y);
            Capture = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_drawing && e.Button.HasFlag(MouseButtons.Left) &&
            ToPixel(e.Location, out int x, out int y))
        {
            PaintPixel(x, y);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && _drawing)
        {
            _drawing = false;
            Capture = false;
            if (_strokeChanged)
                StrokeFinished?.Invoke(this, EventArgs.Empty);
        }
    }
}

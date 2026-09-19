using System.Drawing;

namespace Nitemare3D.ImgEditor;

public sealed class GamePalette
{
    public const byte TransparentIndex = 31;
    public Color[] Colors { get; } = new Color[256];
    public string Description { get; private set; } = "Fallback grayscale";

    public GamePalette()
    {
        for (int i = 0; i < 256; i++)
            Colors[i] = Color.FromArgb(i, i, i);
    }

    public void Load(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        if (data.Length < 768)
            throw new InvalidDataException("Palette file must contain at least 768 RGB bytes.");

        int start = data.Length >= 1924 ? 1156 : data.Length - 768;
        if (start + 768 > data.Length)
            throw new InvalidDataException("Palette data is truncated.");

        int maxComponent = 0;
        for (int i = 0; i < 768; i++)
            maxComponent = Math.Max(maxComponent, data[start + i]);

        bool vga6Bit = maxComponent <= 63;

        for (int i = 0; i < 256; i++)
        {
            int r = data[start + i * 3 + 0];
            int g = data[start + i * 3 + 1];
            int b = data[start + i * 3 + 2];

            if (vga6Bit)
            {
                r = Math.Min(255, r * 4);
                g = Math.Min(255, g * 4);
                b = Math.Min(255, b * 4);
            }

            Colors[i] = Color.FromArgb(r, g, b);
        }

        Description = $"{Path.GetFileName(path)} @ {start} " +
                      (vga6Bit ? "(6-bit VGA scaled)" : "(8-bit RGB)");
    }

    public byte FindNearest(Color c, bool allowTransparent = false)
    {
        if (c.A < 128)
            return TransparentIndex;

        int bestIndex = 0;
        long bestDistance = long.MaxValue;

        for (int i = 0; i < 256; i++)
        {
            if (!allowTransparent && i == TransparentIndex)
                continue;

            Color p = Colors[i];
            long dr = c.R - p.R;
            long dg = c.G - p.G;
            long db = c.B - p.B;
            long d = dr * dr + dg * dg + db * db;

            if (d < bestDistance)
            {
                bestDistance = d;
                bestIndex = i;
            }
        }

        return (byte)bestIndex;
    }
}

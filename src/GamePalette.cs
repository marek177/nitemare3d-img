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

        // Nitemare 3D uses two different GAME.PAL container layouts.
        // DOS:     1924 bytes, RGB palette starts at offset 1156.
        // Windows: 5459 bytes, the 256 x RGB palette is the final 768 bytes.
        // Do not use the DOS offset merely because a file is >= 1924 bytes:
        // that was the reason the Windows palette was decoded as garbage.
        int start;
        string format;

        if (data.Length == 1924)
        {
            start = 1156;
            format = "DOS";
        }
        else if (data.Length == 5459)
        {
            start = data.Length - 768; // 4691 / 0x1253
            format = "Windows";
        }
        else
        {
            // Unknown PAL variants: use the final complete 256-entry RGB table.
            start = data.Length - 768;
            format = "generic";
        }

        if (start < 0 || start + 768 > data.Length)
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
                // Map VGA 0..63 to the full 0..255 range.
                r = (r * 255 + 31) / 63;
                g = (g * 255 + 31) / 63;
                b = (b * 255 + 31) / 63;
            }

            Colors[i] = Color.FromArgb(r, g, b);
        }

        Description = $"{Path.GetFileName(path)} | {format} | {data.Length} bytes | " +
                      $"RGB @ {start} (0x{start:X}) " +
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

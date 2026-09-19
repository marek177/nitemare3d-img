using System.Drawing;
using System.Drawing.Imaging;

namespace Nitemare3D.ImgEditor;

public static class ImageCodec
{
    public static Bitmap ToBitmap(ImgEntry entry, GamePalette palette, bool transparentIndex31)
    {
        var bmp = new Bitmap(entry.Width, entry.Height, PixelFormat.Format32bppArgb);
        bool treatTransparent = transparentIndex31 && !entry.IsWall;

        for (int y = 0; y < entry.Height; y++)
        {
            for (int x = 0; x < entry.Width; x++)
            {
                byte idx = entry.GetPixel(x, y);
                if (treatTransparent && idx == GamePalette.TransparentIndex)
                    bmp.SetPixel(x, y, Color.Transparent);
                else
                    bmp.SetPixel(x, y, palette.Colors[idx]);
            }
        }

        return bmp;
    }

    public static ImgEntry FromBitmap(Bitmap bmp, GamePalette palette, byte[]? metadata = null)
    {
        if (bmp.Width is < 1 or > 255 || bmp.Height is < 1 or > 255)
            throw new InvalidDataException("Nitemare IMG width/height are one byte; PNG must be 1..255 pixels.");

        var e = new ImgEntry
        {
            Width = (byte)bmp.Width,
            Height = (byte)bmp.Height,
            Metadata = metadata is { Length: 8 } ? (byte[])metadata.Clone() : new byte[8],
            Pixels = new byte[bmp.Width * bmp.Height]
        };

        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                Color c = bmp.GetPixel(x, y);
                e.Pixels[y * bmp.Width + x] = palette.FindNearest(c, allowTransparent: false);
            }
        }

        return e;
    }

    public static void ExportPng(ImgEntry entry, GamePalette palette, string path, bool transparentIndex31)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        using Bitmap bmp = ToBitmap(entry, palette, transparentIndex31);
        bmp.Save(path, ImageFormat.Png);
    }
}

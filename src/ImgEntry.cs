namespace Nitemare3D.ImgEditor;

public sealed class ImgEntry
{
    public int Index { get; set; }
    public long OriginalOffset { get; set; }
    public byte Width { get; set; }
    public byte Height { get; set; }
    public byte[] Metadata { get; set; } = new byte[8];

    public byte[] Pixels { get; set; } = Array.Empty<byte>();

    public string Kind =>
        Height == 64 && (Width == 64 || Width == 128) ? "Wall" : "Sprite/UI";

    public ImgEntry Clone()
    {
        return new ImgEntry
        {
            Index = Index,
            OriginalOffset = OriginalOffset,
            Width = Width,
            Height = Height,
            Metadata = (byte[])Metadata.Clone(),
            Pixels = (byte[])Pixels.Clone()
        };
    }

    public void CopyFrom(ImgEntry other)
    {
        OriginalOffset = other.OriginalOffset;
        Width = other.Width;
        Height = other.Height;
        Metadata = (byte[])other.Metadata.Clone();
        Pixels = (byte[])other.Pixels.Clone();
    }

    public byte GetPixel(int x, int y) => Pixels[y * Width + x];

    public void SetPixel(int x, int y, byte value)
    {
        Pixels[y * Width + x] = value;
    }

    public override string ToString() =>
        $"#{Index:D4}  {Kind,-9}  {Width}x{Height}";
}

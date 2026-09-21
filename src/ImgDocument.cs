using System.Buffers.Binary;

namespace Nitemare3D.ImgEditor;

public sealed class ImgDocument
{
    public byte[] Header { get; private set; } = Array.Empty<byte>();
    public List<ImgEntry> Entries { get; } = new();
    public string? FilePath { get; private set; }
    public bool Dirty { get; set; }
    public int FirstImageOffset => Header.Length;

    public static ImgDocument Load(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        if (data.Length < 14)
            throw new InvalidDataException("IMG file is too small.");

        uint firstOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4));
        if (firstOffset < 12 || firstOffset >= data.Length)
            throw new InvalidDataException($"Invalid first image offset {firstOffset}.");

        var doc = new ImgDocument
        {
            Header = data[..(int)firstOffset],
            FilePath = Path.GetFullPath(path)
        };

        int pos = (int)firstOffset;
        int index = 0;

        while (pos < data.Length)
        {
            if (pos + 10 > data.Length)
                throw new InvalidDataException($"Truncated image header at 0x{pos:X}.");

            byte w = data[pos];
            byte h = data[pos + 1];
            if (w == 0 || h == 0)
                throw new InvalidDataException($"Invalid zero-sized image #{index} at 0x{pos:X}.");

            int pixelCount = checked(w * h);
            int recordSize = checked(10 + pixelCount);
            if (pos + recordSize > data.Length)
                throw new InvalidDataException($"Image #{index} extends past end of file.");

            var entry = new ImgEntry
            {
                Index = index,
                OriginalOffset = pos,
                Width = w,
                Height = h,
                Metadata = data[(pos + 2)..(pos + 10)],
                Pixels = new byte[pixelCount]
            };

            int src = pos + 10;
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                    entry.Pixels[y * w + x] = data[src++];
            }

            doc.Entries.Add(entry);
            index++;
            pos += recordSize;
        }

        doc.Dirty = false;
        return doc;
    }

    public (string Groups, int Frame) GetGroupFrame(ImgEntry entry)
    {
        // The first 512 DWORDs of the Nitemare 3D IMG header are the Group
        // pointer table used by the original data. A group points at its first
        // physical IMG record; following records belong to that group until the
        // next greater group pointer. Multiple group IDs may alias one start.
        const int groupCount = 512;
        const int tableBytes = groupCount * 4;
        if (Header.Length < tableBytes || entry.OriginalOffset <= 0)
            return ("-", -1);

        uint target = (uint)entry.OriginalOffset;
        uint start = 0;
        var pointers = new uint[groupCount];

        for (int g = 0; g < groupCount; g++)
        {
            uint p = BinaryPrimitives.ReadUInt32LittleEndian(Header.AsSpan(g * 4, 4));
            pointers[g] = p;
            if (p != 0 && p <= target && p > start)
                start = p;
        }

        if (start == 0)
            return ("-", -1);

        uint next = uint.MaxValue;
        for (int g = 0; g < groupCount; g++)
        {
            uint p = pointers[g];
            if (p > start && p < next)
                next = p;
        }

        if (target >= next)
            return ("-", -1);

        var groups = new List<int>();
        for (int g = 0; g < groupCount; g++)
            if (pointers[g] == start)
                groups.Add(g);

        int frame = 0;
        foreach (ImgEntry e in Entries)
        {
            if (e.OriginalOffset < start)
                continue;
            if (e.OriginalOffset >= target)
                break;
            frame++;
        }

        return (groups.Count == 0 ? "-" : string.Join("/", groups), frame);
    }

    public int CountHeaderReferences(ImgEntry entry)
    {
        if (entry.OriginalOffset <= 0 || entry.OriginalOffset > uint.MaxValue)
            return 0;

        uint target = (uint)entry.OriginalOffset;
        int count = 0;
        for (int i = 0; i + 4 <= Header.Length; i += 4)
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(Header.AsSpan(i, 4));
            if (value == target)
                count++;
        }
        return count;
    }

    public void Append(ImgEntry entry)
    {
        entry.Index = Entries.Count;
        entry.OriginalOffset = 0;
        Entries.Add(entry);
        Dirty = true;
    }

    public void Save()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
            throw new InvalidOperationException("No target file is selected.");
        SaveAs(FilePath, makeBackup: true);
    }

    public void SaveAs(string path, bool makeBackup = true)
    {
        path = Path.GetFullPath(path);
        long cursor = Header.Length;
        var newOffsets = new long[Entries.Count];
        var relocation = new Dictionary<uint, uint>();

        for (int i = 0; i < Entries.Count; i++)
        {
            ImgEntry e = Entries[i];
            newOffsets[i] = cursor;

            if (e.OriginalOffset > 0 && e.OriginalOffset <= uint.MaxValue && cursor <= uint.MaxValue)
                relocation[(uint)e.OriginalOffset] = (uint)cursor;

            cursor = checked(cursor + 10L + e.Width * e.Height);
        }

        if (cursor > int.MaxValue)
            throw new InvalidDataException("IMG file would exceed this editor's 2 GB safety limit.");

        byte[] newHeader = (byte[])Header.Clone();
        for (int i = 0; i + 4 <= newHeader.Length; i += 4)
        {
            uint oldValue = BinaryPrimitives.ReadUInt32LittleEndian(newHeader.AsSpan(i, 4));
            if (relocation.TryGetValue(oldValue, out uint newValue))
                BinaryPrimitives.WriteUInt32LittleEndian(newHeader.AsSpan(i, 4), newValue);
        }

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string temp = path + ".tmp";
        using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(newHeader);

            foreach (ImgEntry e in Entries)
            {
                fs.WriteByte(e.Width);
                fs.WriteByte(e.Height);
                fs.Write(e.Metadata, 0, 8);

                for (int x = 0; x < e.Width; x++)
                {
                    for (int y = 0; y < e.Height; y++)
                        fs.WriteByte(e.Pixels[y * e.Width + x]);
                }
            }

            fs.Flush(true);
        }

        if (makeBackup && File.Exists(path))
            File.Copy(path, path + ".bak", overwrite: true);

        File.Move(temp, path, overwrite: true);

        Header = newHeader;
        for (int i = 0; i < Entries.Count; i++)
        {
            Entries[i].Index = i;
            Entries[i].OriginalOffset = newOffsets[i];
        }

        FilePath = path;
        Dirty = false;
    }
}

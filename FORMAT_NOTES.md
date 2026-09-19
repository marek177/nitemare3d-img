# Nitemare 3D IMG format notes

## Verified on the supplied episode files

The supplied files parse cleanly as a header followed by sequential uncompressed image records.

Observed image counts in the current supplied versions:

- IMG.1: 840 sequential records
- IMG.2: 740 sequential records
- IMG.3: 572 sequential records

All three supplied files use first-image offset `0xBC00` (48128).

Common dimensions include:

- `128x64` walls,
- `64x64` walls,
- many variable-size sprites such as `42x58`, `26x57`, `44x57`, etc.

This proves that Nitemare sprites do not require a fixed 64x64 record.

## Record serialization

```text
+0  BYTE width
+1  BYTE height
+2  BYTE metadata[8]
+10 BYTE pixel data [width*height], column-major
```

Metadata is preserved byte-for-byte by Replace.

## Relocation strategy

The 0xBC00-byte header contains multiple structures and is not treated as one simple offset table.

When saving after a size change, the editor:

1. computes every old and new sequential image start;
2. clones the original header;
3. scans aligned DWORDs in the header;
4. only when a DWORD exactly equals an old known image start, it substitutes the corresponding new image start;
5. leaves all other values unchanged.

This avoids trying to regenerate still-undocumented parts of the header.

## Physical deletion

Do not physically delete records yet. Sequential sprite indices are used by animation logic, and deleting one record shifts all later indices.

Safe delete = retain the record and blank its pixel data.

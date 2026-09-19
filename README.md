# Nitemare 3D IMG Editor — C# / .NET 8

A graphical editor for the original Nitemare 3D episode graphics containers:

- `IMG.1`
- `IMG.2`
- `IMG.3`

The project is deliberately separate from the map editor so graphics editing can be tested independently before the two tools are combined.

## Current features

- Open original `IMG.1`, `IMG.2`, `IMG.3`.
- Sequentially decode every image record.
- Correct column-major Nitemare pixel layout.
- Recognize common wall records (`64x64` and `128x64`).
- Variable-size sprite/UI records.
- Pixel-level editor with palette painting and eyedropper.
- `GAME.PAL` loading.
- Palette-index-31 transparency for sprites in preview/export.
- Green 64-pixel divider for `128x64` wall graphics.
- Extract one image to PNG.
- Extract all images to PNG.
- Replace an existing wall/sprite with PNG/BMP.
- Replacement may change width and height (1..255).
- Append a new image at the end.
- Duplicate an image.
- Safe Clear/Blank operation that preserves the image index.
- Undo/Redo.
- Save / Save As.
- `.bak` backup before overwrite.
- Header relocation: any aligned 32-bit header value equal to a known original image start is rewritten to the corresponding new start after image sizes change.
- Every other unknown header byte is preserved.

## Important: Delete vs Clear

Enemy animations can depend on stable sequential image indices.

For that reason this first version **does not physically remove an image record from the middle of the file**.

`Clear / Blank` instead keeps the record and index:

- sprites/UI -> palette index 31,
- walls -> palette index 0.

Appending at the end does not shift old indices.

A future "Physical Remove" command should only be enabled after all original game reference mechanisms have been mapped.

## Nitemare IMG record

Observed on the original files and independently documented by reverse-engineering work:

```text
IMG header/directory/metadata region
...
first image:
    BYTE width
    BYTE height
    BYTE metadata[8]
    BYTE pixels[width * height]
next image:
    ...
```

Pixels are stored in **column-major** order:

```text
for x = 0..width-1
    for y = 0..height-1
        read pixel
```

The editor converts this to normal row-major pixels internally and converts it back on Save.

## 128x64 walls

The editor treats `128x64` as one IMG record, but shows a green vertical divider at pixel 64:

```text
0..63       64..127
+-----------+-----------+
| 64x64 A   | 64x64 B   |
+-----------+-----------+
```

This matches the Nitemare renderer behavior where the second 64-pixel half can be selected as an alternate wall variant.

This is separate from animation: animated walls may advance through multiple image records.

## Palette

For the classic DOS `GAME.PAL` (1924 bytes), the editor reads the 768-byte RGB palette at offset 1156.

If another PAL is loaded, the editor falls back to the final 768 bytes.

6-bit VGA components (`0..63`) are automatically scaled to 8-bit RGB.

Without a palette file the editor uses grayscale, so editing should normally begin by loading the game's `GAME.PAL`.

## Build

Requires Visual Studio 2022 or .NET 8 SDK on Windows.

```bat
dotnet build -c Release
```

Publish x64:

```bat
dotnet publish -c Release -r win-x64 --self-contained false
```

Publish x86:

```bat
dotnet publish -c Release -r win-x86 --self-contained false
```

## Recommended workflow

1. Make copies of `IMG.1`, `IMG.2`, `IMG.3`.
2. Start the editor.
3. Load `GAME.PAL`.
4. Open an IMG file.
5. Export original graphics before changing them.
6. Replace/edit sprites or walls.
7. Save As to a test IMG file.
8. Test the result in the original game and OpenNitemare3D.
9. Only after validation replace the working game file.

## Planned Stage 2

- Decode WDC-like logical groups from the header tables.
- Group/animation tree instead of only sequential image index.
- Play animation frames in the preview.
- Dedicated 128x64 split editor.
- Batch PNG import with index mapping.
- Reference search: show which wall/object/animation uses each image.
- Integration into MapEdit72-N3D C#.

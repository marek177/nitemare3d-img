namespace Nitemare3D.ImgEditor;

public sealed class MainForm : Form
{
    ImgDocument? doc;
    readonly GamePalette pal = new();
    readonly HistoryManager hist = new();
    readonly ListView list = new();
    readonly PixelCanvas canvas = new();
    readonly PaletteControl palette = new();
    readonly Panel previewScroll = new();
    readonly Label status = new(), details = new();
    readonly ComboBox filter = new();
    readonly NumericUpDown zoom = new();
    readonly CheckBox transparent = new();
    ImgEntry? strokeBefore;
    int current = -1;

    ImgEntry? Cur => doc is not null && current >= 0 && current < doc.Entries.Count
        ? doc.Entries[current]
        : null;

    public MainForm()
    {
        Text = "Nitemare 3D IMG Editor";
        Width = 1250;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;
        BuildMenu();
        BuildUi();
        Hook();
        RefreshUi();
    }

    void BuildMenu()
    {
        var m = new MenuStrip();
        var f = new ToolStripMenuItem("&File");
        var i = new ToolStripMenuItem("&Image");

        f.DropDownItems.Add("Open IMG.1/2/3...", null, (_, _) => Open());
        f.DropDownItems.Add("Load GAME.PAL...", null, (_, _) => LoadPal());
        f.DropDownItems.Add("Save", null, (_, _) => Save());
        f.DropDownItems.Add("Save As...", null, (_, _) => SaveAs());

        i.DropDownItems.Add("Export selected PNG...", null, (_, _) => ExportOne());
        i.DropDownItems.Add("Export all PNG...", null, (_, _) => ExportAll());
        i.DropDownItems.Add("Replace selected from PNG/BMP...", null, (_, _) => Replace());
        i.DropDownItems.Add("Append PNG/BMP...", null, (_, _) => Append());
        i.DropDownItems.Add("Duplicate selected", null, (_, _) => Duplicate());
        i.DropDownItems.Add("Clear / Blank selected", null, (_, _) => Clear());
        i.DropDownItems.Add("Undo", null, (_, _) => Undo());
        i.DropDownItems.Add("Redo", null, (_, _) => Redo());

        m.Items.AddRange([f, i]);
        MainMenuStrip = m;
        Controls.Add(m);
    }

    void BuildUi()
    {
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            Padding = new Padding(6)
        };

        filter.Items.AddRange(["All", "Walls", "Sprites"]);
        filter.SelectedIndex = 0;
        filter.DropDownStyle = ComboBoxStyle.DropDownList;

        zoom.Minimum = 1;
        zoom.Maximum = 24;
        zoom.Value = 6;

        transparent.Text = "Index 31 transparent";
        transparent.Checked = true;
        transparent.AutoSize = true;

        top.Controls.AddRange([
            new Label { Text = "Filter:", AutoSize = true },
            filter,
            new Label { Text = "Zoom:", AutoSize = true },
            zoom,
            transparent
        ]);

        var a = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 340
        };

        list.Dock = DockStyle.Fill;
        list.View = View.Details;
        list.FullRowSelect = true;
        list.MultiSelect = false;

        foreach (var c in new[]
        {
            ("Index", 58), ("Group", 72), ("Frame", 52), ("Type", 78), ("Size", 70), ("Offset", 82), ("Refs", 45)
        })
            list.Columns.Add(c.Item1, c.Item2);

        a.Panel1.Controls.Add(list);

        var b = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 590
        };

        previewScroll.Dock = DockStyle.Fill;
        previewScroll.AutoScroll = true;
        previewScroll.BackColor = Color.FromArgb(40, 40, 40);
        previewScroll.Controls.Add(canvas);
        canvas.Location = new Point(8, 8);

        b.Panel1.Controls.Add(previewScroll);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            Padding = new Padding(8)
        };

        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 275));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        palette.Dock = DockStyle.Fill;
        details.Dock = DockStyle.Fill;
        details.Font = new Font(FontFamily.GenericMonospace, 9);

        right.Controls.Add(palette);
        right.Controls.Add(details);
        right.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Left mouse = paint\r\nRight mouse = eyedropper\r\nReplace preserves 8 metadata bytes.\r\nSave creates .bak before overwrite."
        });

        b.Panel2.Controls.Add(right);
        a.Panel2.Controls.Add(b);

        status.Dock = DockStyle.Bottom;
        status.Height = 24;
        status.BorderStyle = BorderStyle.Fixed3D;

        Controls.Add(a);
        Controls.Add(status);
        Controls.Add(top);
        top.BringToFront();

        canvas.Palette = pal;
        palette.Palette = pal;
        canvas.Zoom = 6;
    }

    void Hook()
    {
        list.SelectedIndexChanged += (_, _) =>
        {
            if (list.SelectedItems.Count == 1 && list.SelectedItems[0].Tag is int n)
                Select(n);
        };

        filter.SelectedIndexChanged += (_, _) => Rebuild();

        zoom.ValueChanged += (_, _) =>
        {
            canvas.Zoom = (int)zoom.Value;
            ResetPreviewView();
        };

        palette.SelectedIndexChanged += (_, _) => canvas.SelectedIndex = palette.SelectedIndex;
        canvas.ColorPicked += x => palette.SelectedIndex = x;
        canvas.StrokeStarted += (_, _) => strokeBefore = Cur?.Clone();
        canvas.StrokeFinished += (_, _) =>
        {
            if (strokeBefore is not null && doc is not null)
            {
                hist.PushUndo(current, strokeBefore);
                strokeBefore = null;
                doc.Dirty = true;
                RefreshUi();
            }
        };
        canvas.PixelChanged += (_, _) =>
        {
            if (doc is not null)
            {
                doc.Dirty = true;
                RefreshUi();
            }
        };

        previewScroll.Resize += (_, _) => PositionPreviewCanvas();
        canvas.SizeChanged += (_, _) => PositionPreviewCanvas();

        FormClosing += (_, e) =>
        {
            if (!DiscardOk())
                e.Cancel = true;
        };
    }

    void ResetPreviewView()
    {
        // AutoScroll remembers the scroll position from the previously selected
        // image. When a smaller sprite is selected afterwards, that old offset
        // can leave the top of the new sprite outside the visible area.
        previewScroll.AutoScrollPosition = Point.Empty;
        previewScroll.PerformLayout();
        PositionPreviewCanvas();

        // WinForms may recalculate AutoScroll after the child size changes.
        // Reset once more after that layout pass so the image always starts
        // fully inside the viewport.
        if (previewScroll.IsHandleCreated)
        {
            BeginInvoke(new Action(() =>
            {
                if (previewScroll.IsDisposed)
                    return;

                previewScroll.AutoScrollPosition = Point.Empty;
                previewScroll.PerformLayout();
                PositionPreviewCanvas();
                canvas.Invalidate();
            }));
        }
    }

    void PositionPreviewCanvas()
    {
        if (previewScroll.IsDisposed)
            return;

        const int margin = 8;
        int x = margin;
        int y = margin;

        // Center images which fit in the viewport. Large images stay at the
        // upper-left margin and can be reached with the scrollbars.
        if (canvas.Width + margin * 2 < previewScroll.ClientSize.Width)
            x = Math.Max(margin, (previewScroll.ClientSize.Width - canvas.Width) / 2);

        if (canvas.Height + margin * 2 < previewScroll.ClientSize.Height)
            y = Math.Max(margin, (previewScroll.ClientSize.Height - canvas.Height) / 2);

        canvas.Location = new Point(x, y);
    }

    bool DiscardOk() =>
        doc is null ||
        !doc.Dirty ||
        MessageBox.Show(
            this,
            "Unsaved changes. Continue without saving?",
            "Nitemare 3D IMG Editor",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) == DialogResult.Yes;

    void Open()
    {
        if (!DiscardOk()) return;
        using var d = new OpenFileDialog
        {
            Filter = "Nitemare IMG files|IMG.1;IMG.2;IMG.3|All files|*.*"
        };
        if (d.ShowDialog() != DialogResult.OK) return;

        try
        {
            doc = ImgDocument.Load(d.FileName);
            hist.Clear();
            current = doc.Entries.Count > 0 ? 0 : -1;
            Rebuild();
            SelectList(current);
            if (list.Items.Count > 0)
                list.Items[0].EnsureVisible();
            RefreshUi();
        }
        catch (Exception e)
        {
            Err(e);
        }
    }

    void LoadPal()
    {
        using var d = new OpenFileDialog
        {
            Filter = "Nitemare GAME.PAL|GAME.PAL;*.PAL|All files|*.*"
        };
        if (d.ShowDialog() != DialogResult.OK) return;

        try
        {
            pal.Load(d.FileName);
            canvas.Palette = pal;
            palette.Palette = pal;
            RefreshUi();
        }
        catch (Exception e)
        {
            Err(e);
        }
    }

    void Save()
    {
        if (doc is null) return;
        try
        {
            doc.Save();
            Rebuild();
            RefreshUi();
        }
        catch (Exception e)
        {
            Err(e);
        }
    }

    void SaveAs()
    {
        if (doc is null) return;
        using var d = new SaveFileDialog
        {
            Filter = "Nitemare IMG file|IMG.1;IMG.2;IMG.3|All files|*.*",
            FileName = doc.FilePath is null ? "IMG.1" : Path.GetFileName(doc.FilePath)
        };
        if (d.ShowDialog() != DialogResult.OK) return;

        try
        {
            doc.SaveAs(d.FileName, true);
            Rebuild();
            RefreshUi();
        }
        catch (Exception e)
        {
            Err(e);
        }
    }

    void Rebuild()
    {
        int keep = current;
        list.BeginUpdate();
        list.Items.Clear();

        if (doc is not null)
        {
            foreach (var e in doc.Entries)
            {
                if ((filter.SelectedIndex == 1 && !e.IsWall) ||
                    (filter.SelectedIndex == 2 && e.IsWall))
                    continue;

                var x = new ListViewItem(e.Index.ToString("D4"));
                var gf = doc.GetGroupFrame(e);
                x.SubItems.Add(gf.Groups);
                x.SubItems.Add(gf.Frame >= 0 ? gf.Frame.ToString("D2") : "-");
                x.SubItems.Add(e.Kind);
                x.SubItems.Add($"{e.Width}x{e.Height}");
                x.SubItems.Add($"0x{e.OriginalOffset:X}");
                x.SubItems.Add(doc.CountHeaderReferences(e).ToString());
                x.Tag = e.Index;
                list.Items.Add(x);

                if (e.Index == keep)
                    x.Selected = true;
            }
        }

        list.EndUpdate();

        // Always keep the real first IMG record visible. Older builds could
        // open with 0002 at the top even though records 0000/0001 existed.
        if (list.Items.Count > 0 && (keep <= 0 || current <= 0))
        {
            list.Items[0].Selected = true;
            list.Items[0].Focused = true;
            list.Items[0].EnsureVisible();
        }
    }

    void Select(int n)
    {
        if (doc is null || n < 0 || n >= doc.Entries.Count)
        {
            current = -1;
            canvas.Entry = null;
        }
        else
        {
            current = n;
            canvas.Entry = doc.Entries[n];
            canvas.SelectedIndex = palette.SelectedIndex;
        }

        ResetPreviewView();
        RefreshUi();
    }

    void SelectList(int n)
    {
        foreach (ListViewItem x in list.Items)
        {
            if (x.Tag is int i && i == n)
            {
                x.Selected = true;
                x.EnsureVisible();
                return;
            }
        }
        Select(n);
    }

    void RefreshUi()
    {
        Text = "Nitemare 3D IMG Editor" +
               (doc?.FilePath is null ? "" : $" - {Path.GetFileName(doc.FilePath)}") +
               (doc?.Dirty == true ? " *" : "");

        status.Text = doc is null
            ? $"Ready | Palette: {pal.Description}"
            : $"{doc.Entries.Count} images | first 0x{doc.FirstImageOffset:X} | Palette: {pal.Description}";

        var e = Cur;
        details.Text = e is null
            ? "No image selected."
            : $"Index: {e.Index}\r\n" +
              $"Group: {doc!.GetGroupFrame(e).Groups}\r\n" +
              $"Frame: {(doc.GetGroupFrame(e).Frame >= 0 ? doc.GetGroupFrame(e).Frame.ToString("D2") : "-")}\r\n" +
              $"Type: {e.Kind}\r\n" +
              $"Size: {e.Width}x{e.Height}\r\n" +
              $"Offset: 0x{e.OriginalOffset:X}\r\n" +
              $"Hdr refs: {doc!.CountHeaderReferences(e)}\r\n" +
              $"Meta: {BitConverter.ToString(e.Metadata)}\r\n" +
              $"Color: {palette.SelectedIndex}";
    }

    static string ExportFileName(ImgEntry e)
    {
        // e.Kind is currently filesystem-safe (Sprite-UI), but sanitize the
        // final name as an additional guard against future labels.
        string name = $"{e.Index:D4}_{e.Kind}_{e.Width}x{e.Height}.png";
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Replace('/', '_').Replace('\\', '_');
        return name;
    }

    void ExportOne()
    {
        var e = Cur;
        if (e is null) return;

        using var d = new SaveFileDialog
        {
            Filter = "PNG image|*.png",
            FileName = ExportFileName(e)
        };
        if (d.ShowDialog() != DialogResult.OK) return;

        try
        {
            ImageCodec.ExportPng(e, pal, d.FileName, transparent.Checked);
        }
        catch (Exception ex)
        {
            Err(ex);
        }
    }

    void ExportAll()
    {
        if (doc is null) return;
        using var d = new FolderBrowserDialog();
        if (d.ShowDialog() != DialogResult.OK) return;

        try
        {
            foreach (var e in doc.Entries)
                ImageCodec.ExportPng(
                    e,
                    pal,
                    Path.Combine(d.SelectedPath, ExportFileName(e)),
                    transparent.Checked);
        }
        catch (Exception ex)
        {
            Err(ex);
        }
    }

    enum Wall128ImportMode
    {
        Cancel,
        DuplicateBoth,
        ReplaceLeft,
        ReplaceRight
    }

    Wall128ImportMode AskWall128ImportMode()
    {
        using var dlg = new Form
        {
            Text = "Import 64x64 into 128x64 wall",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(430, 150)
        };

        dlg.Controls.Add(new Label
        {
            Left = 12,
            Top = 12,
            Width = 405,
            Height = 42,
            Text = "This 128x64 wall contains two 64x64 halves.\r\nChoose how the imported 64x64 texture is used:"
        });

        Wall128ImportMode result = Wall128ImportMode.Cancel;
        void AddButton(string text, int left, Wall128ImportMode mode)
        {
            var b = new Button { Text = text, Left = left, Top = 72, Width = 95, Height = 32 };
            b.Click += (_, _) => { result = mode; dlg.DialogResult = DialogResult.OK; dlg.Close(); };
            dlg.Controls.Add(b);
        }

        AddButton("Both", 12, Wall128ImportMode.DuplicateBoth);
        AddButton("Left", 115, Wall128ImportMode.ReplaceLeft);
        AddButton("Right", 218, Wall128ImportMode.ReplaceRight);
        AddButton("Cancel", 321, Wall128ImportMode.Cancel);

        dlg.ShowDialog(this);
        return result;
    }

    void Replace()
    {
        var old = Cur;
        if (old is null || doc is null) return;

        using var d = new OpenFileDialog { Filter = "Images|*.png;*.bmp" };
        if (d.ShowDialog() != DialogResult.OK) return;

        try
        {
            using var bmp = new Bitmap(d.FileName);

            // Nitemare 3D 128x64 walls are two logical 64x64 wall halves.
            // Importing one 64x64 texture must not resize/relocate the IMG record.
            if (old.Width == 128 && old.Height == 64 && bmp.Width == 64 && bmp.Height == 64)
            {
                Wall128ImportMode mode = AskWall128ImportMode();
                if (mode == Wall128ImportMode.Cancel)
                    return;

                ImgEntry imported = ImageCodec.FromBitmap(bmp, pal, old.Metadata);
                hist.PushUndo(current, old);

                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        byte px = imported.GetPixel(x, y);
                        if (mode == Wall128ImportMode.DuplicateBoth || mode == Wall128ImportMode.ReplaceLeft)
                            old.SetPixel(x, y, px);
                        if (mode == Wall128ImportMode.DuplicateBoth || mode == Wall128ImportMode.ReplaceRight)
                            old.SetPixel(x + 64, y, px);
                    }
                }

                doc.Dirty = true;
                canvas.Invalidate();
                Rebuild();
                SelectList(current);
                ResetPreviewView();
                return;
            }

            if ((bmp.Width != old.Width || bmp.Height != old.Height) &&
                MessageBox.Show(
                    this,
                    $"Change {old.Width}x{old.Height} to {bmp.Width}x{bmp.Height}? Offsets will be relocated.",
                    "Resize",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            hist.PushUndo(current, old);
            var n = ImageCodec.FromBitmap(bmp, pal, old.Metadata);
            n.Index = old.Index;
            n.OriginalOffset = old.OriginalOffset;
            old.CopyFrom(n);
            doc.Dirty = true;
            Rebuild();
            SelectList(current);
            ResetPreviewView();
        }
        catch (Exception e)
        {
            Err(e);
        }
    }

    void Append()
    {
        if (doc is null) return;
        using var d = new OpenFileDialog { Filter = "Images|*.png;*.bmp" };
        if (d.ShowDialog() != DialogResult.OK) return;

        try
        {
            using var bmp = new Bitmap(d.FileName);
            var e = ImageCodec.FromBitmap(bmp, pal, Cur?.Metadata ?? new byte[8]);
            doc.Append(e);
            current = e.Index;
            Rebuild();
            SelectList(current);
            ResetPreviewView();
        }
        catch (Exception e)
        {
            Err(e);
        }
    }

    void Duplicate()
    {
        if (doc is null || Cur is null) return;
        var e = Cur.Clone();
        e.OriginalOffset = 0;
        doc.Append(e);
        current = e.Index;
        Rebuild();
        SelectList(current);
        ResetPreviewView();
    }

    void Clear()
    {
        var e = Cur;
        if (e is null || doc is null) return;
        if (MessageBox.Show(
                this,
                "Blank pixels but keep the image index?",
                "Clear",
                MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;

        hist.PushUndo(current, e);
        Array.Fill(e.Pixels, e.IsWall ? (byte)0 : GamePalette.TransparentIndex);
        doc.Dirty = true;
        canvas.Invalidate();
        RefreshUi();
    }

    void Undo()
    {
        if (doc is null) return;
        int n = hist.Undo(doc.Entries);
        if (n >= 0)
        {
            doc.Dirty = true;
            Rebuild();
            SelectList(n);
            ResetPreviewView();
        }
    }

    void Redo()
    {
        if (doc is null) return;
        int n = hist.Redo(doc.Entries);
        if (n >= 0)
        {
            doc.Dirty = true;
            Rebuild();
            SelectList(n);
            ResetPreviewView();
        }
    }

    void Err(Exception e) =>
        MessageBox.Show(this, e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
}

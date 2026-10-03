using System.Drawing;
using System.Globalization;
using HexGrid.App.Rendering;
using HexGrid.Core;
using HexGrid.Core.Layout;
using HexGrid.Core.Naming;
using HexGrid.Core.Presets;
using HexGrid.Core.Scene;

namespace HexGrid.App;

public sealed class MainForm : Form
{
    private const double MinZoomPercent = 10.0;

    // The deepest zoom, in canvas pixels rather than percent of fit: 3200%, as in Photoshop. Past
    // 100% the preview magnifies the exported pixels themselves, so going deeper costs no rendering.
    private const double MaxZoomOfCanvas = 32.0;

    // Each wheel notch multiplies or divides the zoom by this. A fixed step in percent would take
    // thousands of notches to get from fit to 3200% on a large canvas.
    private const double ZoomStepFactor = 1.25;

    // Panel pixels rendered beyond each edge of the view, so a short pan needs no new frame.
    private const int PreviewOverscanPx = 128;
    private static readonly int[] ZoomPresets = [200, 150, 100, 75, 50];

    // SS066: these three are Controls added into this form's own Controls tree in BuildUi() below
    // (via root -> split -> Panel1/Panel2, and root directly for _status), not orphaned fields.
    // Control.Dispose(bool) documents that it "releases the unmanaged resources used by the Control
    // and its child controls." base.Dispose(disposing) at the bottom of this class's own
    // Dispose(bool) already disposes them recursively. Explicitly disposing them again here would be
    // redundant, not a fix for a real leak.
#pragma warning disable SS066
    private readonly PropertyGrid _properties = new();
    private readonly PreviewPanel _preview = new();
    private readonly Label _status = new();
    private readonly Label _savingTag = new();
#pragma warning restore SS066
    // Not a Control (doesn't live in the Controls tree above), so not covered by the SS066 reasoning
    // either - disposed explicitly below, same as _zoomMenu. Assigned in the constructor body, not
    // here: a field initializer cannot reference _properties (CS0236), since it needs to already exist.
    private readonly PropertyGridBoolOverlay _boolOverlay;
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 180 };

    // Assigned in the constructor body for the same CS0236 reason as _boolOverlay: it is handed this
    // form's own methods. Disposed explicitly below.
    private readonly PreviewRenderLoop _renderLoop;

    // Not part of the Controls tree above (it's a popup, only assigned via _preview.ContextMenuStrip),
    // so it is not covered by the SS066 auto-dispose reasoning and needs its own Dispose() call below.
    private readonly ContextMenuStrip _zoomMenu = new();

    // Buttons that write files. Disabled while a background export runs, so two writes can never
    // race each other onto the same path.
    private readonly List<Button> _saveButtons = [];

    private GridSettings _settings = new();
    private GridLayout? _layout;
    private DrawScene? _scene;
    private string? _lastFolder;
    private double _zoomPercent = 100.0;

    // True when a setting changed since the last layout. Zoom and panel resizes only need the
    // preview redrawn at a new scale: the layout and scene don't depend on either.
    private bool _layoutStale;
    private bool _isSaving;

    public MainForm()
    {
        Text = "HexGrid Generator";
        MinimumSize = new Size(1000, 640);
        Size = new Size(1360, 860);
        StartPosition = FormStartPosition.CenterScreen;

        _renderLoop = new PreviewRenderLoop(NextPreviewRequest, ShowPreviewFrame, ShowPreviewFailure);
        BuildUi();
        BuildZoomMenu();

        _properties.SelectedObject = _settings;
        _boolOverlay = new PropertyGridBoolOverlay(_properties);
        _boolOverlay.Toggled += (_, _) => ScheduleRebuild();
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            if (_layoutStale)
            {
                Rebuild();
            }
            else
            {
                RefreshPreview();
            }
        };

        Shown += (_, _) => Rebuild();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0),
        };
        // Without an explicit ColumnStyle the single column defaults to AutoSize and collapses
        // around the preferred width of its contents, squeezing the whole window.
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        SplitContainer split = BuildSplit();

        root.Controls.Add(BuildButtonBar(), 0, 0);
        root.Controls.Add(split, 0, 1);
        root.Controls.Add(BuildStatusBar(), 0, 2);
        Controls.Add(root);

        Shown += (_, _) => TrySetPreferredSplit(split);
    }

    private SplitContainer BuildSplit()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1,
            SplitterWidth = 6,
            // Setting Panel2MinSize triggers SplitContainer's internal ApplyPanel2MinSize, which
            // recomputes SplitterDistance immediately - before this control is parented and Dock
            // has a chance to size it. Its default Width is far smaller than Panel1MinSize +
            // Panel2MinSize, so that recompute throws unless Width is already large enough here.
            Width = 900,
            Panel1MinSize = 300,
            Panel2MinSize = 200,
        };

        _properties.Dock = DockStyle.Fill;
        _properties.PropertySort = PropertySort.Categorized;
        _properties.ToolbarVisible = false;
        _properties.HelpVisible = true;
        _properties.PropertyValueChanged += (_, e) =>
        {
            if (string.Equals(e.ChangedItem?.PropertyDescriptor?.Name, nameof(GridSettings.GridType), StringComparison.Ordinal))
            {
                // GridType changes which rows the descriptor exposes and how they're labelled.
                // Refresh() alone doesn't re-run GetProperties(), so the category list is stale
                // until the grid re-binds to the same object.
                _properties.SelectedObject = null;
                _properties.SelectedObject = _settings;
            }

            ScheduleRebuild();
        };
        split.Panel1.Controls.Add(_properties);

        _preview.Dock = DockStyle.Fill;
        _preview.Resize += (_, _) => ScheduleRender();
        _preview.ZoomRequested += (_, e) => AdjustZoom(e.Direction, e.Location);
        _preview.ViewportChanged += (_, _) => _renderLoop.Kick();
        _preview.ContextMenuStrip = _zoomMenu;
        split.Panel2.Controls.Add(_preview);

        return split;
    }

    private void BuildZoomMenu()
    {
        foreach (int percent in ZoomPresets)
        {
            var item = new ToolStripMenuItem(string.Create(CultureInfo.CurrentCulture, $"{percent}%")) { Tag = percent };
            item.Click += (_, _) => SetZoom(percent);
            _zoomMenu.Items.Add(item);
        }

        _zoomMenu.Opening += (_, _) =>
        {
            foreach (ToolStripItem entry in _zoomMenu.Items)
            {
                if (entry is ToolStripMenuItem menuItem && menuItem.Tag is int percent)
                {
                    menuItem.Checked = Math.Abs(_zoomPercent - percent) < 0.01;
                }
            }
        };
    }

    private FlowLayoutPanel BuildButtonBar()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(8, 8, 8, 8),
        };
        _saveButtons.Add(MakeButton("Export PNG…", ExportPngAsync, 130));
        _saveButtons.Add(MakeButton("Export SVG…", ExportSvgAsync, 130));
        _saveButtons.Add(MakeButton("Export both…", ExportBothAsync, 130));
        bar.Controls.AddRange([.. _saveButtons]);
        bar.Controls.Add(new Label { Width = 24, Height = 1 });

        Button savePreset = MakeButton("Save preset…", SavePreset, 130);
        _saveButtons.Add(savePreset);
        bar.Controls.Add(savePreset);
        bar.Controls.Add(MakeButton("Load preset…", LoadPreset, 130));
        bar.Controls.Add(MakeButton("Reset", ResetSettings, 90));

        _savingTag.AutoSize = true;
        _savingTag.Visible = false;
        _savingTag.Margin = new Padding(8, 7, 0, 0);
        _savingTag.ForeColor = SystemColors.Highlight;
        bar.Controls.Add(_savingTag);
        return bar;
    }

    private Label BuildStatusBar()
    {
        _status.Dock = DockStyle.Fill;
        _status.AutoSize = false;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Padding = new Padding(10, 0, 10, 0);
        _status.ForeColor = SystemColors.GrayText;
        _status.Text = "Ready.";
        return _status;
    }

    private static void TrySetPreferredSplit(SplitContainer split)
    {
        // Only safe once the container has a real width. The default split from BuildSplit() above
        // is usable on its own, so a too-narrow window is not fatal - just logged in case the
        // underlying cause is something other than "window too narrow" (BuildSplit() above has the
        // construction-order bug this SplitContainer already hit once).
        try
        {
            split.SplitterDistance = 430;
        }
        catch (InvalidOperationException ex)
        {
            // SplitContainer.set_SplitterDistance throws InvalidOperationException when the distance
            // falls outside Panel1MinSize..Width-Panel2MinSize.
            Program.WriteCrashLog(ex);
        }
    }

    private static Button MakeButton(string text, Action onClick, int width)
    {
        Button b = NewButton(text, width);
        b.Click += (_, _) => onClick();
        return b;
    }

    // async void is confined to this Click handler, as event handlers require. SaveInBackgroundAsync
    // catches and reports a failed write. Anything thrown before it, still on the UI thread, reaches
    // Application.ThreadException in Program like any other click handler's exception.
    private static Button MakeButton(string text, Func<Task> onClick, int width)
    {
        Button b = NewButton(text, width);
        b.Click += async (_, _) => await onClick();
        return b;
    }

    private static Button NewButton(string text, int width) =>
        new() { Text = text, Width = width, Height = 30, Margin = new Padding(0, 0, 8, 0) };

    // ---------------------------------------------------------------- pipeline

    private void ScheduleRebuild()
    {
        _layoutStale = true;
        ScheduleRender();
    }

    private void ScheduleRender()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void Rebuild()
    {
        _layoutStale = false;
        try
        {
            _layout = GridLayoutEngine.Build(_settings);
            _scene = SceneBuilder.Build(_settings, _layout);
            _preview.SetMessage(message: null);
            _preview.MarkFrameStale();

            UpdateStatus();
            RefreshPreview();
        }
        catch (Exception ex)
        {
            // A bad setting combination must leave the window usable so it can be corrected,
            // never take the application down. The friendly ex.Message goes to the UI; the full
            // exception still goes to the crash log so a genuinely unexpected failure (not just an
            // out-of-range setting) is diagnosable after the fact.
            Program.WriteCrashLog(ex);
            _layout = null;
            _scene = null;
            _preview.ClearFrame();
            _preview.SetMessage(ex.Message);
            _status.Text = "Cannot lay out this grid: " + ex.Message;
        }
    }

    /// <summary>Applies the current zoom to the panel and starts rendering whatever it now lacks.</summary>
    private void RefreshPreview(Point? zoomAnchor = null)
    {
        if (_scene is null)
        {
            return;
        }

        _preview.SetView(_scene.WidthPx, _scene.HeightPx, PreviewZoom(_scene), zoomAnchor);
        _renderLoop.Kick();
    }

    // Panel pixels per canvas pixel. _zoomPercent is relative to the live "fit" scale, not to native
    // canvas pixels. At 100% (the default) the preview always fits the panel.
    private double PreviewZoom(DrawScene scene) =>
        Math.Min(FitScale(scene) * (_zoomPercent / 100.0), MaxZoomOfCanvas);

    private double FitScale(DrawScene scene)
    {
        // The panel's full size, not ClientSize: the scrollbars that appear once zoomed past fit must
        // not shrink the fit scale, or every zoom past fit would render twice at slightly different scales.
        int availW = Math.Max(1, _preview.Width - 24);
        int availH = Math.Max(1, _preview.Height - 24);
        return Math.Clamp(Math.Min(availW / scene.WidthPx, availH / scene.HeightPx), 0.001, 1.0);
    }

    /// <summary>The frame the panel needs for what is in view now, or null when its current frame covers it.</summary>
    private PreviewRequest? NextPreviewRequest()
    {
        if (_scene is null || _preview.Zoom <= 0)
        {
            return null;
        }

        double scale = PreviewPanel.RenderScaleFor(_preview.Zoom);
        Rectangle inView = _preview.VisibleRegion(scale, marginPx: 0);
        if (inView.IsEmpty || _preview.HasFrameCovering(scale, inView))
        {
            return null;
        }

        return new PreviewRequest(
            _scene, ExportService.BackgroundFor(_settings), _settings.Antialiasing, scale, _preview.VisibleRegion(scale, PreviewOverscanPx));
    }

    private void ShowPreviewFrame(PreviewRequest request, Bitmap frame)
    {
        // Rendered for a scene that a rebuild has replaced since. IDISP007: the render loop hands the
        // frame over to this method, so it is this method's to dispose, not an injected dependency.
        if (!ReferenceEquals(request.Scene, _scene))
        {
#pragma warning disable IDISP007
            frame.Dispose();
#pragma warning restore IDISP007
            return;
        }

        _preview.SetFrame(frame, request.Scale, request.Region);
    }

    private void ShowPreviewFailure(Exception ex)
    {
        // As in Rebuild(): friendly ex.Message for the UI, full exception to the crash log.
        Program.WriteCrashLog(ex);
        _preview.SetMessage(ex.Message);
    }

    private void AdjustZoom(int direction, Point anchor)
    {
        if (direction == 0)
        {
            return;
        }

        SetZoom(direction > 0 ? _zoomPercent * ZoomStepFactor : _zoomPercent / ZoomStepFactor, anchor);
    }

    // Not debounced: the panel rescales at once, showing the last frame stretched, while the exact
    // frame renders in the background. A fast wheel spin cancels the frames it overtakes.
    private void SetZoom(double percent, Point? anchor = null)
    {
        // The deepest zoom is fixed in canvas pixels, so as a percent of fit it depends on the canvas and panel.
        double maxPercent = _scene is null ? percent : MaxZoomOfCanvas / FitScale(_scene) * 100.0;
        _zoomPercent = Math.Clamp(percent, MinZoomPercent, Math.Max(MinZoomPercent, maxPercent));
        RefreshPreview(anchor);
    }

    private void UpdateStatus()
    {
        if (_layout is null)
        {
            return;
        }

        _status.Text = StatusText.Describe(_settings, _layout);
    }

    // ----------------------------------------------------------------- actions

    private string SuggestedName() =>
        _layout is null ? "HexGrid" : FileNameBuilder.Build(_settings, _layout);

    private bool ConfirmLargeExport()
    {
        if (_layout is null)
        {
            return false;
        }

        long pixels = (long)Math.Ceiling(_layout.CanvasWidthPx) * (long)Math.Ceiling(_layout.CanvasHeightPx);
        if (pixels <= ExportService.LargeExportPixels)
        {
            return true;
        }

        double gb = pixels * 4.0 / (1024 * 1024 * 1024);

        // MA0076: explicit CurrentCulture - this is a dialog read by a local interactive user.
        string message =
            string.Create(CultureInfo.CurrentCulture, $"This export is {_layout.CanvasWidthPx:0} × {_layout.CanvasHeightPx:0} px ({pixels / 1_000_000.0:0} megapixels). ") +
            string.Create(CultureInfo.CurrentCulture, $"It needs roughly {gb:0.0} GB of memory while rendering.\n\nSVG has no such limit. Continue anyway?");

        return MessageBox.Show(
            this,
            message,
            "Large export",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning) == DialogResult.OK;
    }

    // The exports below write on a background thread so the window stays usable. Each one captures
    // the scene it was asked for and a copy of the settings first: the property grid edits _settings
    // in place, and an edit made mid-export must not leak into the file being written.

    private async Task ExportPngAsync()
    {
        DrawScene? scene = _scene;
        if (scene is null || !ConfirmLargeExport())
        {
            return;
        }

        string? path = AskPath("PNG image (*.png)|*.png", ".png");
        if (path is null)
        {
            return;
        }

        GridSettings settings = SnapshotSettings();
        await SaveInBackgroundAsync(Path.GetFileName(path), () => SavePngWithOwnRasterizer(scene, settings, path));
    }

    private async Task ExportSvgAsync()
    {
        DrawScene? scene = _scene;
        if (scene is null)
        {
            return;
        }

        string? path = AskPath("SVG vector (*.svg)|*.svg", ".svg");
        if (path is null)
        {
            return;
        }

        GridSettings settings = SnapshotSettings();
        await SaveInBackgroundAsync(Path.GetFileName(path), () =>
        {
            ExportService.SaveSvg(scene, settings, path);
            return [path];
        });
    }

    private async Task ExportBothAsync()
    {
        DrawScene? scene = _scene;
        if (scene is null || !ConfirmLargeExport())
        {
            return;
        }

        string? path = AskPath("PNG image (*.png)|*.png", ".png");
        if (path is null)
        {
            return;
        }

        GridSettings settings = SnapshotSettings();
        await SaveInBackgroundAsync(Path.GetFileNameWithoutExtension(path) + " (PNG and SVG)", () =>
        {
            var written = new List<string>(SavePngWithOwnRasterizer(scene, settings, path));
            string svgPath = Path.ChangeExtension(path, ".svg");
            ExportService.SaveSvg(scene, settings, svgPath);
            written.Add(svgPath);
            return written;
        });
    }

    private GridSettings SnapshotSettings() => PresetIo.Deserialize(PresetIo.Serialize(_settings));

    // SceneRasterizer caches fonts, pens and brushes without locking, and the preview render loop
    // keeps its own busy meanwhile, so a background export gets a rasterizer of its own.
    private static IReadOnlyList<string> SavePngWithOwnRasterizer(DrawScene scene, GridSettings settings, string path)
    {
        using var rasterizer = new SceneRasterizer();
        return ExportService.SavePng(rasterizer, scene, settings, path);
    }

    /// <summary>
    /// Runs <paramref name="save"/> off the UI thread. Every file-writing button is disabled and a
    /// "Saving" tag is shown until it finishes, and a failure is reported the same way RunGuarded does.
    /// </summary>
    internal async Task SaveInBackgroundAsync(string description, Func<IReadOnlyList<string>> save)
    {
        SetSaving(description);
        try
        {
            IReadOnlyList<string> written = await Task.Run(save);
            Report(written);
        }
        catch (Exception ex)
        {
            Program.WriteCrashLog(ex);
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _status.Text = "Export failed: " + ex.Message;
        }
        finally
        {
            SetSaving(description: null);
        }
    }

    private void SetSaving(string? description)
    {
        _isSaving = description is not null;
        foreach (Button button in _saveButtons)
        {
            button.Enabled = !_isSaving;
        }

        _savingTag.Text = _isSaving ? $"Saving {description}…" : string.Empty;
        _savingTag.Visible = _isSaving;
    }

    private void SavePreset()
    {
        string? path = AskPath("HexGrid preset (*.json)|*.json", ".json");
        if (path is null)
        {
            return;
        }

        RunGuarded("Saving preset failed", () =>
        {
            PresetIo.Save(_settings, path);
            _status.Text = $"Preset saved to {path}";
        });
    }

    private void LoadPreset()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "HexGrid preset (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = _lastFolder ?? string.Empty,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        RunGuarded("Loading preset failed", () =>
        {
            _settings = PresetIo.Load(dialog.FileName);
            _lastFolder = Path.GetDirectoryName(dialog.FileName);
            _properties.SelectedObject = _settings;
            Rebuild();
            _status.Text = $"Preset loaded from {dialog.FileName}";
        });
    }

    private void ResetSettings()
    {
        _settings = new GridSettings();
        _properties.SelectedObject = _settings;
        Rebuild();
    }

    // ----------------------------------------------------------------- helpers

    private string? AskPath(string filter, string extension)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = filter,
            DefaultExt = extension.TrimStart('.'),
            AddExtension = true,
            FileName = SuggestedName() + extension,
            InitialDirectory = _lastFolder ?? string.Empty,
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return null;
        }

        _lastFolder = Path.GetDirectoryName(dialog.FileName);
        return dialog.FileName;
    }

    private void RunGuarded(string failureCaption, Action action)
    {
        var previous = Cursor;
        Cursor = Cursors.WaitCursor;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            // As in Rebuild(): friendly ex.Message for the UI, full exception to the crash log.
            Program.WriteCrashLog(ex);
            MessageBox.Show(this, ex.Message, failureCaption, MessageBoxButtons.OK, MessageBoxIcon.Error);
            _status.Text = failureCaption + ": " + ex.Message;
        }
        finally
        {
            Cursor = previous;
        }
    }

    private void Report(IReadOnlyList<string> written) =>
        _status.Text = written.Count == 1
            ? $"Wrote {written[0]}"
            : $"Wrote {written.Count} files to {Path.GetDirectoryName(written[0])}";

    // Closing mid-export would end the process with a half-written file on disk.
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_isSaving && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            MessageBox.Show(this, "An export is still being written. Close the window once it finishes.",
                "Export in progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _debounce.Dispose();
            _renderLoop.Dispose();
            _zoomMenu.Dispose();
            _boolOverlay.Dispose();
        }

        base.Dispose(disposing);
    }
}

using System.Drawing;
using System.Globalization;
using HexGrid.App.Rendering;
using HexGrid.Core;
using HexGrid.Core.Layout;
using HexGrid.Core.Naming;
using HexGrid.Core.Presets;
using HexGrid.Core.Scene;
using HexGrid.Core.Units;

namespace HexGrid.App;

public sealed class MainForm : Form
{
    private const double MinZoomPercent = 10.0;
    private const double MaxZoomPercent = 400.0;
    private const double ZoomStepPercent = 10.0;
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
#pragma warning restore SS066
    // Not a Control (doesn't live in the Controls tree above), so not covered by the SS066 reasoning
    // either - disposed explicitly below, same as _zoomMenu. Assigned in the constructor body, not
    // here: a field initializer cannot reference _properties (CS0236), since it needs to already exist.
    private readonly PropertyGridBoolOverlay _boolOverlay;
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 180 };
    private readonly SceneRasterizer _rasterizer = new();

    // Not part of the Controls tree above (it's a popup, only assigned via _preview.ContextMenuStrip),
    // so it is not covered by the SS066 auto-dispose reasoning and needs its own Dispose() call below.
    private readonly ContextMenuStrip _zoomMenu = new();

    private GridSettings _settings = new();
    private GridLayout? _layout;
    private DrawScene? _scene;
    private string? _lastFolder;
    private double _zoomPercent = 100.0;

    public MainForm()
    {
        Text = "HexGrid Generator";
        MinimumSize = new Size(1000, 640);
        Size = new Size(1360, 860);
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        BuildZoomMenu();

        _properties.SelectedObject = _settings;
        _boolOverlay = new PropertyGridBoolOverlay(_properties);
        _boolOverlay.Toggled += (_, _) => ScheduleRebuild();
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Rebuild();
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
        _preview.Resize += (_, _) => ScheduleRebuild();
        _preview.ZoomRequested += (_, e) => AdjustZoom(e.Direction);
        _preview.ContextMenuStrip = _zoomMenu;
        split.Panel2.Controls.Add(_preview);

        return split;
    }

    private void BuildZoomMenu()
    {
        foreach (int percent in ZoomPresets)
        {
            var item = new ToolStripMenuItem($"{percent}%") { Tag = percent };
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
        bar.Controls.Add(MakeButton("Export PNG…", ExportPng, 130));
        bar.Controls.Add(MakeButton("Export SVG…", ExportSvg, 130));
        bar.Controls.Add(MakeButton("Export both…", ExportBoth, 130));
        bar.Controls.Add(new Label { Width = 24, Height = 1 });
        bar.Controls.Add(MakeButton("Save preset…", SavePreset, 130));
        bar.Controls.Add(MakeButton("Load preset…", LoadPreset, 130));
        bar.Controls.Add(MakeButton("Reset", ResetSettings, 90));
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
        var b = new Button { Text = text, Width = width, Height = 30, Margin = new Padding(0, 0, 8, 0) };
        b.Click += (_, _) => onClick();
        return b;
    }

    // ---------------------------------------------------------------- pipeline

    private void ScheduleRebuild()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void Rebuild()
    {
        try
        {
            _layout = GridLayoutEngine.Build(_settings);
            _scene = SceneBuilder.Build(_settings, _layout);
            _preview.SetMessage(message: null);

            UpdateStatus();
            RenderPreview();
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
            _preview.SetImage(image: null);
            _preview.SetMessage(ex.Message);
            _status.Text = "Cannot lay out this grid: " + ex.Message;
        }
    }

    private void RenderPreview()
    {
        if (_scene is null)
        {
            return;
        }

        int availW = Math.Max(1, _preview.ClientSize.Width - 24);
        int availH = Math.Max(1, _preview.ClientSize.Height - 24);
        double fitScale = Math.Min(availW / _scene.WidthPx, availH / _scene.HeightPx);
        fitScale = Math.Clamp(fitScale, 0.001, 1.0);

        // _zoomPercent is relative to the live "fit" scale above, not to native canvas pixels. At
        // 100% (the default) the preview always fits the panel, matching the pre-zoom behavior.
        double scale = Math.Max(0.001, fitScale * (_zoomPercent / 100.0));

        Color bg = ExportService.BackgroundFor(_settings);
        _preview.SetImage(_rasterizer.Render(_scene, bg, _settings.Antialiasing, scale, minStrokePx: 1.0));
    }

    private void AdjustZoom(int direction) => SetZoom(_zoomPercent + (direction * ZoomStepPercent));

    private void SetZoom(double percent)
    {
        _zoomPercent = Math.Clamp(percent, MinZoomPercent, MaxZoomPercent);
        RenderPreview();
    }

    private void UpdateStatus()
    {
        if (_layout is null)
        {
            return;
        }

        var scale = new UnitScale(_settings.Unit, _settings.Dpi);
        // SS018: Pixels was previously reached only via the "_" default. Listed explicitly so the
        // fallback is reserved for genuinely unhandled future values, not a real current member.
        string unit = _settings.Unit switch
        {
            LengthUnit.Pixels => "px",
            LengthUnit.Millimeters => "mm",
            LengthUnit.Centimeters => "cm",
            LengthUnit.Inches => "in",
            _ => "px",
        };

        string canvas = _settings.Preset == CanvasPreset.Custom
            ? "Custom"
            : CanvasPresets.ShortName(_settings.Preset) + OrientationSuffix();

        string OrientationSuffix() =>
            CanvasPresets.Resolve(_settings.Preset).IsPaper
                ? " " + _settings.PageOrientation.ToString().ToLowerInvariant()
                : string.Empty;

        bool hexGrid = _settings.GridType != GridType.Square;
        string sizingHint = SizingHint(_settings, _layout, hexGrid);
        string cellsWord = hexGrid ? "hexes" : "squares";
        string sizeLine = hexGrid
            ? F($"hex {_layout.CellWidthPx:0.#} × {_layout.CellHeightPx:0.#} px ") +
              F($"({scale.FromPx(_layout.CellWidthPx):0.##} {unit} wide)  ·  ")
            : F($"square {_layout.CellWidthPx:0.#} px ") +
              F($"({scale.FromPx(_layout.CellWidthPx):0.##} {unit} side)  ·  ");

        // MA0076: number formatting here is deliberately locale-aware (this is status text read by a
        // local interactive user, not machine-parsed output, unlike SvgRenderer's InvariantCulture,
        // which is a file-format requirement). CurrentCulture is spelled out explicitly rather than
        // left implicit, via the FormattableString overload so each interpolated line still reads
        // cleanly instead of a wall of string.Create(...) calls.
        _status.Text =
            F($"{canvas}  ·  {_layout.CanvasWidthPx:0} × {_layout.CanvasHeightPx:0} px @ {_settings.Dpi} dpi ") +
            F($"({_layout.CanvasWidthMm:0.#} × {_layout.CanvasHeightMm:0.#} mm)  ·  ") +
            F($"{_layout.Columns} × {_layout.Rows} {cellsWord}") + sizingHint + F($"  ·  ") +
            sizeLine +
            F($"{_layout.Cells.Count} cells");
    }

    // Hex: AutoFitRowsColumns only. Columns and Rows share one resolved hex size, so whichever axis
    // implies the smaller size wins and the other is inert; hexes always fill the frame by
    // overhanging and clipping, so there is no gap to report, just which axis currently matters.
    //
    // Square + AutoFitSquares: unlike hex, the non-binding axis leaves a real, visible margin
    // instead of overhanging - see SquareLayoutEngine.RecommendFit. In AutoFitRowsColumns mode this
    // reports the tightest achievable (Columns, Rows) and its leftover gap; in FixedHexWidth mode
    // it reports the square size that would produce that same tight fit for the stored Columns/Rows.
    private static string SizingHint(GridSettings settings, GridLayout layout, bool hexGrid)
    {
        if (hexGrid)
        {
            return settings.SizingMode == GridSizingMode.AutoFitRowsColumns
                ? HexSizingHint(settings, layout)
                : string.Empty;
        }

        if (!settings.AutoFitSquares)
        {
            return string.Empty;
        }

        return settings.SizingMode == GridSizingMode.AutoFitRowsColumns
            ? SquareFitHint(settings, layout)
            : SquareRecommendedSizeHint(settings, layout);
    }

    private static string HexSizingHint(GridSettings settings, GridLayout layout)
    {
        (bool columnsBound, int threshold) = HexLayoutEngine.SizingBindingHint(settings, layout.ClipBounds);
        return columnsBound
            ? F($"  ·  Rows need ≥ {threshold} to matter")
            : F($"  ·  Columns need ≥ {threshold} to matter");
    }

    private static string SquareFitHint(GridSettings settings, GridLayout layout)
    {
        var scale = new UnitScale(settings.Unit, settings.Dpi);
        SquareFitSuggestion fit = SquareLayoutEngine.RecommendFit(settings, layout.ClipBounds);
        string unit = UnitSuffix(settings.Unit);

        // FlushAxis moves the whole leftover onto one side instead of splitting it - report the full
        // residual and name that side, rather than the "half on each side" wording centred mode gets.
        (bool columnsBound, _) = SquareLayoutEngine.SizingBindingHint(settings, layout.ClipBounds);
        bool verticalGap = columnsBound;
        bool flushed = settings.AutoFitSquares &&
            (settings.FlushAxis == FlushAxis.Both || settings.FlushAxis == (verticalGap ? FlushAxis.Vertical : FlushAxis.Horizontal));
        string sideDescription = flushed ? $"on the {FarSideName(settings, verticalGap)}" : "on two sides";

        double gapNow = scale.FromPx(flushed ? fit.CurrentGapPx : fit.CurrentGapPx / 2.0);
        if (gapNow < 0.05)
        {
            return string.Empty;
        }

        if (!fit.HasTighterFit)
        {
            return F($"  ·  ≈{gapNow:0.##} {unit} gap {sideDescription} (canvas doesn't divide evenly by {settings.Columns} × {settings.Rows})");
        }

        if (fit.GapPx <= 0)
        {
            return F($"  ·  ≈{gapNow:0.##} {unit} gap {sideDescription} - try {fit.Columns} × {fit.Rows} for no gap");
        }

        double gapAfter = scale.FromPx(flushed ? fit.GapPx : fit.GapPx / 2.0);
        return F($"  ·  ≈{gapNow:0.##} {unit} gap {sideDescription} - try {fit.Columns} × {fit.Rows} for ≈{gapAfter:0.##} {unit}");
    }

    /// <summary>The side that receives the whole leftover margin when FlushAxis is on for this axis: opposite CoordinateOrigin.</summary>
    private static string FarSideName(GridSettings settings, bool verticalAxis)
    {
        if (verticalAxis)
        {
            bool originTop = settings.CoordinateOrigin is CoordinateOrigin.TopLeft or CoordinateOrigin.TopRight;
            return originTop ? "bottom" : "top";
        }

        bool originLeft = settings.CoordinateOrigin is CoordinateOrigin.TopLeft or CoordinateOrigin.BottomLeft;
        return originLeft ? "right" : "left";
    }

    private static string SquareRecommendedSizeHint(GridSettings settings, GridLayout layout)
    {
        var scale = new UnitScale(settings.Unit, settings.Dpi);
        SquareFitSuggestion fit = SquareLayoutEngine.RecommendFit(settings, layout.ClipBounds);
        double recommendedSize = scale.FromPx(fit.SidePx);

        return fit.GapPx <= 0
            ? F($"  ·  {fit.Columns} × {fit.Rows} squares at {recommendedSize:0.##} {UnitSuffix(settings.Unit)} side gives no gap")
            : F($"  ·  {fit.Columns} × {fit.Rows} squares fit tightest at {recommendedSize:0.##} {UnitSuffix(settings.Unit)} side");
    }

    private static string UnitSuffix(LengthUnit unit) => unit switch
    {
        LengthUnit.Pixels => "px",
        LengthUnit.Millimeters => "mm",
        LengthUnit.Centimeters => "cm",
        LengthUnit.Inches => "in",
        _ => "px",
    };

    // MA0076: explicit CurrentCulture - this is status text read by a local interactive user, not
    // machine-parsed output. Shared by every status-bar/hint builder in this file.
    private static string F(FormattableString s) => s.ToString(CultureInfo.CurrentCulture);

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

        string message =
            F($"This export is {_layout.CanvasWidthPx:0} × {_layout.CanvasHeightPx:0} px ({pixels / 1_000_000.0:0} megapixels). ") +
            F($"It needs roughly {gb:0.0} GB of memory while rendering.\n\nSVG has no such limit. Continue anyway?");

        return MessageBox.Show(
            this,
            message,
            "Large export",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning) == DialogResult.OK;
    }

    private void ExportPng()
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

        RunGuarded(() => Report(ExportService.SavePng(_rasterizer, scene, _settings, path)));
    }

    private void ExportSvg()
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

        RunGuarded(() =>
        {
            ExportService.SaveSvg(scene, _settings, path);
            Report([path]);
        });
    }

    private void ExportBoth()
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

        RunGuarded(() =>
        {
            var written = new List<string>(ExportService.SavePng(_rasterizer, scene, _settings, path));
            string svgPath = Path.ChangeExtension(path, ".svg");
            ExportService.SaveSvg(scene, _settings, svgPath);
            written.Add(svgPath);
            Report(written);
        });
    }

    private void SavePreset()
    {
        string? path = AskPath("HexGrid preset (*.json)|*.json", ".json");
        if (path is null)
        {
            return;
        }

        RunGuarded(() =>
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

        RunGuarded(() =>
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

    private void RunGuarded(Action action)
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
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _status.Text = "Export failed: " + ex.Message;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _debounce.Dispose();
            _rasterizer.Dispose();
            _zoomMenu.Dispose();
            _boolOverlay.Dispose();
        }

        base.Dispose(disposing);
    }
}

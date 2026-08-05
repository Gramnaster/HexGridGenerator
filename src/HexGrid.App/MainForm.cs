using System.Drawing;
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
    private readonly PropertyGrid _properties = new();
    private readonly PreviewPanel _preview = new();
    private readonly Label _status = new();
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 180 };
    private readonly SceneRasterizer _rasterizer = new();

    private GridSettings _settings = new();
    private GridLayout? _layout;
    private DrawScene? _scene;
    private string? _lastFolder;

    public MainForm()
    {
        Text = "HexGrid Generator";
        MinimumSize = new Size(1000, 640);
        Size = new Size(1360, 860);
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();

        _properties.SelectedObject = _settings;
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
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

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
        _properties.PropertyValueChanged += (_, _) => ScheduleRebuild();
        split.Panel1.Controls.Add(_properties);

        _preview.Dock = DockStyle.Fill;
        _preview.Resize += (_, _) => ScheduleRebuild();
        split.Panel2.Controls.Add(_preview);

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

        _status.Dock = DockStyle.Fill;
        _status.AutoSize = false;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Padding = new Padding(10, 0, 10, 0);
        _status.ForeColor = SystemColors.GrayText;
        _status.Text = "Ready.";

        root.Controls.Add(split, 0, 0);
        root.Controls.Add(bar, 0, 1);
        root.Controls.Add(_status, 0, 2);
        Controls.Add(root);

        // Only safe once the container has a real width, and the exception type the setter throws
        // when out of range has varied across versions, so catch broadly. The default split is fine.
        Shown += (_, _) =>
        {
            try
            {
                split.SplitterDistance = 430;
            }
            catch (Exception)
            {
                // Window too narrow for the preferred split.
            }
        };
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
            _layout = HexLayoutEngine.Build(_settings);
            _scene = SceneBuilder.Build(_settings, _layout);
            _preview.SetMessage(null);

            UpdateStatus();
            RenderPreview();
        }
        catch (Exception ex)
        {
            // A bad setting combination must leave the window usable so it can be corrected,
            // never take the application down.
            _layout = null;
            _scene = null;
            _preview.SetImage(null);
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
        double scale = Math.Min(availW / _scene.WidthPx, availH / _scene.HeightPx);
        scale = Math.Clamp(scale, 0.001, 1.0);

        Color bg = ExportService.BackgroundFor(_settings);
        Bitmap bmp = _rasterizer.Render(_scene, bg, _settings.Antialiasing, scale, minStrokePx: 1.0);
        _preview.SetImage(bmp);
    }

    private void UpdateStatus()
    {
        if (_layout is null)
        {
            return;
        }

        var scale = new UnitScale(_settings.Unit, _settings.Dpi);
        string unit = _settings.Unit switch
        {
            LengthUnit.Millimeters => "mm",
            LengthUnit.Centimeters => "cm",
            LengthUnit.Inches => "in",
            _ => "px",
        };

        string canvas = _settings.Preset == CanvasPreset.Custom
            ? "Custom"
            : CanvasPresets.ShortName(_settings.Preset) +
              (CanvasPresets.Resolve(_settings.Preset).IsPaper ? " " + _settings.PageOrientation.ToString().ToLowerInvariant() : string.Empty);

        _status.Text =
            $"{canvas}  ·  {_layout.CanvasWidthPx:0} × {_layout.CanvasHeightPx:0} px @ {_settings.Dpi} dpi " +
            $"({_layout.CanvasWidthMm:0.#} × {_layout.CanvasHeightMm:0.#} mm)  ·  " +
            $"{_layout.Columns} × {_layout.Rows} hexes  ·  " +
            $"hex {_layout.HexWidthPx:0.#} × {_layout.HexHeightPx:0.#} px " +
            $"({scale.FromPx(_layout.HexWidthPx):0.##} {unit} wide)  ·  " +
            $"{_layout.Cells.Count} cells";
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
        return MessageBox.Show(
            this,
            $"This export is {_layout.CanvasWidthPx:0} × {_layout.CanvasHeightPx:0} px ({pixels / 1_000_000.0:0} megapixels). " +
            $"It needs roughly {gb:0.0} GB of memory while rendering.\n\nSVG has no such limit. Continue anyway?",
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
        }

        base.Dispose(disposing);
    }
}

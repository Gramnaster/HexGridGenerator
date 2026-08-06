using System.Drawing;
using System.Drawing.Imaging;
using HexGrid.Core;
using HexGrid.Core.Rendering;
using HexGrid.Core.Scene;

namespace HexGrid.App.Rendering;

/// <summary>Writes the scene to disk as SVG or PNG, flattened or one file per layer.</summary>
public static class ExportService
{
    // SS018: Transparent was previously reached only via the "_" default. Listed explicitly so the
    // fallback is reserved for genuinely unhandled future values, not a real current member.
    public static Color BackgroundFor(GridSettings s) => s.PngBackground switch
    {
        PngBackground.Transparent => Color.Transparent,
        PngBackground.White => Color.White,
        PngBackground.Black => Color.Black,
        PngBackground.Custom => s.PngCustomBackground,
        _ => Color.Transparent,
    };

    public static void SaveSvg(DrawScene scene, GridSettings settings, string path)
    {
        Color bg = BackgroundFor(settings);
        string svg = SvgRenderer.Render(scene, bg.A == 0 ? null : bg);
        File.WriteAllText(path, svg);
    }

    /// <summary>Writes the flattened PNG and returns every file written.</summary>
    public static IReadOnlyList<string> SavePng(
        SceneRasterizer rasterizer, DrawScene scene, GridSettings settings, string path)
    {
        ArgumentNullException.ThrowIfNull(rasterizer);
        ArgumentNullException.ThrowIfNull(scene);

        var written = new List<string>();
        Color bg = BackgroundFor(settings);

        using (Bitmap flat = rasterizer.Render(scene, bg, settings.Antialiasing))
        {
            flat.SetResolution((float)scene.Dpi, (float)scene.Dpi);
            flat.Save(path, ImageFormat.Png);
        }

        written.Add(path);

        if (!settings.ExportLayersSeparately)
        {
            return written;
        }

        string dir = Path.GetDirectoryName(path) ?? ".";
        string stem = Path.GetFileNameWithoutExtension(path);

        foreach (SceneLayer layer in scene.Layers)
        {
            if (layer.IsEmpty)
            {
                continue;
            }

            // Every layer but the flattened one stays transparent so it stacks cleanly in Photoshop.
            string layerPath = Path.Combine(dir, $"{stem}_{layer.Name}.png");
            LayerKind kind = layer.Kind;
            using Bitmap bmp = rasterizer.Render(scene, Color.Transparent, settings.Antialiasing,
                includeLayer: k => k == kind);
            bmp.SetResolution((float)scene.Dpi, (float)scene.Dpi);
            bmp.Save(layerPath, ImageFormat.Png);
            written.Add(layerPath);
        }

        return written;
    }

    /// <summary>Pixel count above which a full-resolution export is worth warning about.</summary>
    public const long LargeExportPixels = 100_000_000;
}

using System.Collections;
using System.Reflection;

namespace HexGrid.App.Tests;

public class PropertyGridBoolOverlayTests
{
    [Fact]
    public void Constructor_NullGrid_Throws() => StaThread.Run(() =>
        Assert.Throws<ArgumentNullException>(() => new PropertyGridBoolOverlay(null!)));

    [Fact]
    public void StaticReflection_ResolvesPropertyGridViewInternals()
    {
        // Every reflected member the overlay depends on must still resolve on this .NET version -
        // PropertyGridView, its "_visibleRows" field, GetGridEntryFromRow and GetRectangle are all
        // internal implementation details with no public API. If a future SDK renames one, Sync()
        // degrades to a silent no-op (by design, see the class's own doc comment) rather than a
        // crash - which means this is the only place that would ever catch that regression.
        Assert.NotNull(GetOverlayStaticField("ViewType"));
        Assert.NotNull(GetOverlayStaticField("VisibleRowsField"));
        Assert.NotNull(GetOverlayStaticField("GetGridEntryFromRowMethod"));
        Assert.NotNull(GetOverlayStaticField("GetRectangleMethod"));
    }

    [Fact]
    public void FirstPaint_BoolProperty_CreatesVisibleCheckedCheckBoxMatchingItsValue() => StaThread.Run(() =>
    {
        // Arrange
        var target = new TestTarget { Flag = true };
        using PropertyGrid grid = NewGridWithHandle(target);
        using var overlay = new PropertyGridBoolOverlay(grid);

        // Act: PropertyGrid's own client area is fully covered by its child controls under normal
        // layout, so its Paint event essentially never fires via a real WM_PAINT/message pump - this
        // invokes the same protected OnPaint the real control would receive to exercise EnsureView()
        // and the first Sync() the same way production code does.
        SimulatePaint(grid);

        // Assert: exactly one checkbox exists, for the one bool property among Flag/Number.
        var pool = (IList)GetOverlayInstanceField(overlay, "_pool");
        Assert.Single(pool);
        var box = (CheckBox)pool[0]!;
        Assert.True(box.Visible);
        Assert.True(box.Checked);
    });

    [Fact]
    public void ToggleCheckBox_MutatesThePropertyAndRaisesToggled() => StaThread.Run(() =>
    {
        // Arrange
        var target = new TestTarget { Flag = true };
        using PropertyGrid grid = NewGridWithHandle(target);
        using var overlay = new PropertyGridBoolOverlay(grid);
        SimulatePaint(grid);
        var pool = (IList)GetOverlayInstanceField(overlay, "_pool");
        var box = (CheckBox)pool[0]!;
        bool toggledRaised = false;
        overlay.Toggled += (_, _) => toggledRaised = true;

        // Act
        box.Checked = false;

        // Assert
        Assert.False(target.Flag);
        Assert.True(toggledRaised);
    });

    [Fact]
    public void Sync_SelectedObjectCleared_HidesAllCheckBoxes() => StaThread.Run(() =>
    {
        // Arrange
        var target = new TestTarget { Flag = true };
        using PropertyGrid grid = NewGridWithHandle(target);
        using var overlay = new PropertyGridBoolOverlay(grid);
        SimulatePaint(grid);
        var pool = (IList)GetOverlayInstanceField(overlay, "_pool");
        var box = (CheckBox)pool[0]!;

        // Act: SelectedObject = null is the state MainForm never actually reaches in practice, but
        // Sync()'s own null guard (HideAll()) is real production logic worth pinning. Sync() is
        // invoked directly here rather than via a simulated Paint - PropertyGridView overrides OnPaint
        // without calling base, so its own Paint event cannot be triggered externally at all; that is
        // WinForms plumbing this test does not need to re-verify, only Sync()'s own branching.
        grid.SelectedObject = null;
        InvokeSync(overlay);

        // Assert
        Assert.False(box.Visible);
    });

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow() => StaThread.Run(() =>
    {
        // Arrange
        var target = new TestTarget { Flag = true };
        using PropertyGrid grid = NewGridWithHandle(target);
        var overlay = new PropertyGridBoolOverlay(grid);
        SimulatePaint(grid);

        // Act & Assert
        overlay.Dispose();
        overlay.Dispose();
    });

    private static PropertyGrid NewGridWithHandle(object selectedObject)
    {
        var grid = new PropertyGrid { Size = new Size(300, 400) };
        grid.SelectedObject = selectedObject;
        grid.CreateControl();
        return grid;
    }

    private static void SimulatePaint(Control control)
    {
        using var bmp = new Bitmap(Math.Max(1, control.Width), Math.Max(1, control.Height));
        using Graphics g = Graphics.FromImage(bmp);
        var args = new PaintEventArgs(g, new Rectangle(0, 0, control.Width, control.Height));
        MethodInfo onPaint = typeof(Control).GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!;
        onPaint.Invoke(control, [args]);
    }

    private static void InvokeSync(PropertyGridBoolOverlay overlay)
    {
        MethodInfo sync = typeof(PropertyGridBoolOverlay).GetMethod("Sync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        sync.Invoke(overlay, null);
    }

    private static object? GetOverlayStaticField(string name) =>
        typeof(PropertyGridBoolOverlay).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);

    private static object GetOverlayInstanceField(PropertyGridBoolOverlay overlay, string name)
    {
        FieldInfo field = typeof(PropertyGridBoolOverlay).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(PropertyGridBoolOverlay).Name, name);
        return field.GetValue(overlay) ?? throw new InvalidOperationException($"Field '{name}' was null.");
    }

    private sealed class TestTarget
    {
        public bool Flag { get; set; }

        public int Number { get; set; } = 42;
    }
}

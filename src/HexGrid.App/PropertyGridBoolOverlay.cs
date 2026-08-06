using System.ComponentModel;
using System.Drawing;
using System.Reflection;

namespace HexGrid.App;

/// <summary>
/// Overlays real, clickable <see cref="CheckBox"/> controls on the boolean rows of a
/// <see cref="PropertyGrid"/>. Stock PropertyGrid only offers a True/False dropdown for bool
/// properties — there is no public API to render or host an interactive control per row (confirmed
/// against the dotnet/winforms source: GridEntry and PropertyGridView are both internal, and GridItem
/// exposes no row-bounds member), so this reflects into PropertyGridView's row-layout internals.
/// Every reflected member is looked up once and cached; if a future .NET version renames or removes
/// one, the lookup returns null and <see cref="Sync"/> no-ops instead of throwing — the grid falls
/// back to its normal True/False dropdown, it never crashes the form.
/// </summary>
internal sealed class PropertyGridBoolOverlay : IDisposable
{
    // PropertyGridView.GetRectangle(row, flags)'s "value column" flag (private const RowValue = 2
    // in the current source). Hardcoded rather than reflected: it's a compiled-in literal, and it
    // could not change without breaking PropertyGridView's own row painting alongside it.
    private const int ValueColumnFlag = 2;

    private static readonly Type? ViewType;
    private static readonly FieldInfo? VisibleRowsField;
    private static readonly MethodInfo? GetGridEntryFromRowMethod;
    private static readonly MethodInfo? GetRectangleMethod;

    // _grid and _view are both borrowed references, not owned here: _grid is created and disposed by
    // MainForm, and _view is one of _grid's own child controls (found in EnsureView() below), disposed
    // recursively along with _grid. Neither belongs to this type, so this type does not dispose them.
#pragma warning disable SS066
    private readonly PropertyGrid _grid;
    private Control? _view;
#pragma warning restore SS066
    private readonly List<CheckBox> _pool = [];

    // PropertyGridView scrolls by blitting its own client pixels rather than repainting the whole
    // control (a normal perf optimisation for a plain Control, not ScrollableControl), so its Paint
    // event does not reliably fire for rows that only moved, not redrew. A quiet 150ms poll is a
    // reliable backstop for exactly that case: cheap (a handful of reflection calls over ~a dozen
    // visible rows), and correctness here does not depend on catching every single frame - just
    // settling to the right positions shortly after the grid stops moving. Paint-driven Sync() above
    // still covers the common cases (edits, selection, resize) instantly; this only catches scroll.
    private readonly System.Windows.Forms.Timer _resyncTimer = new() { Interval = 150 };

    private bool _disposed;
    private bool _loggedSyncFailure;

    /// <summary>Raised after a checkbox click commits a new value, mirroring PropertyValueChanged.</summary>
    public event EventHandler? Toggled;

    static PropertyGridBoolOverlay()
    {
        try
        {
            Type? viewType = Type.GetType(
                "System.Windows.Forms.PropertyGridInternal.PropertyGridView, System.Windows.Forms");
            if (viewType is null)
            {
                return;
            }

            // S3011: deliberate, narrow, and documented at the class level above - there is no public
            // API for a PropertyGrid row's bounds, and every lookup here is null-checked before use so
            // a member rename degrades to "no checkboxes" rather than an accessibility violation at
            // runtime.
#pragma warning disable S3011
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            ViewType = viewType;
            VisibleRowsField = viewType.GetField("_visibleRows", flags);
            GetGridEntryFromRowMethod = viewType.GetMethod("GetGridEntryFromRow", flags, [typeof(int)]);
            GetRectangleMethod = viewType.GetMethod("GetRectangle", flags, [typeof(int), typeof(int)]);
#pragma warning restore S3011
        }
        catch (Exception ex)
        {
            // Same fail-safe contract as a missing member: leave every static field null so Sync()
            // no-ops forever instead of the type initializer crashing the whole process. Logged once
            // here (unlike Sync()'s per-call catch below) since this only runs a single time and, if
            // it ever fires, means the reflected internals moved and this file needs a look.
            Program.WriteCrashLog(ex);
        }
    }

    public PropertyGridBoolOverlay(PropertyGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        _grid = grid;

        // PropertyGrid is itself a composite control: its constituent PropertyGridView child isn't
        // guaranteed to exist the moment _grid's own handle is created, only by the time it actually
        // paints (the OS won't deliver a paint for a control whose children aren't yet realized). So
        // this retries on every _grid.Paint - cheap once _view is found, since it then unsubscribes -
        // rather than trying exactly once against a single lifecycle event that may fire too early.
        _grid.Paint += OnGridPaint;
        _resyncTimer.Tick += (_, _) => Sync();
    }

    private void OnGridPaint(object? sender, PaintEventArgs e)
    {
        EnsureView();
        if (_view is not null)
        {
            _grid.Paint -= OnGridPaint;
        }
    }

    private void EnsureView()
    {
        if (_view is not null || ViewType is null)
        {
            return;
        }

        foreach (Control candidate in _grid.Controls)
        {
            if (!ViewType.IsInstanceOfType(candidate))
            {
                continue;
            }

            _view = candidate;
            _view.Paint += OnViewPaint;
            _resyncTimer.Start();
            Sync();
            return;
        }
    }

    private void OnViewPaint(object? sender, PaintEventArgs e) => Sync();

    private void Sync()
    {
        if (_disposed || _view is null ||
            VisibleRowsField is null || GetGridEntryFromRowMethod is null || GetRectangleMethod is null)
        {
            return;
        }

        object? selected = _grid.SelectedObject;
        if (selected is null)
        {
            HideAll();
            return;
        }

        try
        {
            int visibleRows = VisibleRowsField.GetValue(_view) is int n ? n : 0;
            int used = 0;

            for (int row = 0; row < visibleRows; row++)
            {
                if (SyncRow(row, GetGridEntryFromRowMethod, GetRectangleMethod, selected, used))
                {
                    used++;
                }
            }

            for (int i = used; i < _pool.Count; i++)
            {
                _pool[i].Visible = false;
            }
        }
        catch (TargetInvocationException ex)
        {
            // The reflected members exist but threw for this call - most likely a transient layout
            // state (mid-scroll, entry collection rebuilding). Skip this pass; the next Paint retries.
            // Logged once per overlay instance (not every occurrence, since Paint - and therefore this
            // catch, if something is genuinely wrong - can fire many times a second during a resize).
            if (!_loggedSyncFailure)
            {
                _loggedSyncFailure = true;
                Program.WriteCrashLog(ex);
            }
        }
    }

    /// <summary>Positions a checkbox for <paramref name="row"/> if it is a boolean property row.</summary>
    /// <returns>Whether a pooled checkbox at index <paramref name="poolIndex"/> was used for this row.</returns>
    private bool SyncRow(
        int row, MethodInfo getGridEntryFromRow, MethodInfo getRectangle, object selected, int poolIndex)
    {
        var item = getGridEntryFromRow.Invoke(_view, [row]) as GridItem;
        PropertyDescriptor? descriptor = item?.PropertyDescriptor;
        if (descriptor is null || descriptor.PropertyType != typeof(bool))
        {
            return false;
        }

        if (getRectangle.Invoke(_view, [row, ValueColumnFlag]) is not Rectangle rect ||
            rect.Width <= 0 || rect.Height <= 0)
        {
            return false;
        }

        // IDISP001: CreatePooled() hands ownership straight to _view.Controls (see its own body), the
        // same pattern MainForm uses for _properties/_preview/_status - the parent control tree
        // disposes it, not this local.
#pragma warning disable IDISP001
        CheckBox box = poolIndex < _pool.Count ? _pool[poolIndex] : CreatePooled();
#pragma warning restore IDISP001
        PositionAndSync(box, descriptor, selected, rect);
        return true;
    }

    private void PositionAndSync(CheckBox box, PropertyDescriptor descriptor, object selected, Rectangle rect)
    {
        box.Tag = descriptor;

        // The value came from the grid's own state here, not a user click - detach the handler so
        // setting Checked doesn't immediately re-fire OnCheckBoxCheckedChanged and write it right back.
        box.CheckedChanged -= OnCheckBoxCheckedChanged;
        box.Checked = descriptor.GetValue(selected) is true;
        box.CheckedChanged += OnCheckBoxCheckedChanged;

        // Spans the whole value cell (not just a small glyph-sized square): the grid still paints its
        // own "True"/"False" text and dropdown arrow underneath, unaware it's covered, so anything
        // narrower than the full cell leaves fragments of that text peeking out past the checkbox.
        box.Bounds = rect;
        box.Visible = true;
        box.BringToFront();
    }

    private CheckBox CreatePooled()
    {
        var box = new CheckBox
        {
            AutoSize = false,
            TabStop = false,
            BackColor = SystemColors.Window,
            CheckAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0),
        };
        box.CheckedChanged += OnCheckBoxCheckedChanged;
        _pool.Add(box);
        _view!.Controls.Add(box);
        return box;
    }

    private void OnCheckBoxCheckedChanged(object? sender, EventArgs e)
    {
        if (sender is not CheckBox { Tag: PropertyDescriptor descriptor } box)
        {
            return;
        }

        object? selected = _grid.SelectedObject;
        if (selected is null)
        {
            return;
        }

        descriptor.SetValue(selected, box.Checked);
        _grid.Refresh();
        Toggled?.Invoke(this, EventArgs.Empty);
    }

    private void HideAll()
    {
        foreach (CheckBox box in _pool)
        {
            box.Visible = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _resyncTimer.Dispose();
        _grid.Paint -= OnGridPaint;
        if (_view is not null)
        {
            _view.Paint -= OnViewPaint;
        }
    }
}

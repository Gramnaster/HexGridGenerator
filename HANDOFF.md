# WinForms gotchas — HexGrid Generator

Historical root-cause notes for bugs hit while building `HexGrid.App`. Not a live status doc:
the crash below was fixed on 2026-08-05, and `HexGrid.App.Tests` / `HexGrid.Core.Tests` (108
tests total) now cover the regressions this file used to guard against by hand. Kept because the
root cause is a real WinForms trap that is easy to reintroduce. Read the SplitContainer section
before touching `MainForm.BuildUi()`.

## SplitContainer construction-order crash

`hexgrid-crash.log` (and the debugger, independently) gave:

```
System.InvalidOperationException: SplitterDistance must be between Panel1MinSize and Width - Panel2MinSize.
   at System.Windows.Forms.SplitContainer.set_SplitterDistance(Int32 value)
   at System.Windows.Forms.SplitContainer.ApplyPanel2MinSize(Int32 value)
   at HexGrid.App.MainForm.BuildUi() in MainForm.cs:line 60
   at HexGrid.App.MainForm..ctor() in MainForm.cs:line 32
```

The throw is inside the `SplitContainer` object initializer itself, at `Panel2MinSize = 200`.
Setting `Panel2MinSize` fires `SplitContainer`'s internal `ApplyPanel2MinSize`, which recomputes
`SplitterDistance` immediately, before the control is parented, so `Dock = DockStyle.Fill` has
not had a chance to size it yet. Its default un-parented `Width` is far smaller than
`Panel1MinSize (300) + Panel2MinSize (200) + SplitterWidth (6) = 506`, so the recompute is
mathematically impossible and throws on the spot.

**Fix:** set `Width` explicitly before `Panel1MinSize`/`Panel2MinSize` in the `SplitContainer`
initializer, so the invariant holds at construction time. `Dock = Fill` still takes over once
the control is parented and the form lays out for real; the explicit width only needs to survive
the object initializer.

## Other WinForms gotchas hit in this codebase

- **`TableLayoutPanel` with a single column collapses the window** unless a `ColumnStyle` is
  added explicitly (`root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100))`). Without
  it, the column defaults to `AutoSize`.
- **`StringFormat.GenericTypographic` allocates a fresh disposable GDI+ object on every access.**
  `SceneRasterizer` caches one instance instead of reading the property per text item; reading it
  per item was leaking handles across a grid with a label in every hex.

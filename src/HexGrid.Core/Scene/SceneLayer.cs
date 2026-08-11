namespace HexGrid.Core.Scene;

public sealed class SceneLayer(LayerKind kind, string name)
{
    public LayerKind Kind { get; } = kind;

    public string Name { get; } = name;

    // MA0016: expose the collection abstraction, not the concrete List<T>, to callers outside this
    // assembly. IList<T> (not IReadOnlyList<T>) because SceneBuilder appends items through this
    // property directly (layer.Items.Add(...)) - narrowing further would force a separate mutation
    // method for no behavioural gain.
    public IList<IDrawItem> Items { get; } = new List<IDrawItem>();

    public bool IsEmpty => Items.Count == 0;
}

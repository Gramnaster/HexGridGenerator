using System.ComponentModel;

namespace HexGrid.Core;

/// <summary>
/// Wraps a <see cref="PropertyDescriptor"/> to override its PropertyGrid display text, forwarding
/// every read/write operation to <paramref name="inner"/> unchanged. Lets one GridSettings property
/// read "Hex fill colour" in hex mode and "Square fill colour" in square mode without being two
/// separate properties with two separate JSON keys.
/// </summary>
internal sealed class RelabelledPropertyDescriptor(
    PropertyDescriptor inner, string? displayName, string? description, string? category)
    : PropertyDescriptor(inner)
{
    public override string DisplayName => displayName ?? inner.DisplayName;

    public override string Description => description ?? inner.Description;

    public override string Category => category ?? inner.Category;

    public override Type ComponentType => inner.ComponentType;

    public override bool IsReadOnly => inner.IsReadOnly;

    public override Type PropertyType => inner.PropertyType;

    public override bool CanResetValue(object component) => inner.CanResetValue(component);

    public override object? GetValue(object? component) => inner.GetValue(component);

    public override void ResetValue(object component) => inner.ResetValue(component);

    public override void SetValue(object? component, object? value) => inner.SetValue(component, value);

    public override bool ShouldSerializeValue(object component) => inner.ShouldSerializeValue(component);
}

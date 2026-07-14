using Orivy.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Orivy.Studio.Canvas;

/// <summary>
/// Multi-selection state for the design canvas. The primary item is the anchor used for the
/// property inspector and single-selection operations (resize grips).
/// </summary>
public sealed class SelectionService
{
    private readonly List<ElementBase> _items = new();

    public event Action? Changed;

    public IReadOnlyList<ElementBase> Items => _items;
    public ElementBase? Primary => _items.Count > 0 ? _items[^1] : null;
    public int Count => _items.Count;

    public bool Contains(ElementBase control) => _items.Contains(control);

    public void SelectOnly(ElementBase? control)
    {
        if (control == null)
        {
            Clear();
            return;
        }

        if (_items.Count == 1 && ReferenceEquals(_items[0], control))
            return;

        _items.Clear();
        _items.Add(control);
        Changed?.Invoke();
    }

    public void Toggle(ElementBase control)
    {
        if (!_items.Remove(control))
            _items.Add(control);
        Changed?.Invoke();
    }

    public void SetMany(IEnumerable<ElementBase> controls)
    {
        _items.Clear();
        _items.AddRange(controls.Distinct());
        Changed?.Invoke();
    }

    public void Remove(ElementBase control)
    {
        if (_items.Remove(control))
            Changed?.Invoke();
    }

    public void Clear()
    {
        if (_items.Count == 0)
            return;
        _items.Clear();
        Changed?.Invoke();
    }
}

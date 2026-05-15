using System;
using System.Collections;
using System.Collections.Generic;

namespace Orivy.Collections;

public sealed class ItemCollection<T> : IReadOnlyList<T>
{
    private readonly List<T> _items = new();
    private readonly Action _onChanged;

    public ItemCollection(Action onChanged) => _onChanged = onChanged;

    public int Count => _items.Count;

    public T this[int index] => _items[index];

    public void Add(T item)
    {
        _items.Add(item);
        _onChanged();
    }

    public void Remove(T item)
    {
        if (_items.Remove(item))
            _onChanged();
    }

    public void Clear()
    {
        if (_items.Count == 0)
            return;

        _items.Clear();
        _onChanged();
    }

    public void AddRange(IEnumerable<T> items)
    {
        _items.AddRange(items);
        _onChanged();
    }

    public bool Contains(T item) => _items.Contains(item);

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    public void ReplaceAll(IEnumerable<T> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _onChanged();
    }
}

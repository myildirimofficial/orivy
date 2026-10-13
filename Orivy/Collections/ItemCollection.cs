using System;
using System.Collections;
using System.Collections.Generic;

namespace Orivy.Collections;

/// <summary>Mutable item list used by controls such as <c>ButtonGroup</c>. Implements
/// non-generic <see cref="IList"/> so Studio's PropertyGrid can add/remove/reorder rows.</summary>
public sealed class ItemCollection<T> : IList<T>, IList
{
    private readonly List<T> _items = new();
    private readonly Action _onChanged;

    public ItemCollection(Action onChanged) => _onChanged = onChanged;

    public int Count => _items.Count;

    public bool IsReadOnly => false;

    public T this[int index]
    {
        get => _items[index];
        set
        {
            _items[index] = value;
            _onChanged();
        }
    }

    public void Add(T item)
    {
        _items.Add(item);
        _onChanged();
    }

    public void Insert(int index, T item)
    {
        _items.Insert(index, item);
        _onChanged();
    }

    public bool Remove(T item)
    {
        if (!_items.Remove(item))
            return false;

        _onChanged();
        return true;
    }

    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
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

    public int IndexOf(T item) => _items.IndexOf(item);

    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    public void ReplaceAll(IEnumerable<T> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _onChanged();
    }

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    #region IList

    object? IList.this[int index]
    {
        get => _items[index];
        set => this[index] = Unbox(value);
    }

    bool IList.IsFixedSize => false;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => ((ICollection)_items).SyncRoot;

    int IList.Add(object? value)
    {
        Add(Unbox(value));
        return _items.Count - 1;
    }

    bool IList.Contains(object? value) => value is T typed && Contains(typed);

    int IList.IndexOf(object? value) => value is T typed ? IndexOf(typed) : -1;

    void IList.Insert(int index, object? value) => Insert(index, Unbox(value));

    void IList.Remove(object? value)
    {
        if (value is T typed)
            Remove(typed);
    }

    void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

    private static T Unbox(object? value)
    {
        if (value is null)
        {
            if (default(T) is null)
                return default!;
            throw new ArgumentNullException(nameof(value));
        }

        if (value is T typed)
            return typed;

        if (typeof(T) == typeof(string))
            return (T)(object)(value.ToString() ?? string.Empty);

        return (T)Convert.ChangeType(value, typeof(T));
    }

    #endregion
}

using System;

namespace Orivy;

public sealed class GridListItemReorderedEventArgs : EventArgs
{
    public GridListItemReorderedEventArgs(int oldIndex, int newIndex)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    public int OldIndex { get; }
    public int NewIndex { get; }
}

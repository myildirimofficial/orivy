using Orivy.Controls;
using System;

namespace Orivy;

public sealed class GridListColumnClickEventArgs : EventArgs
{
    public GridListColumnClickEventArgs(GridListColumn column, int columnIndex, GridListSortDirection sortDirection)
    {
        Column = column;
        ColumnIndex = columnIndex;
        SortDirection = sortDirection;
    }

    public GridListColumn Column { get; }
    public int ColumnIndex { get; }
    public GridListSortDirection SortDirection { get; }
}

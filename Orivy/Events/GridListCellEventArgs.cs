using Orivy.Controls;
using System;

namespace Orivy;

public class GridListCellEventArgs : EventArgs
{
    public GridListCellEventArgs(GridListItem item, GridListColumn column, GridListCell cell, int itemIndex, int columnIndex)
    {
        Item = item;
        Column = column;
        Cell = cell;
        ItemIndex = itemIndex;
        ColumnIndex = columnIndex;
    }

    public GridListItem Item { get; }
    public GridListColumn Column { get; }
    public GridListCell Cell { get; }
    public int ItemIndex { get; }
    public int ColumnIndex { get; }
}

using Orivy.Controls;

namespace Orivy;

public sealed class GridListCellCheckChangedEventArgs : GridListCellEventArgs
{
    public GridListCellCheckChangedEventArgs(
        GridListItem item,
        GridListColumn column,
        GridListCell cell,
        int itemIndex,
        int columnIndex,
        CheckState previousState,
        CheckState currentState)
        : base(item, column, cell, itemIndex, columnIndex)
    {
        PreviousState = previousState;
        CurrentState = currentState;
    }

    public CheckState PreviousState { get; }
    public CheckState CurrentState { get; }
}

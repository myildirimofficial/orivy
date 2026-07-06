using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orivy;

public sealed class GridListSelectionChangedEventArgs : EventArgs
{
    public GridListSelectionChangedEventArgs(int previousSelectedIndex, int selectedIndex)
    {
        PreviousSelectedIndex = previousSelectedIndex;
        SelectedIndex = selectedIndex;
    }

    public int PreviousSelectedIndex { get; }
    public int SelectedIndex { get; }
}

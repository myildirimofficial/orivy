using System;
using System.Collections.Generic;

namespace Orivy.Studio.History;

/// <summary>A reversible designer operation.</summary>
public interface IDesignerCommand
{
    string Label { get; }
    void Do();
    void Undo();
}

/// <summary>
/// Central undo/redo stack. Every mutation of the design document goes through
/// <see cref="Execute"/> so the full session is reversible.
/// </summary>
public sealed class CommandStack
{
    private const int Capacity = 200;

    private readonly List<IDesignerCommand> _undo = new();
    private readonly List<IDesignerCommand> _redo = new();

    /// <summary>Raised after any Execute/Undo/Redo/Clear so the UI can refresh button states.</summary>
    public event Action? Changed;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public string? UndoLabel => CanUndo ? _undo[^1].Label : null;
    public string? RedoLabel => CanRedo ? _redo[^1].Label : null;

    /// <summary>Records an ALREADY-APPLIED command (e.g. after an interactive drag) without running Do().</summary>
    public void Push(IDesignerCommand command)
    {
        _undo.Add(command);
        if (_undo.Count > Capacity)
            _undo.RemoveAt(0);
        _redo.Clear();
        Changed?.Invoke();
    }

    public void Execute(IDesignerCommand command)
    {
        command.Do();
        _undo.Add(command);
        if (_undo.Count > Capacity)
            _undo.RemoveAt(0);
        _redo.Clear();
        Changed?.Invoke();
    }

    public void Undo()
    {
        if (!CanUndo)
            return;

        var command = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        command.Undo();
        _redo.Add(command);
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo)
            return;

        var command = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        command.Do();
        _undo.Add(command);
        Changed?.Invoke();
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke();
    }
}

/// <summary>General-purpose command built from do/undo delegates.</summary>
public sealed class DelegateCommand : IDesignerCommand
{
    private readonly Action _do;
    private readonly Action _undo;

    public DelegateCommand(string label, Action @do, Action undo)
    {
        Label = label;
        _do = @do;
        _undo = undo;
    }

    public string Label { get; }
    public void Do() => _do();
    public void Undo() => _undo();
}

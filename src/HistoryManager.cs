namespace Nitemare3D.ImgEditor;

public sealed class HistoryManager
{
    private sealed record State(int Index, ImgEntry Snapshot);

    private readonly Stack<State> _undo = new();
    private readonly Stack<State> _redo = new();
    private const int Limit = 100;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    public void PushUndo(int index, ImgEntry before)
    {
        _undo.Push(new State(index, before.Clone()));
        while (_undo.Count > Limit)
        {
            var a = _undo.Reverse().Skip(1).ToArray();
            _undo.Clear();
            foreach (var s in a)
                _undo.Push(s);
        }
        _redo.Clear();
    }

    public int Undo(IList<ImgEntry> entries)
    {
        if (_undo.Count == 0)
            return -1;

        State s = _undo.Pop();
        if (s.Index < 0 || s.Index >= entries.Count)
            return -1;

        _redo.Push(new State(s.Index, entries[s.Index].Clone()));
        entries[s.Index].CopyFrom(s.Snapshot);
        return s.Index;
    }

    public int Redo(IList<ImgEntry> entries)
    {
        if (_redo.Count == 0)
            return -1;

        State s = _redo.Pop();
        if (s.Index < 0 || s.Index >= entries.Count)
            return -1;

        _undo.Push(new State(s.Index, entries[s.Index].Clone()));
        entries[s.Index].CopyFrom(s.Snapshot);
        return s.Index;
    }
}

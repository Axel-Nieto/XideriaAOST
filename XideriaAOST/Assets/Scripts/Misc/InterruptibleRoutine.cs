using System;
using System.Collections;
using System.Collections.Generic;

// Own the nested iterators so an interrupted action executes its finally blocks,
// including movement/animation cleanup, even while Unity is waiting on a delay.
public sealed class InterruptibleRoutine : IEnumerator, IDisposable
{
    private readonly Stack<IEnumerator> stack = new();
    private bool advancing;
    private bool cancelled;
    public object Current { get; private set; }
    public bool IsCancelled => cancelled;

    public InterruptibleRoutine(IEnumerator routine)
    {
        if (routine != null) stack.Push(routine);
    }

    public bool MoveNext()
    {
        if (cancelled) { DisposeStack(); return false; }
        // TimeScale does not suspend iterators yielding null or nested actions.
        if (PauseMenuUI.IsPaused) { Current = null; return true; }
        advancing = true;
        try
        {
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                bool next = top.MoveNext();
                if (cancelled) return false;
                if (!next)
                {
                    stack.Pop();
                    (top as IDisposable)?.Dispose();
                    continue;
                }
                if (top.Current is IEnumerator child) { stack.Push(child); continue; }
                Current = top.Current;
                return true;
            }
            return false;
        }
        finally
        {
            advancing = false;
            if (cancelled) DisposeStack();
        }
    }

    public void Cancel()
    {
        cancelled = true;
        if (!advancing) DisposeStack();
    }

    public void Dispose() => Cancel();
    public void Reset() => throw new NotSupportedException();
    private void DisposeStack()
    {
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        Current = null;
    }
}

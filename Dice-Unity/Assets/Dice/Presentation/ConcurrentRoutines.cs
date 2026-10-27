using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * Runs several presentation routines side by side inside one coroutine and ends when all have ended. Each step advances
     * every running routine once, entering the routines it yields as Unity does; null and CustomYieldInstruction waits
     * such as WaitForSecondsRealtime hold a routine for a frame at a time. Disposing it disposes every routine still running.
     */
    public sealed class ConcurrentRoutines : IEnumerator, IDisposable
    {
        private readonly List<Stack<IEnumerator>> running = new List<Stack<IEnumerator>>();

        public ConcurrentRoutines(IEnumerable<IEnumerator> routines)
        {
            if (routines == null) throw new ArgumentNullException(nameof(routines));
            foreach (var routine in routines)
            {
                if (routine == null) throw new ArgumentException("Concurrent routines cannot be null.", nameof(routines));
                var stack = new Stack<IEnumerator>();
                stack.Push(routine);
                running.Add(stack);
            }
        }

        /** Always null: between steps the routines wait for the next frame. */
        public object Current => null;

        /** Advances every running routine by one step; false once all have ended. */
        public bool MoveNext()
        {
            // Routines advance in the order they were given, so their side effects keep that order within a step.
            int kept = 0;
            for (int i = 0; i < running.Count; ++i)
                if (Step(running[i])) running[kept++] = running[i];
            running.RemoveRange(kept, running.Count - kept);
            return running.Count > 0;
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose()
        {
            foreach (var stack in running)
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            running.Clear();
        }

        /** Runs the routine until it waits; false when it has ended. */
        private static bool Step(Stack<IEnumerator> stack)
        {
            while (stack.Count > 0)
            {
                var routine = stack.Peek();
                if (!routine.MoveNext())
                {
                    // A finished nested routine resumes its parent within the same step.
                    stack.Pop();
                    continue;
                }
                object current = routine.Current;
                if (current == null) return true;
                if (current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                // Other yield instructions are scheduled by Unity itself and cannot be advanced here.
                throw new NotSupportedException("Concurrent presentation routines may yield only null and IEnumerator waits, not " +
                    current.GetType().Name + ".");
            }
            return false;
        }
    }
}

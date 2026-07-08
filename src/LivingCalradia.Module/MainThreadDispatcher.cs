using System;
using System.Collections.Concurrent;

namespace LivingCalradia
{
    /// <summary>
    /// LLM calls complete on background threads, but TaleWorlds UI (inquiries, messages)
    /// must be touched from the game thread. Actions queued here are drained on campaign tick.
    /// </summary>
    public static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();

        public static void Enqueue(Action action)
        {
            if (action != null) Queue.Enqueue(action);
        }

        public static void Drain()
        {
            while (Queue.TryDequeue(out var action))
            {
                try { action(); }
                catch { }
            }
        }
    }
}

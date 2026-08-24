using System;
using System.Collections.Generic;

namespace Spades.Core.Events
{
    /// <summary>
    /// The buffer between a core that resolves a trick in microseconds and a view that needs
    /// eight hundred milliseconds of cards flying to show what happened.
    ///
    /// It is also exactly the buffer a networked build needs to absorb latency, which is what
    /// makes the "multiplayer ready" claim concrete rather than aspirational.
    /// </summary>
    public sealed class GameEventQueue
    {
        private readonly Queue<IGameEvent> _queue = new Queue<IGameEvent>(64);

        public int Count => _queue.Count;

        public void Enqueue(IGameEvent gameEvent)
        {
            if (gameEvent == null) throw new ArgumentNullException(nameof(gameEvent));
            _queue.Enqueue(gameEvent);
        }

        public bool TryDequeue(out IGameEvent gameEvent)
        {
            if (_queue.Count == 0)
            {
                gameEvent = null;
                return false;
            }

            gameEvent = _queue.Dequeue();
            return true;
        }

        public void Clear() => _queue.Clear();
    }
}

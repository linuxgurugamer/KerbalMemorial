using System;
using System.Collections.Generic;

namespace KerbalMemorial
{
    // Scene/save-local presentation queue. This is not another casualty ledger.
    // Keep an ID until its plaque is acknowledged; duplicate KSP events cannot
    // re-enqueue it, even after the player has closed that plaque.
    internal sealed class FatalityPlaqueQueue
    {
        private sealed class Entry { internal string Id; internal double ReadyAt; }
        private readonly Queue<Entry> pending = new Queue<Entry>();
        internal const float DefaultDelay = 1.5f;
        internal const float MinDelay = 0f, MaxDelay = 5f;
        internal static double ClampDelay(double delay)
        {
            return double.IsNaN(delay) ? DefaultDelay : Math.Max(MinDelay, Math.Min(MaxDelay, delay));
        }
        private readonly HashSet<string> accepted = new HashSet<string>(StringComparer.Ordinal);
        internal int Count => pending.Count;
        internal string Current => pending.Count == 0 ? null : pending.Peek().Id;

        internal bool Observe(Casualty record, bool wasFatal, bool imported, double now = 0, bool enabled = true, double delay = DefaultDelay)
        {
            if (!enabled || record == null || !record.Fatal || wasFatal || imported ||
                string.IsNullOrEmpty(record.Id) || !accepted.Add(record.Id)) return false;
            pending.Enqueue(new Entry { Id = record.Id, ReadyAt = now + ClampDelay(delay) });
            return true;
        }
        internal bool IsReady(double now) => pending.Count > 0 && now >= pending.Peek().ReadyAt;
        internal void Clear() { pending.Clear(); }
        internal bool Acknowledge(string id)
        {
            if (pending.Count == 0 || pending.Peek().Id != id) return false;
            pending.Dequeue();
            return true;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 要求が届くたびに TeamDeathmatchSession.TryRevive を呼ばないのは、要求の届く間隔とサーバーのフレームの間隔が
    // そろわないため。届かなかったフレームがあると Session は累計を 0 に戻してしまい、まとめて届いたフレームでは 2 度目以降が数えられない。
    // ここで押している間を覚えておき、サーバーのフレームごとに 1 度だけ、そのフレームの秒数で進める。
    // 進みはサーバーの時間で決まるので、要求を速く送っても速くは起こせない。
    public sealed class ReviveHoldTable
    {
        // 要求は tick (1/60 秒) ごとに来るので、
        // 届く間隔が揺れても途切れたことにしない程度に長く、離してから止まるまでが目立たない程度に短くする。
        public const double DefaultGraceSeconds = 0.2d;

        // 起こす人の id の昇順に回して、同じ入力なら同じ順に進める
        private readonly SortedDictionary<int, Entry> _entries = new SortedDictionary<int, Entry>();

        public int Count => _entries.Count;

        public void Hold(int reviverId, int targetId, double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now))
            {
                throw new ArgumentOutOfRangeException(nameof(now), now, "now must be finite");
            }

            _entries[reviverId] = new Entry(targetId, now);
        }

        public bool TryGetTarget(int reviverId, out int targetId)
        {
            if (_entries.TryGetValue(reviverId, out var entry))
            {
                targetId = entry.TargetId;
                return true;
            }

            targetId = 0;
            return false;
        }

        public bool Release(int reviverId) => _entries.Remove(reviverId);

        public void RemovePlayer(int playerId)
        {
            List<int> removed = null;
            foreach (var pair in _entries)
            {
                if (pair.Key == playerId || pair.Value.TargetId == playerId)
                {
                    (removed ??= new List<int>()).Add(pair.Key);
                }
            }

            if (removed == null)
            {
                return;
            }

            foreach (var reviverId in removed)
            {
                _entries.Remove(reviverId);
            }
        }

        public void Clear() => _entries.Clear();

        public IReadOnlyList<ReviveHold> TakeActive(double now, double graceSeconds)
        {
            if (double.IsNaN(now) || double.IsInfinity(now))
            {
                throw new ArgumentOutOfRangeException(nameof(now), now, "now must be finite");
            }

            if (!(graceSeconds >= 0d) || double.IsInfinity(graceSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(graceSeconds), graceSeconds, "graceSeconds must not be negative and must be finite");
            }

            List<int> expired = null;
            var active = new List<ReviveHold>();
            foreach (var pair in _entries)
            {
                if (now - pair.Value.LastHeldAt > graceSeconds)
                {
                    (expired ??= new List<int>()).Add(pair.Key);
                }
                else
                {
                    active.Add(new ReviveHold(pair.Key, pair.Value.TargetId));
                }
            }

            if (expired != null)
            {
                foreach (var reviverId in expired)
                {
                    _entries.Remove(reviverId);
                }
            }

            return active;
        }

        private readonly struct Entry
        {
            public Entry(int targetId, double lastHeldAt)
            {
                TargetId = targetId;
                LastHeldAt = lastHeldAt;
            }

            public int TargetId { get; }

            public double LastHeldAt { get; }
        }
    }

    public readonly struct ReviveHold : IEquatable<ReviveHold>
    {
        public ReviveHold(int reviverId, int targetId)
        {
            ReviverId = reviverId;
            TargetId = targetId;
        }

        public int ReviverId { get; }

        public int TargetId { get; }

        public bool Equals(ReviveHold other) => ReviverId == other.ReviverId && TargetId == other.TargetId;

        public override bool Equals(object obj) => obj is ReviveHold other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ReviverId, TargetId);

        public static bool operator ==(ReviveHold left, ReviveHold right) => left.Equals(right);

        public static bool operator !=(ReviveHold left, ReviveHold right) => !left.Equals(right);

        public override string ToString() => "reviver=" + ReviverId + " target=" + TargetId;
    }
}

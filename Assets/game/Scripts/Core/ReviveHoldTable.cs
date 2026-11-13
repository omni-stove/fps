using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// サーバーで、誰が誰を起こそうと押し続けているかを持つ。クライアントは押している間 tick ごとに要求を送り、
    /// サーバーは届くたびに <see cref="Hold"/> で時刻を更新する。<see cref="TakeActive"/> は最後に届いてから
    /// 猶予の秒数を過ぎた要求を捨て、残りを返す。
    /// </summary>
    /// <remarks>
    /// 要求が届くたびに <see cref="TeamDeathmatchSession.TryRevive"/> を呼ばないのは、要求の届く間隔とサーバーのフレームの間隔が
    /// そろわないため。届かなかったフレームがあると Session は累計を 0 に戻してしまい、まとめて届いたフレームでは 2 度目以降が数えられない。
    /// ここで押している間を覚えておき、サーバーのフレームごとに 1 度だけ、そのフレームの秒数で進める。
    /// 進みはサーバーの時間で決まるので、要求を速く送っても速くは起こせない。
    /// </remarks>
    public sealed class ReviveHoldTable
    {
        /// <summary>
        /// 最後の要求からこの秒数のうちは押し続けているとみなす既定値。要求は tick (1/60 秒) ごとに来るので、
        /// 届く間隔が揺れても途切れたことにしない程度に長く、離してから止まるまでが目立たない程度に短くする。
        /// </summary>
        public const double DefaultGraceSeconds = 0.2d;

        // 起こす人の id の昇順に回して、同じ入力なら同じ順に進める
        private readonly SortedDictionary<int, Entry> _entries = new SortedDictionary<int, Entry>();

        /// <summary>押し続けている起こす人の数。</summary>
        public int Count => _entries.Count;

        /// <summary>reviverId が targetId を起こす要求が now に届いた。前に別の人を起こしていたら置き換える。</summary>
        /// <exception cref="ArgumentOutOfRangeException">now が有限値でない。</exception>
        public void Hold(int reviverId, int targetId, double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now))
            {
                throw new ArgumentOutOfRangeException(nameof(now), now, "now must be finite");
            }

            _entries[reviverId] = new Entry(targetId, now);
        }

        /// <summary>reviverId が起こしている相手。押していなければ false。</summary>
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

        /// <summary>reviverId の要求を捨てる。押していたら true。</summary>
        public bool Release(int reviverId) => _entries.Remove(reviverId);

        /// <summary>playerId が起こす人か起こされる人になっている要求をすべて捨てる (抜けたとき)。</summary>
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

        /// <summary>すべての要求を捨てる (試合の始め直し)。</summary>
        public void Clear() => _entries.Clear();

        /// <summary>
        /// 最後に届いてから graceSeconds を過ぎた要求を捨て、残った (起こす人, 起こされる人) を起こす人の id の昇順で返す。
        /// </summary>
        /// <param name="now">今の時刻。<see cref="Hold"/> と同じ時計の秒。</param>
        /// <param name="graceSeconds">押し続けているとみなす秒数。0 以上の有限値。</param>
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

    /// <summary><see cref="ReviveHoldTable.TakeActive"/> が返す、押し続けている蘇生の 1 組。</summary>
    public readonly struct ReviveHold : IEquatable<ReviveHold>
    {
        public ReviveHold(int reviverId, int targetId)
        {
            ReviverId = reviverId;
            TargetId = targetId;
        }

        /// <summary>起こす人。</summary>
        public int ReviverId { get; }

        /// <summary>起こされる人。</summary>
        public int TargetId { get; }

        public bool Equals(ReviveHold other) => ReviverId == other.ReviverId && TargetId == other.TargetId;

        public override bool Equals(object obj) => obj is ReviveHold other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ReviverId, TargetId);

        public static bool operator ==(ReviveHold left, ReviveHold right) => left.Equals(right);

        public static bool operator !=(ReviveHold left, ReviveHold right) => !left.Equals(right);

        public override string ToString() => "reviver=" + ReviverId + " target=" + TargetId;
    }
}

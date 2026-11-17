using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    /// <summary>
    /// 1 人が付けた印 (<see cref="PingMarker"/>) の一覧。付ける・敵を追う・寿命で消す・数の上限を受け持つ。時刻は呼び出し側が渡す。
    /// <list type="bullet">
    /// <item>印は付けてから <see cref="PingSpec.LifetimeSeconds"/> 秒で消える (<see cref="Expire"/>)。</item>
    /// <item><see cref="PingSpec.MaxMarkers"/> を超えて付けると、最も古い印を消す。</item>
    /// <item>同じ敵にもう一度付けると、増やさずにその印を付け直す (寿命を延ばし、最も新しい印にする)。</item>
    /// <item>敵の印は見えている間だけ位置を追い、見失うと最後に見た位置に残って、付け直すまで追わない。敵が倒れたら消す。</item>
    /// <item><see cref="Reveal"/> で付けた敵の印 (UAV など) は、秒数を呼び出し側が決め、数の上限に数えず、消える時まで見えていなくても追う
    /// (<see cref="IsRevealed"/>)。</item>
    /// </list>
    /// </summary>
    public sealed class PingBoard
    {
        // 古い順。上限で消すのは先頭。
        private readonly List<PingMarker> _markers = new List<PingMarker>();
        private readonly HashSet<int> _revealed = new HashSet<int>();
        private int _nextId = 1;

        public PingBoard(PingSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public PingSpec Spec { get; }

        /// <summary>今ある印。古い順。</summary>
        public IReadOnlyList<PingMarker> Markers => _markers;

        public int Count => _markers.Count;

        /// <summary>
        /// hit に印を付ける。敵の印には targetKey (敵を表す鍵) が要り、場所の印には要らない。
        /// </summary>
        /// <param name="hit">付けるもの。<see cref="PingAim.Resolve"/> の結果。</param>
        /// <param name="targetKey">敵の印なら敵を表す鍵。場所の印なら null。</param>
        /// <param name="now">今の時刻 (秒)。有限の値。</param>
        /// <exception cref="ArgumentException">hit が何も無いか、hit の種類と targetKey の有無が合わない。</exception>
        public PingPlacement Place(PingHit hit, int? targetKey, float now)
        {
            RequireFinite(now, nameof(now));
            if (!hit.HasTarget)
            {
                throw new ArgumentException("hit has nothing to mark", nameof(hit));
            }

            if (!PingAim.IsFinite(hit.Position))
            {
                throw new ArgumentException("hit position must be finite", nameof(hit));
            }

            if (hit.Kind == PingKind.Enemy && !targetKey.HasValue)
            {
                throw new ArgumentException("an enemy mark needs a target key", nameof(targetKey));
            }

            if (hit.Kind == PingKind.Location && targetKey.HasValue)
            {
                throw new ArgumentException("a location mark must not have a target key", nameof(targetKey));
            }

            var expiresAt = now + Spec.LifetimeSeconds;
            var tracking = hit.Kind == PingKind.Enemy;
            if (targetKey.HasValue && TryRefresh(targetKey.Value, hit.Position, now, expiresAt, out var refreshed))
            {
                return new PingPlacement(refreshed, true, null);
            }

            PingMarker? replaced = null;
            if (CountLimited() >= Spec.MaxMarkers)
            {
                var oldest = IndexOfOldestLimited();
                replaced = _markers[oldest];
                _markers.RemoveAt(oldest);
            }

            var marker = new PingMarker(_nextId++, hit.Kind, targetKey, hit.Position, tracking, now, expiresAt);
            _markers.Add(marker);
            return new PingPlacement(marker, false, replaced);
        }

        /// <summary>
        /// targetKey の敵に、lifetimeSeconds 秒で消える敵の印を付ける。この印は数の上限に数えず、他の印を消さない。
        /// 消える時まで <see cref="IsRevealed"/> が true になり、付けた人から見えていなくても追う。
        /// 同じ敵に印があれば増やさずに付け直し、今より遅く消える予定ならその時刻を残す。
        /// </summary>
        /// <param name="targetKey">敵を表す鍵。</param>
        /// <param name="position">敵の今の位置。有限の値。</param>
        /// <param name="now">今の時刻 (秒)。有限の値。</param>
        /// <param name="lifetimeSeconds">消えるまでの秒数。0 より大きい有限の値。</param>
        public PingPlacement Reveal(int targetKey, Vector3 position, float now, float lifetimeSeconds)
        {
            RequireFinite(now, nameof(now));
            if (!PingAim.IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (float.IsNaN(lifetimeSeconds) || float.IsInfinity(lifetimeSeconds) || lifetimeSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds), lifetimeSeconds, "lifetimeSeconds must be positive and finite");
            }

            var expiresAt = now + lifetimeSeconds;
            if (TryRefresh(targetKey, position, now, expiresAt, out var refreshed))
            {
                _revealed.Add(refreshed.Id);
                return new PingPlacement(refreshed, true, null);
            }

            var marker = new PingMarker(_nextId++, PingKind.Enemy, targetKey, position, true, now, expiresAt);
            _markers.Add(marker);
            _revealed.Add(marker.Id);
            return new PingPlacement(marker, false, null);
        }

        /// <summary>印 id が <see cref="Reveal"/> で付けたものか (その後に付け直されても、消えるまでは true)。</summary>
        public bool IsRevealed(int id) => _revealed.Contains(id);

        /// <summary>
        /// 敵の印 id に、今の敵の様子を渡す。倒れていれば (alive が false) 印を消す。追っている間に見えていれば position へ動かし、
        /// 見えていなければ見失ったことにして今の位置に残す。見失った後と場所の印は、倒れたとき以外は変えない。
        /// </summary>
        /// <param name="id">印の番号。</param>
        /// <param name="alive">敵が生きているか (居なくなったら false)。</param>
        /// <param name="visible">敵が付けた人から見えているか。</param>
        /// <param name="position">敵の今の位置。visible が true のときだけ使う。有限の値。</param>
        /// <param name="marker">呼んだ後の印。消したときは消す前の印。無ければ default。</param>
        public PingTrackResult Track(int id, bool alive, bool visible, Vector3 position, out PingMarker marker)
        {
            var index = IndexOf(id);
            if (index < 0)
            {
                marker = default;
                return PingTrackResult.Missing;
            }

            marker = _markers[index];
            if (marker.Kind != PingKind.Enemy)
            {
                return PingTrackResult.Unchanged;
            }

            if (!alive)
            {
                _markers.RemoveAt(index);
                _revealed.Remove(id);
                return PingTrackResult.Gone;
            }

            if (!marker.IsTracking)
            {
                return PingTrackResult.Unchanged;
            }

            if (!visible)
            {
                marker = marker.Lost();
                _markers[index] = marker;
                return PingTrackResult.Lost;
            }

            if (!PingAim.IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (marker.Position.Equals(position))
            {
                return PingTrackResult.Unchanged;
            }

            marker = marker.WithPosition(position);
            _markers[index] = marker;
            return PingTrackResult.Moved;
        }

        /// <summary>now で寿命が尽きた印を消し、消したものを古い順に expired に足す。消した数を返す。</summary>
        /// <param name="now">今の時刻 (秒)。有限の値。</param>
        /// <param name="expired">消した印を足す一覧。null なら足さない。</param>
        public int Expire(float now, List<PingMarker> expired)
        {
            RequireFinite(now, nameof(now));
            var removed = 0;
            for (var i = 0; i < _markers.Count;)
            {
                if (_markers[i].IsExpiredAt(now))
                {
                    expired?.Add(_markers[i]);
                    _revealed.Remove(_markers[i].Id);
                    _markers.RemoveAt(i);
                    removed++;
                }
                else
                {
                    i++;
                }
            }

            return removed;
        }

        /// <summary>印をすべて消し、消したものを古い順に removed に足す。消した数を返す。</summary>
        public int Clear(List<PingMarker> removed)
        {
            var count = _markers.Count;
            removed?.AddRange(_markers);
            _markers.Clear();
            _revealed.Clear();
            return count;
        }

        public bool TryGet(int id, out PingMarker marker)
        {
            var index = IndexOf(id);
            marker = index >= 0 ? _markers[index] : default;
            return index >= 0;
        }

        /// <summary>targetKey の敵に印が付いているか (見失った後も、消えるまでは付いている)。</summary>
        public bool IsMarked(int targetKey) => IndexOfTarget(targetKey) >= 0;

        private int IndexOf(int id)
        {
            for (var i = 0; i < _markers.Count; i++)
            {
                if (_markers[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        // 付け直しで消える時刻を縮めない。UAV の長い印に手で付け直したときに、UAV の秒数を失わないため。
        private bool TryRefresh(int targetKey, Vector3 position, float now, float expiresAt, out PingMarker refreshed)
        {
            var existing = IndexOfTarget(targetKey);
            if (existing < 0)
            {
                refreshed = default;
                return false;
            }

            var previous = _markers[existing];
            refreshed = new PingMarker(previous.Id, PingKind.Enemy, targetKey, position, true, now, Math.Max(previous.ExpiresAt, expiresAt));
            _markers.RemoveAt(existing);
            _markers.Add(refreshed);
            return true;
        }

        private int CountLimited()
        {
            var count = 0;
            foreach (var marker in _markers)
            {
                if (!_revealed.Contains(marker.Id))
                {
                    count++;
                }
            }

            return count;
        }

        // CountLimited が上限 (1 以上) に達しているときだけ呼ぶので、必ず見つかる。
        private int IndexOfOldestLimited()
        {
            for (var i = 0; i < _markers.Count; i++)
            {
                if (!_revealed.Contains(_markers[i].Id))
                {
                    return i;
                }
            }

            return 0;
        }

        private int IndexOfTarget(int targetKey)
        {
            for (var i = 0; i < _markers.Count; i++)
            {
                if (_markers[i].TargetKey == targetKey)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void RequireFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite");
            }
        }
    }
}

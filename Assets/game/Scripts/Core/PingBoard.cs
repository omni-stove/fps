using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
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

        public IReadOnlyList<PingMarker> Markers => _markers;

        public int Count => _markers.Count;

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

        public bool IsRevealed(int id) => _revealed.Contains(id);

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

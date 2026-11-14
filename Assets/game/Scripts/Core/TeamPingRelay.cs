using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    public sealed class TeamPingRelay
    {
        private readonly Dictionary<int, Pinger> _pingers = new Dictionary<int, Pinger>();
        private readonly Dictionary<int, Owner> _owners = new Dictionary<int, Owner>();
        private readonly List<PingMarker> _removed = new List<PingMarker>();
        private int _nextSharedId = 1;

        public TeamPingRelay(PingSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public PingSpec Spec { get; }

        public int Count => _owners.Count;

        public PingRefusal CheckInterval(int pingerId, float now)
        {
            RequireFinite(now, nameof(now));
            return _pingers.TryGetValue(pingerId, out var pinger) && pinger.LastAttemptAt.HasValue
                && now - pinger.LastAttemptAt.Value < Spec.MinIntervalSeconds
                    ? PingRefusal.TooSoon
                    : PingRefusal.None;
        }

        public PingRefusal TryPlace(
            int pingerId, Team team, PingHit hit, int? targetKey, float now, List<SharedPingChange> changes, out SharedPing placed)
        {
            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            placed = default;
            var interval = CheckInterval(pingerId, now);
            if (interval != PingRefusal.None)
            {
                return interval;
            }

            var pinger = GetOrAdd(pingerId, team, changes);

            // 何も無い所への連打でも、そのたびにサーバーが照準の先を調べ直さないよう、付けなかった試みも間隔に数える。
            pinger.LastAttemptAt = now;
            if (!hit.HasTarget)
            {
                return PingRefusal.NoTarget;
            }

            var placement = pinger.Board.Place(hit, targetKey, now);
            if (placement.Replaced.HasValue)
            {
                var replaced = placement.Replaced.Value;
                if (pinger.SharedIds.TryGetValue(replaced.Id, out var replacedId))
                {
                    Forget(pinger, replacedId, replaced.Id);
                    changes.Add(SharedPingChange.Removed(ToShared(pingerId, pinger.Team, replacedId, replaced), PingRemoval.Replaced));
                }
            }

            var marker = placement.Marker;
            if (!pinger.SharedIds.TryGetValue(marker.Id, out var sharedId))
            {
                sharedId = _nextSharedId++;
                pinger.SharedIds.Add(marker.Id, sharedId);
                _owners.Add(sharedId, new Owner(pingerId, marker.Id));
            }

            placed = ToShared(pingerId, pinger.Team, sharedId, marker);
            changes.Add(SharedPingChange.Placed(placed, placement.Refreshed));
            return PingRefusal.None;
        }

        public void CollectTracking(List<SharedPing> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            foreach (var pair in _pingers)
            {
                foreach (var marker in pair.Value.Board.Markers)
                {
                    if (marker.Kind == PingKind.Enemy && marker.IsTracking && pair.Value.SharedIds.TryGetValue(marker.Id, out var sharedId))
                    {
                        into.Add(ToShared(pair.Key, pair.Value.Team, sharedId, marker));
                    }
                }
            }
        }

        public void CollectTeam(Team team, List<SharedPing> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            foreach (var pair in _pingers)
            {
                if (pair.Value.Team != team)
                {
                    continue;
                }

                foreach (var marker in pair.Value.Board.Markers)
                {
                    if (pair.Value.SharedIds.TryGetValue(marker.Id, out var sharedId))
                    {
                        into.Add(ToShared(pair.Key, pair.Value.Team, sharedId, marker));
                    }
                }
            }
        }

        public void Track(int sharedId, bool alive, bool visible, Vector3 position, List<SharedPingChange> changes)
        {
            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            if (!_owners.TryGetValue(sharedId, out var owner) || !_pingers.TryGetValue(owner.PingerId, out var pinger))
            {
                return;
            }

            switch (pinger.Board.Track(owner.BoardId, alive, visible, position, out var marker))
            {
                case PingTrackResult.Moved:
                    changes.Add(SharedPingChange.Moved(ToShared(owner.PingerId, pinger.Team, sharedId, marker)));
                    break;
                case PingTrackResult.Lost:
                    changes.Add(SharedPingChange.Lost(ToShared(owner.PingerId, pinger.Team, sharedId, marker)));
                    break;
                case PingTrackResult.Gone:
                    Forget(pinger, sharedId, owner.BoardId);
                    changes.Add(SharedPingChange.Removed(ToShared(owner.PingerId, pinger.Team, sharedId, marker), PingRemoval.TargetGone));
                    break;
            }
        }

        public int Expire(float now, List<SharedPingChange> changes)
        {
            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            RequireFinite(now, nameof(now));
            var count = 0;
            foreach (var pair in _pingers)
            {
                _removed.Clear();
                pair.Value.Board.Expire(now, _removed);
                count += RemoveAll(pair.Key, pair.Value, PingRemoval.Expired, changes);
            }

            _removed.Clear();
            return count;
        }

        public int RemovePlayer(int pingerId, List<SharedPingChange> changes)
        {
            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            if (!_pingers.TryGetValue(pingerId, out var pinger))
            {
                return 0;
            }

            _removed.Clear();
            pinger.Board.Clear(_removed);
            var count = RemoveAll(pingerId, pinger, PingRemoval.PingerLeft, changes);
            _removed.Clear();
            _pingers.Remove(pingerId);
            return count;
        }

        // 共有の番号は振り直さない。前の試合の印の知らせが遅れて届いても、次の試合の印と取り違えないため。
        public int Clear(List<SharedPingChange> changes)
        {
            var count = 0;
            foreach (var pair in _pingers)
            {
                _removed.Clear();
                pair.Value.Board.Clear(_removed);
                count += RemoveAll(pair.Key, pair.Value, PingRemoval.Cleared, changes);
            }

            _removed.Clear();
            _pingers.Clear();
            _owners.Clear();
            return count;
        }

        public bool TryGet(int sharedId, out SharedPing ping)
        {
            if (_owners.TryGetValue(sharedId, out var owner)
                && _pingers.TryGetValue(owner.PingerId, out var pinger)
                && pinger.Board.TryGet(owner.BoardId, out var marker))
            {
                ping = ToShared(owner.PingerId, pinger.Team, sharedId, marker);
                return true;
            }

            ping = default;
            return false;
        }

        private Pinger GetOrAdd(int pingerId, Team team, List<SharedPingChange> changes)
        {
            if (_pingers.TryGetValue(pingerId, out var pinger))
            {
                // 前の陣営の味方に残った印を、もう敵になった人の印として見せ続けないよう消す。
                if (pinger.Team != team)
                {
                    _removed.Clear();
                    pinger.Board.Clear(_removed);
                    RemoveAll(pingerId, pinger, PingRemoval.PingerLeft, changes);
                    _removed.Clear();
                    pinger.Team = team;
                }

                return pinger;
            }

            pinger = new Pinger(new PingBoard(Spec), team);
            _pingers.Add(pingerId, pinger);
            return pinger;
        }

        private int RemoveAll(int pingerId, Pinger pinger, PingRemoval reason, List<SharedPingChange> changes)
        {
            var count = 0;
            foreach (var marker in _removed)
            {
                if (!pinger.SharedIds.TryGetValue(marker.Id, out var sharedId))
                {
                    continue;
                }

                Forget(pinger, sharedId, marker.Id);
                changes?.Add(SharedPingChange.Removed(ToShared(pingerId, pinger.Team, sharedId, marker), reason));
                count++;
            }

            return count;
        }

        private void Forget(Pinger pinger, int sharedId, int boardId)
        {
            pinger.SharedIds.Remove(boardId);
            _owners.Remove(sharedId);
        }

        private static SharedPing ToShared(int pingerId, Team team, int sharedId, PingMarker marker)
        {
            return new SharedPing(
                pingerId,
                team,
                new PingMarker(sharedId, marker.Kind, marker.TargetKey, marker.Position, marker.IsTracking, marker.PlacedAt, marker.ExpiresAt));
        }

        private static void RequireFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite");
            }
        }

        private sealed class Pinger
        {
            public Pinger(PingBoard board, Team team)
            {
                Board = board;
                Team = team;
            }

            public PingBoard Board { get; }

            // 板の番号は 1 人ごとに 1 から振られるので、配るときは試合の中で一意の番号に置き換える。
            public Dictionary<int, int> SharedIds { get; } = new Dictionary<int, int>();

            public Team Team { get; set; }

            public float? LastAttemptAt { get; set; }
        }

        private readonly struct Owner
        {
            public Owner(int pingerId, int boardId)
            {
                PingerId = pingerId;
                BoardId = boardId;
            }

            public int PingerId { get; }

            public int BoardId { get; }
        }
    }
}

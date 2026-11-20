using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 再生では誰の味方でもないので、観戦 (ClientSpectate) と違って敵味方・生死で絞らずに、残っている全員と自由なカメラを順に選べる。
    // 最初は記録した人の目から見る。見ていた人が抜けたら次の人へ、誰もいなければ自由なカメラへ移る。
    public sealed class ClientReplayView
    {
        private readonly int _recordedPlayerId;
        private readonly StanceGeometry _stance;
        private readonly InterpolationClock _clock;
        private readonly SortedDictionary<int, RemoteTrack> _tracks = new SortedDictionary<int, RemoteTrack>();
        private readonly HashSet<int> _present = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();
        private readonly List<int> _order = new List<int>();
        private int _newestTick = -1;

        public ClientReplayView(int recordedPlayerId, int tickRate, StanceGeometry stance)
        {
            if (recordedPlayerId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(recordedPlayerId), recordedPlayerId, "recordedPlayerId must not be negative");
            }

            _recordedPlayerId = recordedPlayerId;
            _stance = stance ?? throw new ArgumentNullException(nameof(stance));
            _clock = new InterpolationClock(tickRate);
            TargetId = recordedPlayerId;
        }

        public int? TargetId { get; private set; }

        public bool IsFree => !TargetId.HasValue;

        public int PlayerCount => _tracks.Count;

        public IEnumerable<int> PlayerIds => _tracks.Keys;

        public void Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (snapshot.Tick > _newestTick)
            {
                _newestTick = snapshot.Tick;
            }

            _present.Clear();
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                _present.Add(row.PlayerId);
                if (!_tracks.TryGetValue(row.PlayerId, out var track))
                {
                    track = new RemoteTrack();
                    _tracks.Add(row.PlayerId, track);
                }

                // 隠れた行の位置は空なので記録に入れず、見え直した tick から記録し直す (ClientMatch と同じ)
                if (row.Concealed)
                {
                    track.Clear();
                    continue;
                }

                track.Record(snapshot.Tick, RemotePose.From(row));
            }

            _gone.Clear();
            foreach (var id in _tracks.Keys)
            {
                if (!_present.Contains(id))
                {
                    _gone.Add(id);
                }
            }

            foreach (var id in _gone)
            {
                _tracks.Remove(id);
            }
        }

        // 見ている人が変わったら true を返す。
        public bool Update(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be zero or more and finite");
            }

            if (_newestTick >= 0)
            {
                _clock.Advance(deltaSeconds, _newestTick);
            }

            // 最初のスナップショットが届くまでは誰も載っていないので、記録した人を待つ
            if (!TargetId.HasValue || _newestTick < 0 || _tracks.ContainsKey(TargetId.Value))
            {
                return false;
            }

            return Select(NextAfter(TargetId.Value));
        }

        // 人の並び (Id の順) の後ろに自由なカメラを置き、step の向きに巡る。変わったら true を返す。
        public bool Step(int step)
        {
            if (step == 0)
            {
                return false;
            }

            _order.Clear();
            _order.AddRange(_tracks.Keys);
            var slots = _order.Count + 1;
            var at = TargetId.HasValue ? _order.IndexOf(TargetId.Value) : _order.Count;
            if (at < 0)
            {
                at = _order.Count;
            }

            var next = ((at + step) % slots + slots) % slots;
            return Select(next < _order.Count ? _order[next] : (int?)null);
        }

        public bool FreeCamera()
        {
            return Select(null);
        }

        public bool Follow(int playerId)
        {
            return _tracks.ContainsKey(playerId) && Select(playerId);
        }

        public bool TryGetView(out Vector3 eye, out float yaw)
        {
            eye = default;
            yaw = 0f;
            if (!TargetId.HasValue || !_tracks.TryGetValue(TargetId.Value, out var track) || !track.TrySample(_clock.RenderTick, out var pose))
            {
                return false;
            }

            eye = pose.Position + new Vector3(0f, _stance.Shape(pose.Stance).EyeY, 0f);
            yaw = pose.Yaw;
            return true;
        }

        public void Reset()
        {
            _tracks.Clear();
            _newestTick = -1;
            TargetId = _recordedPlayerId;
        }

        private int? NextAfter(int from)
        {
            int? first = null;
            foreach (var id in _tracks.Keys)
            {
                if (id > from)
                {
                    return id;
                }

                first ??= id;
            }

            return first;
        }

        private bool Select(int? target)
        {
            if (TargetId == target)
            {
                return false;
            }

            TargetId = target;
            return true;
        }
    }
}

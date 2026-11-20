using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 見ていた相手が倒れたり抜けたりしたら、知らせを送らずに次の味方へ移るのは、サーバーも同じ SpectateTargets の決まりで移るため。
    public sealed class ClientSpectate
    {
        private readonly int _localPlayerId;
        private readonly StanceGeometry _stance;
        private readonly Func<IMessage, bool> _send;
        private readonly SpectateRequestMessage _request = new SpectateRequestMessage();
        private readonly Dictionary<int, Remote> _remotes = new Dictionary<int, Remote>();
        private readonly HashSet<int> _present = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();
        private readonly List<SpectateCandidate> _candidates = new List<SpectateCandidate>();
        private readonly InterpolationClock _clock;
        private int _newestTick = -1;
        private bool _hasSelf;
        private Team _team;
        private bool _selfDead;
        private DeathCamera _deathCamera = DeathCamera.None;
        private ulong? _target;

        public ClientSpectate(int localPlayerId, int tickRate, StanceGeometry stance, Func<IMessage, bool> send)
        {
            _localPlayerId = localPlayerId;
            _stance = stance ?? throw new ArgumentNullException(nameof(stance));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _clock = new InterpolationClock(tickRate);
        }

        public SpectateStatus? Status { get; private set; }

        public int? TargetId => Status?.TargetId;

        public RoundPhase? Round { get; private set; }

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

            _deathCamera = snapshot.Match.Rules.DeathCamera;
            Round = snapshot.Match.HasRound ? snapshot.Match.Round : (RoundPhase?)null;
            _hasSelf = false;
            _present.Clear();
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (row.PlayerId == _localPlayerId)
                {
                    _hasSelf = true;
                    _team = row.Team;
                    _selfDead = row.Health <= 0 && !row.Downed.IsDowned;
                    continue;
                }

                _present.Add(row.PlayerId);
                if (!_remotes.TryGetValue(row.PlayerId, out var remote))
                {
                    remote = new Remote();
                    _remotes.Add(row.PlayerId, remote);
                }

                remote.Team = row.Team;
                remote.IsAlive = row.Health > 0 || row.Downed.IsDowned;

                // 隠れた行の位置は空なので記録に入れず、見え直した tick から記録し直す (ClientMatch と同じ)
                if (row.Concealed)
                {
                    remote.Track.Clear();
                    continue;
                }

                remote.Track.Record(snapshot.Tick, RemotePose.From(row));
            }

            _gone.Clear();
            foreach (var id in _remotes.Keys)
            {
                if (!_present.Contains(id))
                {
                    _gone.Add(id);
                }
            }

            foreach (var id in _gone)
            {
                _remotes.Remove(id);
            }
        }

        public bool Update(float deltaSeconds)
        {
            return Update(deltaSeconds, Round);
        }

        public bool Update(float deltaSeconds, RoundPhase? round)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be zero or more and finite");
            }

            if (_newestTick >= 0)
            {
                _clock.Advance(deltaSeconds, _newestTick);
            }

            var reason = ReasonNow(round);
            if (!reason.HasValue)
            {
                _target = null;
                return SetStatus(null);
            }

            return Pick(0, reason.Value);
        }

        public bool Step(SpectateStep step)
        {
            if (!Enum.IsDefined(typeof(SpectateStep), step))
            {
                throw new ArgumentOutOfRangeException(nameof(step), step, "step must be a defined SpectateStep");
            }

            if (!Status.HasValue || !Status.Value.TargetId.HasValue)
            {
                return false;
            }

            Pick(step == SpectateStep.Next ? 1 : -1, Status.Value.Reason);
            _request.Step = step;
            return _send(_request);
        }

        public bool TryGetView(out Vector3 eye, out float yaw)
        {
            eye = default;
            yaw = 0f;
            var target = TargetId;
            if (!target.HasValue || !_remotes.TryGetValue(target.Value, out var remote)
                || !remote.Track.TrySample(_clock.RenderTick, out var pose))
            {
                return false;
            }

            eye = pose.Position + new Vector3(0f, _stance.Shape(pose.Stance).EyeY, 0f);
            yaw = pose.Yaw;
            return true;
        }

        public bool Reset()
        {
            _remotes.Clear();
            _newestTick = -1;
            _hasSelf = false;
            _target = null;
            Round = null;
            return SetStatus(null);
        }

        private SpectateReason? ReasonNow(RoundPhase? round)
        {
            if (!_hasSelf)
            {
                return null;
            }

            if (round == RoundPhase.Preparing)
            {
                return SpectateReason.BetweenRounds;
            }

            return _selfDead && _deathCamera == DeathCamera.Ally ? SpectateReason.Dead : (SpectateReason?)null;
        }

        private bool Pick(int step, SpectateReason reason)
        {
            _candidates.Clear();
            foreach (var pair in _remotes)
            {
                _candidates.Add(new SpectateCandidate((ulong)pair.Key, pair.Value.Team, pair.Value.IsAlive));
            }

            _target = SpectateTargets.Pick(_candidates, (ulong)_localPlayerId, _team, _target, step);
            var count = 0;
            for (var i = 0; i < _candidates.Count; i++)
            {
                var candidate = _candidates[i];
                if (candidate.Team == _team && candidate.IsAlive)
                {
                    count++;
                }
            }

            return SetStatus(new SpectateStatus(_target.HasValue ? (int)_target.Value : (int?)null, count, reason));
        }

        private bool SetStatus(SpectateStatus? status)
        {
            if (Status == status)
            {
                return false;
            }

            Status = status;
            return true;
        }

        private sealed class Remote
        {
            public RemoteTrack Track { get; } = new RemoteTrack();

            public Team Team { get; set; }

            public bool IsAlive { get; set; }
        }
    }
}

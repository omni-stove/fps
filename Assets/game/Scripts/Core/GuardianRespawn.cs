using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class GuardianRespawn : IRespawnPolicy
    {
        private readonly IRespawnPolicy _inner;
        private readonly Dictionary<Side, int> _leaders = new Dictionary<Side, int>();
        private readonly HashSet<Side> _fallen = new HashSet<Side>();

        public event Action<Side, int> LeaderChosen;

        public event Action<Side, int> LeaderFell;

        public GuardianRespawn(IRespawnPolicy inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IRespawnPolicy Inner => _inner;

        public float? SecondsUntilNextWave => _inner.SecondsUntilNextWave;

        public IReadOnlyList<Side> LeaderSides
        {
            get
            {
                var sides = new List<Side>(_leaders.Keys);
                sides.Sort();
                return sides;
            }
        }

        public int? GetLeader(Side side)
        {
            return _leaders.TryGetValue(side, out var id) ? id : (int?)null;
        }

        public bool IsLeader(int playerId)
        {
            foreach (var id in _leaders.Values)
            {
                if (id == playerId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasLeaderFallen(Side side) => _fallen.Contains(side);

        public bool CanRespawn(Side side) => !_fallen.Contains(side);

        public void ChooseLeaders(ModeSession session, Func<int, int> pick)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (pick == null)
            {
                throw new ArgumentNullException(nameof(pick));
            }

            var ids = session.GetPlayerIds();
            foreach (var side in session.Sides)
            {
                if (_leaders.ContainsKey(side))
                {
                    continue;
                }

                var members = new List<int>();
                foreach (var id in ids)
                {
                    if (session.GetSide(id) == side)
                    {
                        members.Add(id);
                    }
                }

                if (members.Count == 0)
                {
                    continue;
                }

                var index = pick(members.Count);
                if (index < 0 || index >= members.Count)
                {
                    throw new InvalidOperationException($"pick returned {index} for {members.Count} member(s) of {side}");
                }

                Assign(side, members[index]);
            }
        }

        public void SetLeader(ModeSession session, int playerId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var side = session.GetSide(playerId);
            if (_leaders.TryGetValue(side, out var existing))
            {
                throw new InvalidOperationException($"{side} already has a leader (player {existing})");
            }

            Assign(side, playerId);
            if (session.IsDown(playerId))
            {
                Fall(session, side, playerId);
            }
        }

        public bool WillReturn(ModeSession session, int playerId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return CanRespawn(session.GetSide(playerId)) && _inner.WillReturn(session, playerId);
        }

        public bool BeginWait(ModeSession session, int playerId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var side = session.GetSide(playerId);
            if (_leaders.TryGetValue(side, out var leader) && leader == playerId && !_fallen.Contains(side))
            {
                Fall(session, side, playerId);
                return false;
            }

            return CanRespawn(side) && _inner.BeginWait(session, playerId);
        }

        public void Cancel(int playerId)
        {
            _inner.Cancel(playerId);
            foreach (var pair in _leaders)
            {
                if (pair.Value == playerId && _fallen.Add(pair.Key))
                {
                    // 抜けた後は session から陣営の人を引けないので、待っている人は Tick で止める。
                    LeaderFell?.Invoke(pair.Key, playerId);
                    return;
                }
            }
        }

        // 試合の終わりに呼ばれるので、リーダーと倒れたかどうかは消さずに残し、結果の表示に使えるようにする。
        public void CancelAll() => _inner.CancelAll();

        public void Reset()
        {
            _inner.Reset();
            _leaders.Clear();
            _fallen.Clear();
        }

        public void Tick(ModeSession session, float elapsedSeconds, Action<int> respawn)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (respawn == null)
            {
                throw new ArgumentNullException(nameof(respawn));
            }

            _inner.Tick(session, elapsedSeconds, id =>
            {
                if (session.HasPlayer(id) && CanRespawn(session.GetSide(id)))
                {
                    respawn(id);
                }
            });
        }

        public float? GetRemaining(int playerId) => _inner.GetRemaining(playerId);

        private void Assign(Side side, int playerId)
        {
            _leaders[side] = playerId;
            LeaderChosen?.Invoke(side, playerId);
        }

        private void Fall(ModeSession session, Side side, int leaderId)
        {
            _fallen.Add(side);
            foreach (var id in session.GetPlayerIds())
            {
                if (session.GetSide(id) == side)
                {
                    _inner.Cancel(id);
                }
            }

            LeaderFell?.Invoke(side, leaderId);
        }
    }
}

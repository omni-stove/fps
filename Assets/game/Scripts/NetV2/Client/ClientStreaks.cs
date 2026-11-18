using System;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // スナップショットは倒した数 (StreakKills) だけを載せ、持っている報酬は載せないので、同じ StreakSpec の StreakTracker を手元で
    // サーバーと同じ順に進めて報酬の枠を作る。使ったことはサーバーの StreakActivated が届いてから写し、押しただけでは枠を空けない。
    public sealed class ClientStreaks
    {
        private readonly int _localPlayerId;
        private readonly StreakTracker _tracker;
        private readonly Func<IMessage, bool> _send;
        private readonly Action<string> _log;
        private readonly StreakUseRequestMessage _request = new StreakUseRequestMessage();
        private readonly string _logId;
        private GameModeState? _matchState;
        private bool _hasTeam;
        private Team _team;

        public ClientStreaks(int localPlayerId, StreakSpec spec, Func<IMessage, bool> send, Action<string> log)
        {
            _tracker = new StreakTracker(spec ?? throw new ArgumentNullException(nameof(spec)));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _localPlayerId = localPlayerId;
            _logId = ClientStreakLog.Id(localPlayerId);
        }

        public StreakSpec Spec => _tracker.Spec;

        // 自分の行がまだ届いていない間は出さない (HUD に空の枠を出さないため)
        public StreakStatus? Status { get; private set; }

        public float AlliedUavSecondsLeft { get; private set; }

        public float EnemyUavSecondsLeft { get; private set; }

        // 状態が変わったら true を返す
        public bool Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var before = Status;
            var state = snapshot.Match.State;

            // サーバーは試合が始まるたびに数えた数と持っている報酬を消すので、始まったのを見て手元も消す
            if (_matchState.HasValue && _matchState.Value != GameModeState.Playing && state == GameModeState.Playing)
            {
                _tracker.Reset();
                AlliedUavSecondsLeft = 0f;
                EnemyUavSecondsLeft = 0f;
            }

            _matchState = state;
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                var row = players[i];
                if (row.PlayerId != _localPlayerId)
                {
                    continue;
                }

                _team = row.Team;
                _hasTeam = true;
                Follow(row.StreakKills);
                Status = _tracker.Status;
                break;
            }

            return before != Status;
        }

        public bool Handle(StreakActivatedMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var duration = message.DurationSeconds;
            if (_hasTeam)
            {
                _log(ClientStreakLog.Activated(message.PlayerId, message.Team, message.RewardId, message.Team == _team, duration));
            }

            if (string.Equals(message.RewardId, StreakSpec.UavRewardId, StringComparison.Ordinal) && _hasTeam && duration > 0f)
            {
                if (message.Team == _team)
                {
                    AlliedUavSecondsLeft = Math.Max(AlliedUavSecondsLeft, duration);
                }
                else
                {
                    EnemyUavSecondsLeft = Math.Max(EnemyUavSecondsLeft, duration);
                }
            }

            if (message.PlayerId != _localPlayerId)
            {
                return false;
            }

            // サーバーは次に使える報酬しか使わせないので、手元の先頭と違えば手元の数え方がずれている。枠を勝手に空けず、ずれを残す
            var ready = _tracker.ReadyRewardIds;
            if (ready.Count == 0 || !string.Equals(ready[0], message.RewardId, StringComparison.Ordinal))
            {
                _log(ClientStreakLog.NotReadyHere(_logId, message.RewardId));
                return false;
            }

            var status = _tracker.Use(out var used);
            _log(StreakLog.Used(_logId, used, status));
            var changed = Status != status;
            Status = status;
            return changed;
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            AlliedUavSecondsLeft = Math.Max(0f, AlliedUavSecondsLeft - deltaSeconds);
            EnemyUavSecondsLeft = Math.Max(0f, EnemyUavSecondsLeft - deltaSeconds);
        }

        // 狙う地点でなく向きを送る (地点はサーバーが自分の世界で決める)。送れたかは sent で返す
        public bool TryUse(Vector3 direction, out bool sent)
        {
            sent = false;
            var ready = _tracker.ReadyRewardIds;
            if (!Status.HasValue || ready.Count == 0)
            {
                _log(StreakLog.NothingToUse(_logId));
                return false;
            }

            if (!IsFinite(direction))
            {
                return false;
            }

            _request.RewardId = ready[0];
            _request.Direction = direction;
            sent = _send(_request);
            _log(ClientStreakLog.Requested(_logId, _request.RewardId, sent));
            return true;
        }

        public void Reset()
        {
            _tracker.Reset();
            _matchState = null;
            _hasTeam = false;
            Status = null;
            AlliedUavSecondsLeft = 0f;
            EnemyUavSecondsLeft = 0f;
        }

        // 数が減ったら倒れた (持っている報酬は残る)。スナップショットの間に倒れて倒し直していれば、減った後に増えた分を数える
        private void Follow(int kills)
        {
            if (kills < 0)
            {
                return;
            }

            if (kills < _tracker.Kills)
            {
                var lost = _tracker.Kills;
                _log(StreakLog.Died(_logId, lost, _tracker.Died()));
            }

            while (_tracker.Kills < kills)
            {
                var status = _tracker.Killed(out var earned);
                foreach (var rewardId in earned)
                {
                    _log(StreakLog.Earned(_logId, rewardId, status));
                }
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }
    }
}

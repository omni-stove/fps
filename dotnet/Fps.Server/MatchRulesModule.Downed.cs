using System.Collections.Generic;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    // ダウンと蘇生。蘇生の要求は距離・チーム・状態を ReviveReach で確かめてから覚え、tick ごとに確かめ直して Session に渡す
    internal sealed partial class MatchRulesModule
    {
        private readonly ReviveHoldTable _reviveHolds = new ReviveHoldTable();
        private readonly HashSet<int> _downedIds = new HashSet<int>();

        // ApplyDamage の最中の撃った人と撃たれた人。ダウン中にとどめを刺されたのか、出血・全滅で倒れたのかを分ける
        private int? _damageAttackerId;
        private int? _damageVictimId;

        private float _nextReviveWarningTime;

        public int ReviveHoldCount => _reviveHolds.Count;

        private void ServerRevive(int reviverId, int targetId)
        {
            if (!_session.HasPlayer(targetId))
            {
                _reviveHolds.Release(reviverId);
                WarnReviveRejected(reviverId, targetId, ReviveRejection.NotJoined);
                return;
            }

            var rejection = ReviveReach.Check(_session, reviverId, targetId, PositionOf(reviverId), PositionOf(targetId), ReviveReach.DefaultRange);
            if (rejection != ReviveRejection.None)
            {
                _reviveHolds.Release(reviverId);
                WarnReviveRejected(reviverId, targetId, rejection);
                return;
            }

            _reviveHolds.Hold(reviverId, targetId, _now);
        }

        private void AdvanceRevives(float deltaSeconds)
        {
            if (_reviveHolds.Count == 0)
            {
                return;
            }

            // 終わった試合では誰もダウンしていないので、押したままの要求は理由を出さずに捨てる
            if (_session.State != GameModeState.Playing || _session.Spec.Rules.Actions.Downed != Downed.Revive)
            {
                _reviveHolds.Clear();
                return;
            }

            foreach (var hold in _reviveHolds.TakeActive(_now, ReviveHoldTable.DefaultGraceSeconds))
            {
                var rejection = _session.HasPlayer(hold.ReviverId) && _session.HasPlayer(hold.TargetId)
                    ? ReviveReach.Check(
                        _session, hold.ReviverId, hold.TargetId, PositionOf(hold.ReviverId), PositionOf(hold.TargetId), ReviveReach.DefaultRange)
                    : ReviveRejection.NotJoined;
                if (rejection != ReviveRejection.None)
                {
                    _reviveHolds.Release(hold.ReviverId);
                    WarnReviveRejected(hold.ReviverId, hold.TargetId, rejection);
                    continue;
                }

                _session.TryRevive(hold.ReviverId, hold.TargetId, deltaSeconds);
            }
        }

        private void ClearDowned()
        {
            _reviveHolds.Clear();
            _downedIds.Clear();
        }

        private void ForgetDowned(int playerId)
        {
            _reviveHolds.RemovePlayer(playerId);
            _downedIds.Remove(playerId);
        }

        private DownedSync DownedOf(int playerId)
        {
            if (!_session.IsDowned(playerId))
            {
                return DownedSync.None;
            }

            var bleedout = _session.GetDownedRemaining(playerId);
            return new DownedSync(
                true,
                bleedout.HasValue ? HudFormat.DisplayedSeconds(bleedout.Value) : 0,
                (byte)HudFormat.RevivePercent(_session.GetReviveProgress(playerId)));
        }

        private void HandleDowned(int attackerId, int victimId)
        {
            _log.WriteLine(NetDownedLog.Downed(attackerId, victimId));
        }

        private void HandleRevived(int reviverId, int targetId)
        {
            _reviveHolds.Release(reviverId);
            _log.WriteLine(NetDownedLog.Revived(reviverId, targetId));
        }

        // Session は Killed と PlayerChanged の後にこれを出すので、倒れたなら倒した相手はもう _lastKillers にある
        private void HandleDownedChanged(int playerId)
        {
            var joined = _session.HasPlayer(playerId);
            var isDowned = joined && _session.IsDowned(playerId);
            var wasDowned = _downedIds.Contains(playerId);
            if (isDowned)
            {
                _downedIds.Add(playerId);
            }
            else
            {
                _downedIds.Remove(playerId);
            }

            // ダウンした人はもう起こせず、起き上がった・倒れた人はもう起こされない
            _reviveHolds.RemovePlayer(playerId);

            if (wasDowned && !isDowned && joined && !_session.IsAlive(playerId)
                && playerId != _damageAttackerId && playerId != _damageVictimId)
            {
                _log.WriteLine(NetDownedLog.Bleedout(playerId, _lastKillers.TryGetValue(playerId, out var killer) ? killer : (int?)null));
            }
        }

        private void WarnReviveRejected(int reviverId, int targetId, ReviveRejection reason)
        {
            if (_now < _nextReviveWarningTime)
            {
                return;
            }

            _nextReviveWarningTime = _now + _rejectWarningInterval;
            _log.WriteLine(NetDownedLog.Rejected(reviverId, targetId, reason));
        }
    }
}

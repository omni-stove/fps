using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、ダウンと蘇生 (<see cref="ActionRules.Downed"/> が <see cref="Fps.Core.Downed.Revive"/> のとき)。
    /// <list type="bullet">
    /// <item>Session の Downed / Revived / DownedChanged を受け、<c>[Net] downed</c>・<c>[Net] revived</c>・<c>[Net] bleedout</c> を出し、
    /// 各プレイヤーの <see cref="NetDownedState"/> を書き直す。出血の残りと蘇生の進みは時間で変わるので、毎フレームも書き直す
    /// (値が変わったときだけ送られる)。</item>
    /// <item>ダウン中の人の射撃は <see cref="CanFire"/> で捨てる。移動は <see cref="NetworkPlayerMotor"/> がダウンの状態で止める。</item>
    /// <item>蘇生の要求 (<see cref="ServerRevive"/>) は距離・チーム・状態を <see cref="ReviveReach"/> で確かめてから
    /// <see cref="ReviveHoldTable"/> に覚え、フレームごとに確かめ直して <see cref="TeamDeathmatchSession.TryRevive"/> に渡す。</item>
    /// </list>
    /// Off のルールでは Session がイベントを出さず、ダウンの状態はいつも <see cref="NetDownedState.None"/> のまま。
    /// </summary>
    public sealed partial class NetTeamDeathmatch
    {
        // サーバーだけ。押し続けている蘇生と、ダウン中の人 (DownedChanged の前後を比べて出血で倒れたかを見分ける)
        private readonly ReviveHoldTable _reviveHolds = new ReviveHoldTable();
        private readonly HashSet<int> _downedIds = new HashSet<int>();

        // ApplyDamage の最中の撃った人と撃たれた人。ダウン中にとどめを刺されたのか、出血・全滅で倒れたのかを分ける
        private int? _damageAttackerId;
        private int? _damageVictimId;

        private float _nextReviveWarningTime;

        /// <summary>
        /// shooterId の射撃を処理してよいか。試合中で、倒れておらず、ダウン中でないときだけ true。
        /// <see cref="ServerFire"/> はこれが false の射撃を、弾を減らさずに捨てる。
        /// </summary>
        internal static bool CanFire(TeamDeathmatchSession session, int shooterId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return session.State == GameModeState.Playing && session.IsAlive(shooterId) && !session.IsDowned(shooterId);
        }

        /// <summary>
        /// 押し続けている蘇生 (holds) を、このフレームの deltaSeconds だけ進める。最後の要求から猶予を過ぎたものは捨て、
        /// 残りは位置 (positionOf) と状態を <see cref="ReviveReach.Check"/> で確かめ直し、通ったものだけ
        /// <see cref="TeamDeathmatchSession.TryRevive"/> に渡す。通らなかったものは捨てて rejected に知らせる。
        /// Session の <see cref="TeamDeathmatchSession.Tick"/> の前に、フレームごとに 1 度呼ぶ。
        /// </summary>
        /// <param name="positionOf">playerId の本体の中心。参加していなければ null。</param>
        /// <returns>累計を進めた (起き上がらせたときも含む) 数。</returns>
        internal static int AdvanceRevives(
            TeamDeathmatchSession session,
            ReviveHoldTable holds,
            Func<int, System.Numerics.Vector3?> positionOf,
            double now,
            float deltaSeconds,
            float range,
            Action<int, int, ReviveRejection> rejected = null)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (holds == null)
            {
                throw new ArgumentNullException(nameof(holds));
            }

            if (positionOf == null)
            {
                throw new ArgumentNullException(nameof(positionOf));
            }

            if (session.Spec.Rules.Actions.Downed != Fps.Core.Downed.Revive)
            {
                holds.Clear();
                return 0;
            }

            var advanced = 0;
            foreach (var hold in holds.TakeActive(now, ReviveHoldTable.DefaultGraceSeconds))
            {
                var reviverPosition = positionOf(hold.ReviverId);
                var targetPosition = positionOf(hold.TargetId);
                var rejection = reviverPosition.HasValue && targetPosition.HasValue
                    ? ReviveReach.Check(session, hold.ReviverId, hold.TargetId, reviverPosition.Value, targetPosition.Value, range)
                    : ReviveRejection.NotJoined;
                if (rejection != ReviveRejection.None)
                {
                    holds.Release(hold.ReviverId);
                    rejected?.Invoke(hold.ReviverId, hold.TargetId, rejection);
                    continue;
                }

                if (session.TryRevive(hold.ReviverId, hold.TargetId, deltaSeconds))
                {
                    advanced++;
                }
            }

            return advanced;
        }

        /// <summary>
        /// サーバーで、reviver から届いた蘇生の要求を受ける。targetClientId の味方が参加していて、<see cref="ReviveReach.Check"/>
        /// (距離 <see cref="ReviveReach.DefaultRange"/> m 以内・同じチーム・起こす人が動けて相手がダウン中・試合中) を通れば、
        /// 押し続けていることとして覚える。通らなければ前の要求も捨て、<c>[Net] rejected revive</c> を出す (連打でログを埋めないよう間引く)。
        /// </summary>
        internal void ServerRevive(NetPlayer reviver, ulong targetClientId)
        {
            if (!IsServer || _session == null || reviver == null || !IsJoined(reviver))
            {
                return;
            }

            var reviverId = reviver.ServerPlayerId;
            if (targetClientId > int.MaxValue
                || !_playerById.TryGetValue((int)targetClientId, out var target)
                || target == null)
            {
                _reviveHolds.Release(reviverId);
                WarnReviveRejected(reviverId, targetClientId > int.MaxValue ? NetPlayer.NotJoined : (int)targetClientId, ReviveRejection.NotJoined);
                return;
            }

            var targetId = (int)targetClientId;
            var rejection = ReviveReach.Check(
                _session,
                reviverId,
                targetId,
                NetMath.ToNumerics(reviver.transform.position),
                NetMath.ToNumerics(target.transform.position),
                ReviveReach.DefaultRange);
            if (rejection != ReviveRejection.None)
            {
                _reviveHolds.Release(reviverId);
                WarnReviveRejected(reviverId, targetId, rejection);
                return;
            }

            _reviveHolds.Hold(reviverId, targetId, Time.timeAsDouble);
        }

        private void SubscribeDowned(TeamDeathmatchSession session)
        {
            session.Downed += HandleDowned;
            session.Revived += HandleRevived;
            session.DownedChanged += HandleDownedChanged;
        }

        private void UnsubscribeDowned(TeamDeathmatchSession session)
        {
            session.Downed -= HandleDowned;
            session.Revived -= HandleRevived;
            session.DownedChanged -= HandleDownedChanged;
        }

        private void ClearDowned()
        {
            _reviveHolds.Clear();
            _downedIds.Clear();
            _damageAttackerId = null;
            _damageVictimId = null;
        }

        private void ForgetDowned(int playerId)
        {
            _reviveHolds.RemovePlayer(playerId);
            _downedIds.Remove(playerId);
        }

        /// <summary>Session にダメージを渡す。最中に倒れたダウン中の人が、とどめを刺された人かどうかを見分けるために包む。</summary>
        private DamageOutcome ApplySessionDamage(int attackerId, int victimId, int amount)
        {
            _damageAttackerId = attackerId;
            _damageVictimId = victimId;
            try
            {
                return _session.ApplyDamage(attackerId, victimId, amount);
            }
            finally
            {
                _damageAttackerId = null;
                _damageVictimId = null;
            }
        }

        private void ServerAdvanceRevives(float deltaSeconds)
        {
            if (_reviveHolds.Count == 0)
            {
                return;
            }

            // 終わった試合では誰もダウンしていないので、押したままの要求は理由を出さずに捨てる
            if (_session.State != GameModeState.Playing)
            {
                _reviveHolds.Clear();
                return;
            }

            AdvanceRevives(_session, _reviveHolds, PositionOfJoined, Time.timeAsDouble, deltaSeconds, ReviveReach.DefaultRange, WarnReviveRejected);
        }

        private System.Numerics.Vector3? PositionOfJoined(int playerId)
        {
            return _playerById.TryGetValue(playerId, out var player) && player != null
                ? NetMath.ToNumerics(player.transform.position)
                : (System.Numerics.Vector3?)null;
        }

        /// <summary>ダウン中の人の出血の残りと蘇生の進みを書き直す。変わっていなければ送られない。</summary>
        private void SyncDownedStates()
        {
            if (_downedIds.Count == 0)
            {
                return;
            }

            foreach (var playerId in _downedIds)
            {
                SyncDownedState(playerId);
            }
        }

        private void SyncDownedState(int playerId)
        {
            if (!_playerById.TryGetValue(playerId, out var player) || player == null || !_session.HasPlayer(playerId))
            {
                return;
            }

            player.ServerSetDowned(NetDownedState.From(_session, playerId));
        }

        private void HandleDowned(int attackerId, int victimId)
        {
            Debug.Log(NetDownedLog.Downed(attackerId, victimId));
        }

        private void HandleRevived(int reviverId, int targetId)
        {
            _reviveHolds.Release(reviverId);
            Debug.Log(NetDownedLog.Revived(reviverId, targetId));
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
                Debug.Log(NetDownedLog.Bleedout(playerId, _lastKillers.Get(playerId)));
            }

            SyncDownedState(playerId);
        }

        private void WarnReviveRejected(int reviverId, int targetId, ReviveRejection reason)
        {
            if (Time.unscaledTime < _nextReviveWarningTime)
            {
                return;
            }

            _nextReviveWarningTime = Time.unscaledTime + _rejectWarningInterval;
            Debug.LogWarning(NetDownedLog.Rejected(reviverId, targetId, reason), this);
        }
    }
}

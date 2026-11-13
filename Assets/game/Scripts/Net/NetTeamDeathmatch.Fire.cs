using System;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、サーバー権威のヒットスキャン (射撃の検査・ラグ補償の記録と巻き戻し・部位の判定)。
    /// </summary>
    public sealed partial class NetTeamDeathmatch
    {
        /// <summary>
        /// サーバーで shooter の射撃 1 発を処理する。撃った武器・連射の間隔・始点を確かめ、弾倉から 1 発消費してから、shooter が見ていた時点 time まで
        /// 他のプレイヤーを巻き戻して Raycast し、当たればダメージを与えて <c>[Net] hit attacker=.. victim=.. rewindTicks=..</c> を出す。
        /// 巻き戻せるのは <see cref="LagCompensator.MaxRewindTicks"/> tick 前まで。
        /// 射程・ダメージ・減衰は、サーバーが shooter に今持たせている武器 (<see cref="NetPlayerWeapon.CurrentSpec"/>) の値を使う (クライアントの値は使わない)。
        /// </summary>
        /// <param name="shooter">撃った人。試合に参加していて生きていなければ (ダウン中も) 何もしない (<see cref="CanFire"/>)。</param>
        /// <param name="weaponId">クライアントが送ってきた、撃った武器の Id。ロードアウトに無い・今持っている武器と違えば捨てる。</param>
        /// <param name="origin">クライアントが送ってきた始点。</param>
        /// <param name="direction">クライアントが送ってきた向き。</param>
        /// <param name="time">クライアントが見ていた時点。</param>
        internal void ServerFire(NetPlayer shooter, string weaponId, Vector3 origin, Vector3 direction, ShotTime time)
        {
            if (!IsServer || _session == null || _lagCompensator == null || _serverLoadout == null || shooter == null || !IsJoined(shooter))
            {
                return;
            }

            // ダウン中の射撃も、倒れている人の射撃と同じく弾を減らさずに捨てる
            var shooterId = shooter.ServerPlayerId;
            if (!CanFire(_session, shooterId))
            {
                return;
            }

            // 武器の Id を確かめ、今の武器の連射間隔 (ShotBudget) で制限する。ServerWeapon は弾倉とリロードだけを見る。
            // RPC の届く時刻は揺れるので、間隔どおりの射撃がまとめて届いても Burst 発までは受け入れ、速すぎる連打だけを捨てる
            var weaponCheck = CheckWeaponShot(_serverLoadout, shooterId, shooter.Weapon, weaponId, NetworkManager.ServerTime.Time, out var spec);
            if (weaponCheck != ShotRejection.None)
            {
                WarnRejected(shooterId, weaponCheck);
                return;
            }

            var rejection = ShotValidator.Check(
                NetMath.ToNumerics(origin),
                NetMath.ToNumerics(direction),
                time,
                NetMath.ToNumerics(shooter.transform.position),
                _activeMaxShotOriginOffset);
            if (rejection != ShotRejection.None)
            {
                WarnRejected(shooterId, rejection);
                return;
            }

            if (IsOriginBlocked(shooter, origin))
            {
                WarnRejected(shooterId, ShotRejection.OriginBlocked);
                return;
            }

            // 不正な射撃では弾を減らさないよう、検査を通ってから 1 発消費する。弾倉が空ならここでリロードが始まる
            var weapon = shooter.Weapon;
            var ammo = weapon != null ? weapon.ServerTryFire() : ShotRejection.NoWeapon;
            if (ammo != ShotRejection.None)
            {
                WarnRejected(shooterId, ammo);
                return;
            }

            if (_lastRecordedTick == _noRecordedTick)
            {
                // 試合が始まった直後のフレームで、まだ巻き戻す履歴が無い
                return;
            }

            var currentTick = _lastRecordedTick;
            var rewindTick = _lagCompensator.ClampTick(time.ToTick(), currentTick);
            var rewound = _lagCompensator.Rewind(rewindTick);
            var rewoundCrouched = _lagCompensator.RewindCrouched(rewindTick);

            _rewindTargets.Clear();
            foreach (var pair in _playerById)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                var hittable = pair.Key != shooterId && _session.HasPlayer(pair.Key) && _session.IsAlive(pair.Key);
                _rewindTargets.Add(new RewindTarget(pair.Key, pair.Value.transform, hittable, pair.Value.Stance));
            }

            RewindHit hit;
            bool any;
            try
            {
                any = _rewinder.Raycast(
                    _rewindTargets, rewound, rewoundCrouched, origin, direction, spec.Range, shooter.transform, out hit);
            }
            finally
            {
                _rewindTargets.Clear();
            }

            if (!any)
            {
                return;
            }

            if (!hit.HitTarget)
            {
                if (_session.Spec.Rules.Destruction == Destruction.On)
                {
                    StructureDamage.Apply(hit.RaycastHit.collider, hit.RaycastHit.point, spec.Damage, spec.ExplosionRadius);
                }

                return;
            }

            if (!_playerById.TryGetValue(hit.TargetId, out var victim) || victim == null)
            {
                return;
            }

            var zone = ZoneOf(victim, hit);
            var damage = DamageCalculator.Calculate(spec.Damage, zone, hit.RaycastHit.distance, spec.Falloff);
            DamageOutcome outcome;
            _shotWeaponId = spec.Id;
            _shotHeadshot = zone == HitZone.Head;
            try
            {
                outcome = ApplyDamage(shooter, victim, damage);
            }
            finally
            {
                _shotWeaponId = null;
                _shotHeadshot = false;
            }

            RecordHitDirection(shooter, victim, outcome, direction);
            var health = _session.HasPlayer(hit.TargetId) ? _session.GetHealth(hit.TargetId) : 0;
            Debug.Log(NetHitLog.Hit(shooterId, hit.TargetId, currentTick - rewindTick, zone, damage, health, outcome, hit.TargetCrouched));
        }

        /// <summary>
        /// 体力が変わった人 (跳ね返した味方撃ちなら撃った本人) に、受けた弾の向きを持たせる。
        /// クライアントのやられ演出 (<see cref="DeathBodyPresenter"/>) が、倒れた体をこの向きへ倒す。
        /// </summary>
        private void RecordHitDirection(NetPlayer shooter, NetPlayer victim, DamageOutcome outcome, Vector3 direction)
        {
            if (outcome == DamageOutcome.Ignored)
            {
                return;
            }

            var reflected = shooter != victim
                && _session.Spec.Rules.FriendlyFire == FriendlyFire.Reflect
                && _session.GetTeam(shooter.ServerPlayerId) == _session.GetTeam(victim.ServerPlayerId);
            (reflected ? shooter : victim).ServerSetLastHitDirection(direction);
        }

        /// <summary>
        /// <see cref="ServerFire"/> の武器の検査。weapon が今持っている武器 (<see cref="NetPlayerWeapon.CurrentSpec"/>、持ち替え中は持ち替え先) で
        /// weaponId と連射の制限を確かめ、通ればその武器を spec に返す。持ち替えていれば、連射の制限はその武器の間隔で作り直す。
        /// weapon が null か装備前なら <see cref="ShotRejection.NoWeapon"/> (weaponId がロードアウトにあるとき)。
        /// </summary>
        internal static ShotRejection CheckWeaponShot(
            ServerLoadout loadout, int shooterId, NetPlayerWeapon weapon, string weaponId, double nowSeconds, out WeaponSpec spec)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            var held = weapon != null ? weapon.CurrentSpec : null;
            var rejection = loadout.TryFire(shooterId, held, weaponId, nowSeconds);
            spec = rejection == ShotRejection.None ? held : null;
            return rejection;
        }

        // サーバーの知る本人の目の位置 (今の姿勢の高さ) から始点までの間に、プレイヤー以外のコライダー (壁など) があれば true。
        private static bool IsOriginBlocked(NetPlayer shooter, Vector3 origin)
        {
            var eye = shooter.EyePosition();
            var toOrigin = origin - eye;
            var distance = toOrigin.magnitude;
            if (distance <= 0f)
            {
                return false;
            }

            var hits = Physics.RaycastAll(eye, toOrigin / distance, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<NetPlayer>() == null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>参加中の全プレイヤーの今の位置としゃがみを、サーバーの今の tick で記録する。</summary>
        private void RecordHitboxes()
        {
            var time = NetworkManager.ServerTime.Time;
            if (double.IsNaN(time) || time < 0d)
            {
                return;
            }

            var tick = FireTiming.TickAt(time, NetworkPlayerMotor.TickRate);
            if (tick < _lastRecordedTick)
            {
                // サーバーの時刻は戻らないはずだが、履歴は古い tick を受け付けないので念のため飛ばす
                return;
            }

            foreach (var pair in _playerById)
            {
                if (pair.Value != null)
                {
                    _lagCompensator.Record(tick, pair.Key, NetMath.ToNumerics(pair.Value.transform.position), pair.Value.IsCrouched);
                }
            }

            _lastRecordedTick = tick;
        }

        /// <summary>
        /// 当たった高さから部位を決める。プレイヤーの当たりは CharacterController のカプセル 1 つなので、
        /// 巻き戻した位置と巻き戻した時点の姿勢 (<see cref="RewindHit.TargetCrouched"/>) のカプセルの中の高さの割合で
        /// <see cref="CapsuleHitZone"/> に決めさせる。姿勢を持たないプレイヤーは今の CharacterController の高さで決める。
        /// </summary>
        private static HitZone ZoneOf(NetPlayer victim, RewindHit hit)
        {
            var scale = victim.transform.lossyScale.y;
            var stance = victim.Stance;
            if (stance != null)
            {
                return ZoneOf(stance.Geometry.Shape(hit.TargetCrouched), scale, hit);
            }

            var controller = victim.GetComponent<CharacterController>();
            if (controller == null)
            {
                return HitZone.Body;
            }

            return ZoneOf(new StanceShape(controller.height, controller.center.y, 0f), scale, hit);
        }

        /// <summary>
        /// 当たった高さから部位を決める。shape は当たったときのカプセルの寸法 (本体のローカル)、scaleY は本体の縦の拡大率。
        /// カプセルの底は巻き戻した位置 (<see cref="RewindHit.TargetPosition"/>) から測る。高さが 0 以下か有限でなければ胴体。
        /// </summary>
        internal static HitZone ZoneOf(StanceShape shape, float scaleY, RewindHit hit)
        {
            var height = shape.Height * scaleY;
            if (!(height > 0f) || float.IsInfinity(height))
            {
                return HitZone.Body;
            }

            var bottom = hit.TargetPosition.y + (shape.CenterY * scaleY) - (height / 2f);
            return CapsuleHitZone.Classify(hit.RaycastHit.point.y, bottom, height);
        }

        private void WarnRejected(int shooterId, ShotRejection reason)
        {
            if (Time.unscaledTime < _nextRejectWarningTime)
            {
                return;
            }

            _nextRejectWarningTime = Time.unscaledTime + _rejectWarningInterval;
            Debug.LogWarning(NetHitLog.Rejected(shooterId, reason), this);
        }
    }
}

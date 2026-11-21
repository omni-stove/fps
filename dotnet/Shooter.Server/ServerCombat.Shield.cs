using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // 盾の武器 (WeaponSpec.Shield) を持つ人の盾を、オフラインの WeaponController.SyncShield と同じ決まりで出し・構え・下げる。
    // 板の置き場と弾を止める判定は ServerHitboxes が持ち、ここは誰がどの盾を構えているかを体 (ServerBody.RaisedShield) に書く
    internal sealed partial class ServerCombat
    {
        // 当たり判定の記録 (ServerHitboxes.Record) より先に呼び、その tick の位置と同じ tick の盾で巻き戻せるようにする
        private void UpdateShields()
        {
            foreach (var fighter in _fighters.Values)
            {
                if (!_bodies.TryGet(fighter.PlayerId, out var body))
                {
                    continue;
                }

                var held = HeldShield(fighter, body);
                var raised = held != null && !_movement.IsSprinting(fighter.PlayerId);
                LogShield(fighter, held, raised);
                body.RaisedShield = raised ? held.Shield : null;
            }
        }

        // オフラインで盾をしまう場面 (持ち替え中・はしご・乗り物・倒れた・ダウン) では、サーバーでも盾を出さない
        private WeaponSpec HeldShield(Fighter fighter, ServerBody body)
        {
            var spec = fighter.Weapons.CurrentSpec;
            if (spec == null || !spec.IsShield || fighter.Weapons.IsSwitching || !IsHittable(fighter.PlayerId) || body.IsDowned || body.IsClimbing)
            {
                return null;
            }

            return _vehicles != null && _vehicles.TryGetSeat(fighter.PlayerId, out _, out _) ? null : spec;
        }

        private void LogShield(Fighter fighter, WeaponSpec held, bool raised)
        {
            var id = fighter.PlayerId.ToString(CultureInfo.InvariantCulture);
            var heldId = held?.Id;
            if (!string.Equals(fighter.ShieldWeaponId, heldId, StringComparison.Ordinal))
            {
                if (fighter.ShieldWeaponId != null)
                {
                    _log.WriteLine(RiotShieldLog.Away(id, fighter.ShieldWeaponId));
                }

                if (heldId != null)
                {
                    _log.WriteLine(RiotShieldLog.Out(id, heldId));
                }

                fighter.ShieldWeaponId = heldId;

                // 出した直後は構えているので、オフラインと同じく構えた行は出さず、走って出したときだけ下げた行を出す
                fighter.ShieldRaised = heldId != null;
            }

            if (heldId == null || fighter.ShieldRaised == raised)
            {
                return;
            }

            fighter.ShieldRaised = raised;
            _log.WriteLine(raised ? RiotShieldLog.Raised(id) : RiotShieldLog.Lowered(id, RiotShieldLog.SprintCause));
        }

        // オフラインの BulletShield.TryBlock と同じく、止めた弾は持ち主にも撃った人にも知らせず、着弾の効果 (爆発・状態異常) も出さない。
        // 飛ぶ弾の止まった所での起爆 (Detonate) は、置いた物で止まったときと同じく後で起きる
        internal void BlockByShield(in ServerShot shot, in ShotHit hit)
        {
            _log.WriteLine(ServerCombatLog.ShieldBlocked(hit.ShieldHolderId, shot.ShooterId, shot.Weapon.Id, shot.Impact.Damage));
        }
    }
}

using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // Bot の種類 (bots.json の archetype) が戦闘に持ち込むもの: 狙撃兵の強い一発・盾持ちの板・突進の体当たり・自爆。
    // Bot の値は Bot のモジュールが持つので、戦闘から Bot を見に行かず、Bot のモジュールが差し込む口で受ける
    internal sealed partial class ServerCombat
    {
        // 武器で当てたのではないので、オフラインの EnemyCharger・EnemyBomber と同じく帰属の武器を空にする (キルフィードに持っている銃を出さない)
        public const string BotBodyWeaponId = "";

        // 撃った Bot の Id から、持たせた武器のダメージに掛ける倍率を返す。null なら掛けない
        internal Func<int, float> BotShotDamageMultiplier { get; set; }

        // Bot の Id から、構えている盾の板を返す。持つ盾 (WeaponSpec.Shield) と同じ板と扇で弾を止める。null なら盾を持たない
        internal Func<int, RiotShieldSpec> BotShield { get; set; }

        // 爆発と近接で傷ついた人とその種類を知らせる。盾持ちが盾を下ろす秒数は種類で決まるため
        internal event Action<int, DamageKind> Wounded;

        internal void NoteWounded(int victimId, DamageKind kind)
        {
            Wounded?.Invoke(victimId, kind);
        }

        private int ScaleBotShot(int shooterId, int amount)
        {
            if (BotShotDamageMultiplier == null || !BotRoster.IsBotId(shooterId))
            {
                return amount;
            }

            var multiplier = BotShotDamageMultiplier(shooterId);
            return multiplier == 1f ? amount : DamageCalculator.Scale(amount, multiplier);
        }

        private RiotShieldSpec BotShieldOf(int playerId, ServerBody body)
        {
            if (BotShield == null || !BotRoster.IsBotId(playerId) || !IsHittable(playerId) || body.IsDowned || body.IsClimbing)
            {
                return null;
            }

            return BotShield(playerId);
        }

        // 体当たりは撃った弾ではないので、近接と同じく DealDamage を通す。倒しきらなかった相手は突進の向きへ飛ばす (オフラインは受け手の Knockback)
        internal DamageOutcome BotCharge(int attackerId, int victimId, int damage, Vector3 from, Vector3 direction)
        {
            var outcome = DealDamage(attackerId, victimId, damage, BotBodyWeaponId, HitZone.Body, from);
            if (outcome == DamageOutcome.Ignored || outcome == DamageOutcome.Killed)
            {
                return outcome;
            }

            var spec = _movement.Knockback;
            var weight = BotRoster.IsBotId(victimId) ? spec.EnemyWeight : spec.PlayerWeight;
            if (spec.TryGetVelocity(DamageKind.Charge, direction, 1f, weight, out var velocity))
            {
                _movement.Launch(victimId, velocity);
            }

            return outcome;
        }

        // 自爆も倒されて爆ぜるのも、手榴弾と同じ爆発 (Explode) にする。建物は壊さない (オフラインの EnemyBomber も人だけに効く)。
        // attributeTo は倒した人。null なら自分の爆発として数える
        internal (int Damaged, int Killed) BotDetonate(int bomberId, Vector3 center, ExplosionDamage blast, int? attributeTo)
        {
            ArgumentNullException.ThrowIfNull(blast);
            return Explode(center, new ExplosionSpec(blast), attributeTo ?? bomberId, BotBodyWeaponId);
        }
    }
}

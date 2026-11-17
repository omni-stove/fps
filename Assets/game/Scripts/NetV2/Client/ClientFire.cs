using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    /// <summary>引き金を引いたときに、射撃を送るか・リロードを頼むか・何もしないか。</summary>
    public enum FireDecision
    {
        Hold,
        Fire,
        Reload,
    }

    /// <summary>
    /// 自分の射撃を送る前の判断。撃てるか (弾・間隔・当たり) はサーバーが決めるが、サーバーが捨てると分かっている射撃は送らない
    /// (Netcode のクライアントの <c>NetPlayer.Update</c> と <c>NetPlayerWeapon.PrepareFire</c> と同じ条件)。
    /// </summary>
    public static class ClientFire
    {
        /// <summary>
        /// latest の自分の行と持ち主の状態、自分の予測 predicted から決める。倒れている・ダウン中・回避中・乗り越え中・飛び込み中・
        /// はしごにつかまっている・武器が無い・持ち替え中なら <see cref="FireDecision.Hold"/>、弾倉が空なら <see cref="FireDecision.Reload"/>。
        /// リロード中も <see cref="FireDecision.Hold"/> だが、1 発ずつ込める武器は込めた弾があればリロード中でも <see cref="FireDecision.Fire"/> を返す。
        /// </summary>
        public static FireDecision Decide(SnapshotMessage latest, int playerId, in CharacterMotorState predicted)
        {
            return Decide(latest, playerId, predicted, null);
        }

        // spec が null でも決められるようにするのは、武器の値がまだ引けないときも弾倉の残りで今までどおり決めるため。
        public static FireDecision Decide(SnapshotMessage latest, int playerId, in CharacterMotorState predicted, WeaponSpec spec)
        {
            if (latest == null || !latest.HasOwner)
            {
                return FireDecision.Hold;
            }

            var own = latest.Players.IndexOf(playerId);
            if (own < 0)
            {
                return FireDecision.Hold;
            }

            var row = latest.Players[own];
            if (row.Health <= 0 || row.Downed.IsDowned
                || predicted.IsDodging || predicted.IsVaulting || predicted.IsDiving || predicted.Ladder.IsClimbing)
            {
                return FireDecision.Hold;
            }

            var weapon = latest.Owner.Weapon;
            // 1 発ずつ込める武器は、込めた弾があればサーバーがリロードを止めて撃たせるので、リロード中でも送る。
            var firesWhileReloading = spec != null && spec.Ammo.ReloadStyle == ReloadStyle.PerRound && weapon.AmmoInMagazine > 0;
            if (CurrentWeaponId(latest.Owner) == null || (weapon.IsReloading && !firesWhileReloading) || weapon.IsSwitching)
            {
                return FireDecision.Hold;
            }

            // 熱で撃つ武器は弾倉を持たずリロードもしないので、オーバーヒートして冷め切るまでは頼まずに待つ。
            if (spec != null && spec.Ammo.Kind == AmmoKind.Heat)
            {
                return weapon.IsOverheated || weapon.Heat >= 1f ? FireDecision.Hold : FireDecision.Fire;
            }

            return weapon.AmmoInMagazine > 0 ? FireDecision.Fire : FireDecision.Reload;
        }

        // 弾薬の種類はサーバーが選んだ番号を返す。手元で選び直すと、切り替えの要求が届く前の弾がずれた種類で送られるため。
        public static void Fill(
            FireRequestMessage request, OwnerSnapshot owner, string weaponId, in TriggerPull pull, TriggerEdge edge, int? lockTargetId)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            request.WeaponId = weaponId ?? "";
            request.ChargeFraction = Math.Min(1f, Math.Max(0f, pull.ChargeFraction));
            request.AmmoTypeIndex = owner != null ? owner.Weapon.AmmoTypeIndex : (byte)0;
            request.HasLockTarget = lockTargetId.HasValue;
            request.LockTargetId = lockTargetId ?? 0;
            request.Trigger = edge;
        }

        // 弾薬の種類の番号は 0 が武器そのものの弾、1 からが WeaponSpec.AmmoTypes の番号 + 1 (サーバーの ServerPlayerWeapons.AmmoTypeIndex と同じ)。
        public static RoundSpec CurrentRound(OwnerSnapshot owner, WeaponSpec spec)
        {
            if (owner == null || spec == null)
            {
                return null;
            }

            var index = owner.Weapon.AmmoTypeIndex;
            var types = spec.AmmoTypes;
            return index > 0 && index <= types.Count ? types[index - 1].RoundOr(spec.Round) : spec.Round;
        }

        /// <summary>今持っている武器の Id。武器が届いていなければ null。</summary>
        public static string CurrentWeaponId(OwnerSnapshot owner)
        {
            if (owner == null)
            {
                return null;
            }

            var index = owner.Weapon.EquippedIndex;
            var ids = owner.EquippedWeaponIds;
            return owner.Weapon.WeaponCount > 0 && index < ids.Count && !string.IsNullOrEmpty(ids[index]) ? ids[index] : null;
        }
    }
}

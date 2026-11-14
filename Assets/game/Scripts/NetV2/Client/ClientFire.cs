using Fps.Core;
using Fps.Protocol;

namespace Fps.NetV2
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
        /// はしごにつかまっている・武器が無い・リロード中・持ち替え中なら <see cref="FireDecision.Hold"/>、弾倉が空なら <see cref="FireDecision.Reload"/>。
        /// </summary>
        public static FireDecision Decide(SnapshotMessage latest, int playerId, in CharacterMotorState predicted)
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
            if (CurrentWeaponId(latest.Owner) == null || weapon.IsReloading || weapon.IsSwitching)
            {
                return FireDecision.Hold;
            }

            return weapon.AmmoInMagazine > 0 ? FireDecision.Fire : FireDecision.Reload;
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

using System.Collections.Generic;
using Fps.Protocol;

namespace Fps.NetV2
{
    // WeaponSync は熱だけを運び、オーバーヒート中かを運ばない。冷めている途中の熱は撃てる間の熱と見分けられないので、
    // 上限 (1) を見てから 0 を見るまでを、持っている武器の番号ごとにここで覚えておく (サーバーの HeatAmmo も 0 まで冷めて撃てるようになる)。
    public sealed class ClientOverheat
    {
        private readonly HashSet<byte> _overheated = new HashSet<byte>();

        public bool IsOverheated(OwnerSnapshot owner)
        {
            return owner != null && owner.Weapon.WeaponCount > 0 && _overheated.Contains(owner.Weapon.EquippedIndex);
        }

        public void Observe(SnapshotMessage snapshot)
        {
            if (snapshot == null || !snapshot.HasOwner)
            {
                return;
            }

            var weapon = snapshot.Owner.Weapon;
            if (weapon.WeaponCount == 0)
            {
                _overheated.Clear();
                return;
            }

            if (weapon.Heat >= 1f)
            {
                _overheated.Add(weapon.EquippedIndex);
            }
            else if (weapon.Heat <= 0f)
            {
                _overheated.Remove(weapon.EquippedIndex);
            }
        }
    }
}

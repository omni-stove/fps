using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 拾おうとしている人の、拾う判定 (<see cref="PickupJudge.Judge"/>) に要る今の状態。
    /// 体力と陣営で作り、武器を持てる人は <see cref="WithWeapons"/>、防具を持てる人は <see cref="WithArmor"/> で足す。
    /// </summary>
    public sealed class PickupCollector
    {
        private static readonly PickupWeaponState[] _noWeapons =Array.Empty<PickupWeaponState>();

        private PickupCollector(
            Side? side,
            int health,
            int maxHealth,
            int armor,
            int maxArmor,
            IReadOnlyList<PickupWeaponState> weapons,
            string currentWeaponId)
        {
            Side = side;
            Health = health;
            MaxHealth = maxHealth;
            Armor = armor;
            MaxArmor = maxArmor;
            Weapons = weapons;
            CurrentWeaponId = currentWeaponId;
        }

        /// <param name="side">陣営。持たなければ null。</param>
        /// <param name="health">今の体力。0 以上 maxHealth 以下。0 なら倒れている。</param>
        /// <param name="maxHealth">最大体力。1 以上。</param>
        public PickupCollector(Side? side, int health, int maxHealth)
            : this(side, health, maxHealth, 0, 0, null, null)
        {
            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be positive");
            }

            if (health < 0 || health > maxHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(health), health, "health must be between 0 and maxHealth");
            }
        }

        public Side? Side { get; }

        public int Health { get; }

        public int MaxHealth { get; }

        /// <summary>倒れていなければ true。</summary>
        public bool IsAlive => Health > 0;

        /// <summary>今の防具。防具を持てなければ 0。</summary>
        public int Armor { get; }

        /// <summary>防具の上限。0 なら防具を持てない。</summary>
        public int MaxArmor { get; }

        /// <summary>持っている武器。武器を持てない人 (<see cref="WithWeapons"/> していない) は null。</summary>
        public IReadOnlyList<PickupWeaponState> Weapons { get; }

        /// <summary>今持っている武器の Id。武器を持てなければ null。</summary>
        public string CurrentWeaponId { get; }

        /// <summary>武器を持てる人にする。</summary>
        /// <param name="weapons">持っている武器。null を渡すと空として扱う。Id の重なりは前のものを使う。</param>
        /// <param name="currentWeaponId">今持っている武器の Id。持っていなければ null。weapons に無い Id は <see cref="ArgumentException"/>。</param>
        public PickupCollector WithWeapons(IReadOnlyList<PickupWeaponState> weapons, string currentWeaponId)
        {
            var copy = new List<PickupWeaponState>(weapons != null ? weapons.Count : 0);
            if (weapons != null)
            {
                copy.AddRange(weapons);
            }

            var current = string.IsNullOrEmpty(currentWeaponId) ? null : currentWeaponId;
            if (current != null && IndexIn(copy, current) < 0)
            {
                throw new ArgumentException("currentWeaponId '" + current + "' is not one of weapons", nameof(currentWeaponId));
            }

            return new PickupCollector(Side, Health, MaxHealth, Armor, MaxArmor, copy.Count > 0 ? copy.AsReadOnly() : (IReadOnlyList<PickupWeaponState>)_noWeapons, current);
        }

        /// <summary>防具を持てる人にする。</summary>
        /// <param name="armor">今の防具。0 以上 maxArmor 以下。</param>
        /// <param name="maxArmor">防具の上限。1 以上。</param>
        public PickupCollector WithArmor(int armor, int maxArmor)
        {
            if (maxArmor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxArmor), maxArmor, "maxArmor must be positive");
            }

            if (armor < 0 || armor > maxArmor)
            {
                throw new ArgumentOutOfRangeException(nameof(armor), armor, "armor must be between 0 and maxArmor");
            }

            return new PickupCollector(Side, Health, MaxHealth, armor, maxArmor, Weapons, CurrentWeaponId);
        }

        /// <summary>Id が weaponId の持ち武器の番号。無ければ (武器を持てない人も) -1。</summary>
        public int IndexOfWeapon(string weaponId)
        {
            return Weapons != null && !string.IsNullOrEmpty(weaponId) ? IndexIn(Weapons, weaponId) : -1;
        }

        private static int IndexIn(IReadOnlyList<PickupWeaponState> weapons, string weaponId)
        {
            for (var i = 0; i < weapons.Count; i++)
            {
                if (string.Equals(weapons[i].WeaponId, weaponId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

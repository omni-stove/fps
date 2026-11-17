using System;

namespace Shooter.Core
{
    public readonly struct GunGameTier
    {
        // default では null になるので、読むときに空文字や Id へ寄せる。
        private readonly string _weaponId;
        private readonly string _nameKey;

        public string WeaponId => _weaponId ?? string.Empty;

        public string NameKey => _nameKey ?? string.Empty;

        public string LogName => NameKey.Length > 0 ? NameKey : WeaponId;

        public GunGameTier(string weaponId, string nameKey)
        {
            if (string.IsNullOrWhiteSpace(weaponId))
            {
                throw new ArgumentException("a gun game tier needs a weapon id", nameof(weaponId));
            }

            _weaponId = weaponId.Trim();
            _nameKey = string.IsNullOrWhiteSpace(nameKey) ? null : nameKey.Trim();
        }
    }
}

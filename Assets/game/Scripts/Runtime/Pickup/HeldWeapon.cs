using System;
using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>手放した武器 1 本。その定義と、手放したときの残弾。</summary>
    public readonly struct HeldWeapon
    {
        public HeldWeapon(WeaponDefinition definition, WeaponAmmo ammo)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Definition = definition;
            Ammo = ammo ?? throw new ArgumentNullException(nameof(ammo));
        }

        public WeaponDefinition Definition { get; }

        public WeaponAmmo Ammo { get; }
    }
}

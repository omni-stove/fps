using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public readonly struct HeldWeapon
    {
        private readonly IReadOnlyList<string> _attachments;

        public HeldWeapon(WeaponDefinition definition, WeaponAmmo ammo, IReadOnlyList<string> attachments = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Definition = definition;
            Ammo = ammo ?? throw new ArgumentNullException(nameof(ammo));
            _attachments = attachments;
        }

        public WeaponDefinition Definition { get; }

        public WeaponAmmo Ammo { get; }

        public IReadOnlyList<string> Attachments => _attachments ?? WeaponAttachments.None;
    }
}

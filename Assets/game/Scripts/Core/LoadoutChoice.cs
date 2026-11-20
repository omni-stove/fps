using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class LoadoutChoice
    {
        // abilityIds を null のまま持つのは、能力を保存していないこと (既定に戻す) と、どの枠も「なし」を選んだことを見分けるため。
        public LoadoutChoice(IReadOnlyList<string> weaponIds, IReadOnlyList<string> abilityIds = null)
        {
            if (weaponIds == null)
            {
                throw new ArgumentNullException(nameof(weaponIds));
            }

            WeaponIds = new List<string>(weaponIds).AsReadOnly();
            if (abilityIds != null)
            {
                var abilities = new string[abilityIds.Count];
                for (var i = 0; i < abilities.Length; i++)
                {
                    abilities[i] = abilityIds[i] ?? throw new ArgumentException("ability slot " + i + " is null; use an empty id for none", nameof(abilityIds));
                }

                AbilityIds = Array.AsReadOnly(abilities);
            }
        }

        public IReadOnlyList<string> WeaponIds { get; }

        public IReadOnlyList<string> AbilityIds { get; }
    }
}

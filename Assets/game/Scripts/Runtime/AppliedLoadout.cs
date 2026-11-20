using System;
using System.Collections.Generic;

namespace Shooter.Runtime
{
    public sealed class AppliedLoadout
    {
        // 能力の空の要素は LoadoutDefinition.Abilities と同じくその枠を空にする決まりなので、null を残したまま持つ
        public AppliedLoadout(
            IReadOnlyList<WeaponDefinition> weapons,
            IReadOnlyList<AbilityDefinition> abilities,
            bool restoredWeapons,
            bool restoredAbilities)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            if (abilities == null)
            {
                throw new ArgumentNullException(nameof(abilities));
            }

            Weapons = Array.AsReadOnly(Copy(weapons));
            Abilities = Array.AsReadOnly(Copy(abilities));
            RestoredWeapons = restoredWeapons;
            RestoredAbilities = restoredAbilities;
        }

        public IReadOnlyList<WeaponDefinition> Weapons { get; }

        public IReadOnlyList<AbilityDefinition> Abilities { get; }

        public bool RestoredWeapons { get; }

        public bool RestoredAbilities { get; }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var copy = new T[source.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }
}

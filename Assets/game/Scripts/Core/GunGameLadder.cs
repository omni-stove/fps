using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public sealed class GunGameLadder
    {
        private readonly GunGameTier[] _tiers;
        private readonly string[] _meleeWeaponIds;
        private readonly HashSet<string> _melee;

        public GunGameLadder(IReadOnlyList<GunGameTier> tiers, IEnumerable<string> meleeWeaponIds, bool demotesOnMelee)
        {
            if (tiers == null)
            {
                throw new ArgumentNullException(nameof(tiers));
            }

            if (tiers.Count == 0)
            {
                throw new ArgumentException("a gun game ladder needs at least one tier", nameof(tiers));
            }

            _tiers = new GunGameTier[tiers.Count];
            for (var i = 0; i < _tiers.Length; i++)
            {
                if (tiers[i].WeaponId.Length == 0)
                {
                    throw new ArgumentException("tier #" + i.ToString(CultureInfo.InvariantCulture) + " has no weapon id", nameof(tiers));
                }

                _tiers[i] = tiers[i];
            }

            _melee = new HashSet<string>(StringComparer.Ordinal);
            var melee = new List<string>();
            foreach (var id in meleeWeaponIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new ArgumentException("melee weapon ids must not contain null or blank ids", nameof(meleeWeaponIds));
                }

                var trimmed = id.Trim();
                if (_melee.Add(trimmed))
                {
                    melee.Add(trimmed);
                }
            }

            _meleeWeaponIds = melee.ToArray();
            DemotesOnMelee = demotesOnMelee;
        }

        public IReadOnlyList<GunGameTier> Tiers => _tiers;

        public int TierCount => _tiers.Length;

        public IReadOnlyList<string> MeleeWeaponIds => _meleeWeaponIds;

        public bool DemotesOnMelee { get; }

        public bool IsMelee(string weaponId)
        {
            return weaponId != null && _melee.Contains(weaponId.Trim());
        }
    }
}

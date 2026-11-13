using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ガンゲーム (<see cref="GunGame"/>) の武器の並び。全員が 0 段目の武器から始め、今の段の武器で倒すたびに次の段へ進み、
    /// 最後の段の武器で倒したら勝つ。<see cref="MeleeWeaponIds"/> の武器で倒されたら、<see cref="DemotesOnMelee"/> のときは倒された人が 1 段戻る。
    /// 同じ武器を複数の段に置いてもよい。Id は大文字小文字を区別して比べる。
    /// </summary>
    public sealed class GunGameLadder
    {
        private readonly GunGameTier[] _tiers;
        private readonly string[] _meleeWeaponIds;
        private readonly HashSet<string> _melee;

        /// <param name="tiers">0 段目からの並び。1 段以上。</param>
        /// <param name="meleeWeaponIds">近接の武器 (ナイフなど) の Id。null なら無し。空白の要素は不可、重複は 1 つにまとめる。</param>
        /// <param name="demotesOnMelee">近接の武器で倒された人を 1 段戻すか。</param>
        /// <exception cref="ArgumentNullException">tiers が null。</exception>
        /// <exception cref="ArgumentException">tiers が空、default の段を含む、または meleeWeaponIds に null か空白がある。</exception>
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

        /// <summary>0 段目からの並び。</summary>
        public IReadOnlyList<GunGameTier> Tiers => _tiers;

        /// <summary>段の数。最後の段は TierCount - 1。</summary>
        public int TierCount => _tiers.Length;

        /// <summary>近接の武器の Id。渡された順で、重複は除いてある。</summary>
        public IReadOnlyList<string> MeleeWeaponIds => _meleeWeaponIds;

        /// <summary>近接の武器で倒された人を 1 段戻すか。</summary>
        public bool DemotesOnMelee { get; }

        /// <summary>weaponId が近接の武器なら true。null は false。前後の空白は除いて比べる。</summary>
        public bool IsMelee(string weaponId)
        {
            return weaponId != null && _melee.Contains(weaponId.Trim());
        }
    }
}

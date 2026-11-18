using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    // 段の並びを武器の Id で持つのは、Unity 抜きのサーバーが WeaponDefinition を読めず、武器を Id で引くため。
    // 段の無い既定を認めるのは、ガンゲームを遊ばないルールにも武器の並びを書かせないため。並びが要るかはルールの目的を知る側が確かめる。
    public sealed class GunGameSpec
    {
        public const float DefaultTimeLimitSeconds = 600f;
        public const float DefaultRespawnSeconds = 2f;
        public const bool DefaultDemotesOnMelee = true;

        public static GunGameSpec Default { get; } = new GunGameSpec();

        public float TimeLimitSeconds { get; }

        public float RespawnSeconds { get; }

        public IReadOnlyList<string> TierWeaponIds { get; }

        public IReadOnlyList<string> MeleeWeaponIds { get; }

        public bool DemotesOnMelee { get; }

        public GunGameSpec(
            float timeLimitSeconds = DefaultTimeLimitSeconds,
            float respawnSeconds = DefaultRespawnSeconds,
            IReadOnlyList<string> tierWeaponIds = null,
            IReadOnlyList<string> meleeWeaponIds = null,
            bool demotesOnMelee = DefaultDemotesOnMelee)
        {
            if (!(timeLimitSeconds > 0f) || float.IsInfinity(timeLimitSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds), timeLimitSeconds, "timeLimitSeconds must be positive and finite");
            }

            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds), respawnSeconds, "respawnSeconds must not be negative and must be finite");
            }

            TimeLimitSeconds = timeLimitSeconds;
            RespawnSeconds = respawnSeconds;
            TierWeaponIds = CopyIds(tierWeaponIds, nameof(tierWeaponIds));
            MeleeWeaponIds = CopyIds(meleeWeaponIds, nameof(meleeWeaponIds));
            DemotesOnMelee = demotesOnMelee;
        }

        public bool HasTiers => TierWeaponIds.Count > 0;

        public GunGameLadder ToLadder()
        {
            if (!HasTiers)
            {
                throw new InvalidOperationException("the gun game spec has no tier weapon ids");
            }

            var tiers = new GunGameTier[TierWeaponIds.Count];
            for (var i = 0; i < tiers.Length; i++)
            {
                tiers[i] = new GunGameTier(TierWeaponIds[i], null);
            }

            return new GunGameLadder(tiers, MeleeWeaponIds, DemotesOnMelee);
        }

        private static IReadOnlyList<string> CopyIds(IReadOnlyList<string> ids, string name)
        {
            if (ids == null || ids.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[ids.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(ids[i]))
                {
                    throw new ArgumentException(name + "[" + i.ToString(CultureInfo.InvariantCulture) + "] must not be blank", name);
                }

                copy[i] = ids[i].Trim();
            }

            return Array.AsReadOnly(copy);
        }
    }
}

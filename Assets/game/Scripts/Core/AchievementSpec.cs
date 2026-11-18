using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public sealed class AchievementSpec
    {
        public static IReadOnlyList<AchievementDefinition> DefaultAchievements { get; } = new[]
        {
            new AchievementDefinition("first-blood", "achievement.first-blood", "achievement.first-blood.description", PlayerStat.Kills, 1),
            new AchievementDefinition("marksman", "achievement.marksman", "achievement.marksman.description", PlayerStat.Headshots, 50),
            new AchievementDefinition("veteran", "achievement.veteran", "achievement.veteran.description", PlayerStat.MatchesPlayed, 25),
            new AchievementDefinition("victor", "achievement.victor", "achievement.victor.description", PlayerStat.MatchesWon, 10),
        };

        // 武器の Id で近接を見分けるのは、キルの知らせ (KillFeedEntry) が武器の Id だけを持ち、武器の種類を持たないため。
        public static IReadOnlyList<string> DefaultMeleeWeaponIds { get; } = Array.AsReadOnly(new[] { "knife" });

        public static AchievementSpec Default { get; } = new AchievementSpec();

        public AchievementSpec(IReadOnlyList<AchievementDefinition> achievements = null, IReadOnlyList<string> meleeWeaponIds = null)
        {
            Achievements = achievements == null ? DefaultAchievements : SpecLists.UniqueById(achievements, a => a.Id, nameof(achievements));
            MeleeWeaponIds = meleeWeaponIds == null ? DefaultMeleeWeaponIds : CopyIds(meleeWeaponIds, nameof(meleeWeaponIds));
        }

        public IReadOnlyList<AchievementDefinition> Achievements { get; }

        public IReadOnlyList<string> MeleeWeaponIds { get; }

        public AchievementDefinition Find(string id)
        {
            return SpecLists.FindById(Achievements, a => a.Id, id);
        }

        public bool IsMelee(string weaponId)
        {
            if (weaponId == null)
            {
                return false;
            }

            foreach (var id in MeleeWeaponIds)
            {
                if (string.Equals(id, weaponId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<string> CopyIds(IReadOnlyList<string> ids, string name)
        {
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

    public sealed class AchievementDefinition
    {
        public const bool DefaultHidden = false;

        public AchievementDefinition(string id, string nameKey, string descriptionKey, PlayerStat stat, int target, bool hidden = DefaultHidden)
        {
            Id = SpecLists.Id(id, nameof(id));
            DisplayNameKey = SpecGuard.NameKey(nameKey);
            DescriptionKey = SpecGuard.NameKey(descriptionKey);
            Stat = SpecGuard.Defined(stat, nameof(stat));
            Target = SpecGuard.AtLeast(target, 1, nameof(target));
            Hidden = hidden;
        }

        public string Id { get; }

        public string DisplayNameKey { get; }

        public string DescriptionKey { get; }

        public PlayerStat Stat { get; }

        public int Target { get; }

        public bool Hidden { get; }
    }
}

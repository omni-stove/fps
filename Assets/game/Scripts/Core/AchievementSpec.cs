using System.Collections.Generic;

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

        public static AchievementSpec Default { get; } = new AchievementSpec();

        public AchievementSpec(IReadOnlyList<AchievementDefinition> achievements = null)
        {
            Achievements = achievements == null ? DefaultAchievements : SpecLists.UniqueById(achievements, a => a.Id, nameof(achievements));
        }

        public IReadOnlyList<AchievementDefinition> Achievements { get; }

        public AchievementDefinition Find(string id)
        {
            return SpecLists.FindById(Achievements, a => a.Id, id);
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

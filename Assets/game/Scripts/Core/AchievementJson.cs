using System.Collections.Generic;

namespace Shooter.Core
{
    public static class AchievementJson
    {
        public const string ResourcePath = "Tuning/achievements";

        public const string AchievementsItem = "achievements";

        public const string IdItem = "id";
        public const string NameKeyItem = "nameKey";
        public const string DescriptionKeyItem = "descriptionKey";
        public const string StatItem = "stat";
        public const string TargetItem = "target";
        public const string HiddenItem = "hidden";

        private const string _kind = "achievement JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { AchievementsItem };

        public static IReadOnlyList<string> AchievementItemNames { get; } = new[]
        {
            IdItem,
            NameKeyItem,
            DescriptionKeyItem,
            StatItem,
            TargetItem,
            HiddenItem,
        };

        public static AchievementSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var achievements = SpecJsonReader.ReadList(fields, AchievementsItem, AchievementItemNames, ReadAchievement);
                return SpecJsonReader.Build(fields, ItemNames, () => new AchievementSpec(achievements));
            });
        }

        private static AchievementDefinition ReadAchievement(ServerDataJson.Fields entry)
        {
            var id = SpecJsonReader.RequireText(entry, IdItem);
            var nameKey = entry.ReadString(NameKeyItem, null);
            var descriptionKey = entry.ReadString(DescriptionKeyItem, null);
            var stat = entry.ToEnum<PlayerStat>(entry.ItemPath(StatItem), entry.Require(StatItem));
            var target = entry.ToInt(entry.ItemPath(TargetItem), entry.Require(TargetItem));
            var hidden = entry.ReadBool(HiddenItem, AchievementDefinition.DefaultHidden);
            return new AchievementDefinition(id, nameKey, descriptionKey, stat, target, hidden);
        }
    }
}

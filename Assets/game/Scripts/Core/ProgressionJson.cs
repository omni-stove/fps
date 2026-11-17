using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 項目の名前を ProgressionSpec のコンストラクターの引数名にそろえるのは、値の検査を ProgressionSpec と ProgressionUnlock に任せたまま、
    // 引っかかった項目 (unlocks[2].level など) を例外の引数名から示せるようにするため。
    public static class ProgressionJson
    {
        public const string KillXpItem = "killXp";
        public const string HeadshotXpItem = "headshotXp";
        public const string MatchXpItem = "matchXp";
        public const string WinXpItem = "winXp";
        public const string LevelsItem = "levels";
        public const string UnlocksItem = "unlocks";

        public const string IdItem = "id";
        public const string LevelItem = "level";

        private const string _kind = "progression JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            KillXpItem,
            HeadshotXpItem,
            MatchXpItem,
            WinXpItem,
            LevelsItem,
            UnlocksItem,
        };

        public static IReadOnlyList<string> UnlockItemNames { get; } = new[] { IdItem, LevelItem };

        public static ProgressionSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                var killXp = fields.ReadInt(KillXpItem, ProgressionSpec.DefaultKillXp);
                var headshotXp = fields.ReadInt(HeadshotXpItem, ProgressionSpec.DefaultHeadshotXp);
                var matchXp = fields.ReadInt(MatchXpItem, ProgressionSpec.DefaultMatchXp);
                var winXp = fields.ReadInt(WinXpItem, ProgressionSpec.DefaultWinXp);
                var levels = ReadLevels(fields);
                var unlocks = new List<ProgressionUnlock>();
                var unlockNodes = fields.Array(UnlocksItem);
                for (var i = 0; i < unlockNodes.Count; i++)
                {
                    unlocks.Add(ReadUnlock(fields.Element(unlockNodes[i], UnlocksItem, i, UnlockItemNames)));
                }

                try
                {
                    return new ProgressionSpec(killXp, headshotXp, matchXp, winXp, levels, unlocks);
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(Known(ItemNames, e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path, e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static IReadOnlyList<int> ReadLevels(ServerDataJson.Fields fields)
        {
            if (fields.Find(LevelsItem) == null)
            {
                return ProgressionSpec.Default.Levels;
            }

            var nodes = fields.Array(LevelsItem);
            var levels = new List<int>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                levels.Add(fields.ToInt(fields.ElementPath(LevelsItem, i), nodes[i]));
            }

            return levels;
        }

        private static ProgressionUnlock ReadUnlock(ServerDataJson.Fields fields)
        {
            var id = fields.ReadString(IdItem, null);
            var level = fields.ReadInt(LevelItem, 0);
            try
            {
                return new ProgressionUnlock(id, level);
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(Known(UnlockItemNames, e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path, e.Message, e);
            }
        }

        private static bool Known(IReadOnlyList<string> names, string name)
        {
            foreach (var candidate in names)
            {
                if (string.Equals(candidate, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

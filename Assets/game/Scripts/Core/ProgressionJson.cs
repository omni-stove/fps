using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>
    /// 成長の調整値 (<see cref="ProgressionSpec"/>) を JSON から読む。項目の名前は <see cref="ProgressionSpec"/> のコンストラクターの引数名。
    /// 省略した XP は <see cref="ProgressionSpec"/> の Default* の値、省略した <see cref="LevelsItem"/> は <see cref="ProgressionSpec.Default"/> の表、
    /// 省略した <see cref="UnlocksItem"/> は空 (どれも最初から使える) になる。
    /// 値の検査は <see cref="ProgressionSpec"/> と <see cref="ProgressionUnlock"/> に任せ、引っかかればどの項目 (unlocks[2].level など) かを添えた
    /// <see cref="TuningJsonException"/> を投げる。
    /// </summary>
    public static class ProgressionJson
    {
        public const string KillXpItem = "killXp";
        public const string HeadshotXpItem = "headshotXp";
        public const string AssistXpItem = "assistXp";
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
            AssistXpItem,
            MatchXpItem,
            WinXpItem,
            LevelsItem,
            UnlocksItem,
        };

        public static IReadOnlyList<string> UnlockItemNames { get; } = new[] { IdItem, LevelItem };

        /// <summary>text を成長の調整値として読む。</summary>
        /// <param name="text">JSON の文字列。</param>
        /// <param name="sourceName">例外の文に出す読み込み元の名前 (ファイルのパスなど)。</param>
        /// <exception cref="ArgumentNullException">text が null。</exception>
        /// <exception cref="TuningJsonException">
        /// JSON の文法に合わない、知らない項目がある、型が合わない、または値が <see cref="ProgressionSpec"/>・<see cref="ProgressionUnlock"/> の検査に通らない。
        /// </exception>
        public static ProgressionSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                var killXp = fields.ReadInt(KillXpItem, ProgressionSpec.DefaultKillXp);
                var headshotXp = fields.ReadInt(HeadshotXpItem, ProgressionSpec.DefaultHeadshotXp);
                var assistXp = fields.ReadInt(AssistXpItem, ProgressionSpec.DefaultAssistXp);
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
                    return new ProgressionSpec(killXp, headshotXp, assistXp, matchXp, winXp, levels, unlocks);
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

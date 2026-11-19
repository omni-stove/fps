using System.Collections.Generic;

namespace Shooter.Core
{
    public static class EmoteJson
    {
        public const string ResourcePath = "Tuning/emotes";

        public const string EmotesItem = "emotes";

        public const string IdItem = "id";
        public const string DisplayNameKeyItem = "displayNameKey";
        public const string DurationSecondsItem = "durationSeconds";
        public const string LoopsItem = "loops";
        public const string StopsOnMoveItem = "stopsOnMove";

        private const string _kind = "emote JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { EmotesItem };

        // 項目名はコンストラクターの引数名にそろえ、値の検査で断られた項目を ParamName から示せるようにする。
        public static IReadOnlyList<string> EmoteItemNames { get; } = new[]
        {
            IdItem,
            DisplayNameKeyItem,
            DurationSecondsItem,
            LoopsItem,
            StopsOnMoveItem,
        };

        public static IReadOnlyList<EmoteSpec> Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var emotes = SpecJsonReader.ReadList(fields, EmotesItem, EmoteItemNames, ReadEmote);
                return emotes == null
                    ? EmoteSpec.DefaultEmotes
                    : SpecJsonReader.Build(fields, ItemNames, () => EmoteSpec.UniqueById(emotes));
            });
        }

        private static EmoteSpec ReadEmote(ServerDataJson.Fields entry)
        {
            var id = SpecJsonReader.RequireText(entry, IdItem);
            var displayNameKey = entry.ReadString(DisplayNameKeyItem, null);
            var durationSeconds = entry.ReadFloat(DurationSecondsItem, EmoteSpec.DefaultDurationSeconds);
            var loops = entry.ReadBool(LoopsItem, EmoteSpec.DefaultLoops);
            var stopsOnMove = entry.ReadBool(StopsOnMoveItem, EmoteSpec.DefaultStopsOnMove);
            return new EmoteSpec(id, displayNameKey, durationSeconds, loops, stopsOnMove);
        }
    }
}

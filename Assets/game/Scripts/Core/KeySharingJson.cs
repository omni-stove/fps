using System.Collections.Generic;

namespace Shooter.Core
{
    public static class KeySharingJson
    {
        public const string ResourcePath = "Tuning/key-sharing";

        public const string SharedItem = "shared";

        public const string ActionsItem = "actions";
        public const string PreferItem = "prefer";

        private const string _kind = "key sharing JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { SharedItem };

        // 項目名はコンストラクターの引数名にそろえ、値の検査で断られた項目を ParamName から示せるようにする。
        public static IReadOnlyList<string> RuleItemNames { get; } = new[] { ActionsItem, PreferItem };

        public static KeySharingSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var shared = SpecJsonReader.ReadList(fields, SharedItem, RuleItemNames, ReadRule);
                return shared == null
                    ? KeySharingSpec.Default
                    : SpecJsonReader.Build(fields, ItemNames, () => new KeySharingSpec(shared));
            });
        }

        private static KeySharingRule ReadRule(ServerDataJson.Fields entry)
        {
            var actions = SpecJsonReader.ReadStrings(entry, ActionsItem);
            var prefer = entry.ReadString(PreferItem, null);
            return new KeySharingRule(actions, prefer);
        }
    }
}

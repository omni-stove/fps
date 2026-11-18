using System.Collections.Generic;

namespace Shooter.Core
{
    public static class ChatWordFilterJson
    {
        public const string ResourcePath = "Tuning/chat-words";

        public const string WordsItem = "words";
        public const string MaskItem = "mask";
        public const string ActionItem = "action";
        public const string WholeWordsOnlyItem = "wholeWordsOnly";

        private const string _kind = "chat word JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { WordsItem, MaskItem, ActionItem, WholeWordsOnlyItem };

        public static ChatWordFilterSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var words = SpecJsonReader.ReadStrings(fields, WordsItem);
                var mask = ReadMask(fields);
                var action = fields.ReadEnum(ActionItem, ChatWordFilterSpec.DefaultAction);
                var wholeWordsOnly = fields.ReadBool(WholeWordsOnlyItem, ChatWordFilterSpec.DefaultWholeWordsOnly);
                return SpecJsonReader.Build(fields, ItemNames, () => new ChatWordFilterSpec(words, mask, action, wholeWordsOnly));
            });
        }

        // JSON に char の型は無いので 1 文字の文字列で書かせ、2 文字以上は書き間違いとして断る。
        private static char ReadMask(ServerDataJson.Fields fields)
        {
            var node = fields.Find(MaskItem);
            if (node == null)
            {
                return ChatWordFilterSpec.DefaultMask;
            }

            var text = fields.ToText(fields.ItemPath(MaskItem), node);
            if (text.Length != 1)
            {
                throw fields.Invalid(fields.ItemPath(MaskItem), node, "must be exactly one character");
            }

            return text[0];
        }
    }
}

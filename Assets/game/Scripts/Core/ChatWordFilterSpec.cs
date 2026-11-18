using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public enum ChatWordFilterAction
    {
        Mask,
        Drop,
    }

    public sealed class ChatWordFilterSpec
    {
        public const char DefaultMask = '*';
        public const ChatWordFilterAction DefaultAction = ChatWordFilterAction.Mask;
        public const bool DefaultWholeWordsOnly = false;

        // 何を断るかは言語と年齢層で変わるので、キットは語を持たずにゲームのデータに任せる。
        public static IReadOnlyList<string> DefaultWords { get; } = Array.Empty<string>();

        public static ChatWordFilterSpec Default { get; } = new ChatWordFilterSpec();

        public ChatWordFilterSpec(
            IReadOnlyList<string> words = null,
            char mask = DefaultMask,
            ChatWordFilterAction action = DefaultAction,
            bool wholeWordsOnly = DefaultWholeWordsOnly)
        {
            Words = words == null ? DefaultWords : Normalize(words);
            if (char.IsWhiteSpace(mask) || char.IsControl(mask) || char.IsSurrogate(mask))
            {
                throw new ArgumentException("mask must be one visible character", nameof(mask));
            }

            Mask = mask;
            Action = SpecGuard.Defined(action, nameof(action));
            WholeWordsOnly = wholeWordsOnly;
        }

        // 照らし合わせは大文字と小文字を区別しないので、語は小文字にそろえて持つ。
        public IReadOnlyList<string> Words { get; }

        public char Mask { get; }

        public ChatWordFilterAction Action { get; }

        public bool WholeWordsOnly { get; }

        public bool IsEmpty => Words.Count == 0;

        private static string[] Normalize(IReadOnlyList<string> words)
        {
            var copy = new List<string>(words.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var word in words)
            {
                if (string.IsNullOrWhiteSpace(word))
                {
                    throw new ArgumentException("words must not contain an empty word", nameof(words));
                }

                var normalized = word.Trim().ToLowerInvariant();

                // 同じ語の書き重ねは、リストを足し合わせたときに起きやすく害も無いので、断らずに 1 つにまとめる。
                if (seen.Add(normalized))
                {
                    copy.Add(normalized);
                }
            }

            return copy.ToArray();
        }
    }
}

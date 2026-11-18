using System;
using System.Collections.Generic;
using System.Text;

namespace Shooter.Core
{
    // 照らし合わせは文をそろえた写しの上で行い、伏せるのは元の文の文字にする。
    // そろえた写しを送ると、送った人の書いた全角や大文字まで変わってしまうため。
    public sealed class ChatWordFilter
    {
        private const char _halfWidthKatakanaFirst = '･';
        private const char _halfWidthKatakanaLast = 'ﾟ';
        private const char _halfWidthVoicedMark = 'ﾞ';
        private const char _halfWidthSemiVoicedMark = 'ﾟ';
        private const char _fullWidthAsciiFirst = '！';
        private const char _fullWidthAsciiLast = '～';
        private const int _fullWidthAsciiOffset = 0xFEE0;
        private const char _ideographicSpace = '　';

        // 半角カナから全角へは文字コードの差が一定でないので、U+FF65 から U+FF9F までを順に並べた表で引く。
        private const string _fullWidthKatakana =
            "・ヲァィゥェォャュョッーアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン゛゜";

        private const string _voicedBases = "カキクケコサシスセソタチツテトハヒフヘホ";
        private const string _semiVoicedBases = "ハヒフヘホ";

        private readonly string[] _foldedWords;

        public ChatWordFilter(ChatWordFilterSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            var words = new List<string>(spec.Words.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var word in spec.Words)
            {
                // 全角と半角で書き分けた同じ語は、そろえると 1 つになるので重ねて照らさない。
                var folded = Fold(word);
                if (folded.Length > 0 && seen.Add(folded))
                {
                    words.Add(folded);
                }
            }

            _foldedWords = words.ToArray();
        }

        public ChatWordFilterSpec Spec { get; }

        public int Count => _foldedWords.Length;

        public bool IsEmpty => _foldedWords.Length == 0;

        public bool Contains(string text)
        {
            return Find(text) != null;
        }

        // 語が無ければ同じ文字列を返すので、呼ぶ側は参照の比較で伏せたかどうかを知れる。
        public string Mask(string text)
        {
            var masked = Find(text);
            if (masked == null)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                if (!masked[i])
                {
                    builder.Append(text[i]);
                    continue;
                }

                // サロゲートの対は 1 文字に見えるので、伏せ字も 1 つにする。
                if (char.IsLowSurrogate(text[i]) && i > 0 && masked[i - 1] && char.IsHighSurrogate(text[i - 1]))
                {
                    continue;
                }

                builder.Append(Spec.Mask);
            }

            return builder.ToString();
        }

        public ChatRefusal Apply(ChatLine line, out ChatLine filtered)
        {
            if (IsEmpty)
            {
                filtered = line;
                return ChatRefusal.None;
            }

            if (Spec.Action == ChatWordFilterAction.Drop)
            {
                if (Contains(line.Text))
                {
                    filtered = default;
                    return ChatRefusal.Blocked;
                }

                filtered = line;
                return ChatRefusal.None;
            }

            var text = Mask(line.Text);
            filtered = ReferenceEquals(text, line.Text) ? line : new ChatLine(line.SenderId, line.Channel, text, line.SentAtSeconds);
            return ChatRefusal.None;
        }

        public static string Fold(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                builder.Append(FoldAt(text, i, out var used));
                i += used - 1;
            }

            return builder.ToString();
        }

        // 伏せる元の文字を引けるよう、そろえた 1 文字ごとに元の文の始まりと終わりを持つ。
        private bool[] Find(string text)
        {
            if (IsEmpty || string.IsNullOrEmpty(text))
            {
                return null;
            }

            var folded = new StringBuilder(text.Length);
            var starts = new int[text.Length];
            var ends = new int[text.Length];
            for (var i = 0; i < text.Length; i++)
            {
                starts[folded.Length] = i;
                folded.Append(FoldAt(text, i, out var used));
                i += used - 1;
                ends[folded.Length - 1] = i + 1;
            }

            var foldedText = folded.ToString();
            bool[] masked = null;
            foreach (var word in _foldedWords)
            {
                var from = 0;
                while (from <= foldedText.Length - word.Length)
                {
                    var at = foldedText.IndexOf(word, from, StringComparison.Ordinal);
                    if (at < 0)
                    {
                        break;
                    }

                    var end = at + word.Length;
                    if (!Spec.WholeWordsOnly || (IsBoundary(foldedText, at - 1) && IsBoundary(foldedText, end)))
                    {
                        masked = masked ?? new bool[text.Length];
                        for (var i = starts[at]; i < ends[end - 1]; i++)
                        {
                            masked[i] = true;
                        }
                    }

                    from = at + 1;
                }
            }

            return masked;
        }

        private static bool IsBoundary(string text, int index)
        {
            return index < 0 || index >= text.Length || !char.IsLetterOrDigit(text[index]);
        }

        private static char FoldAt(string text, int index, out int used)
        {
            used = 1;
            var c = text[index];
            if (c >= _fullWidthAsciiFirst && c <= _fullWidthAsciiLast)
            {
                return char.ToLowerInvariant((char)(c - _fullWidthAsciiOffset));
            }

            if (c == _ideographicSpace)
            {
                return ' ';
            }

            if (c < _halfWidthKatakanaFirst || c > _halfWidthKatakanaLast)
            {
                return char.ToLowerInvariant(c);
            }

            var kana = _fullWidthKatakana[c - _halfWidthKatakanaFirst];
            if (index + 1 >= text.Length)
            {
                return kana;
            }

            // 半角の濁点と半濁点は別の文字なので、前のカナと合わせて全角の 1 文字にする。
            var next = text[index + 1];
            if (next == _halfWidthVoicedMark)
            {
                if (kana == 'ウ')
                {
                    used = 2;
                    return 'ヴ';
                }

                if (_voicedBases.Contains(kana))
                {
                    used = 2;
                    return (char)(kana + 1);
                }
            }
            else if (next == _halfWidthSemiVoicedMark && _semiVoicedBases.Contains(kana))
            {
                used = 2;
                return (char)(kana + 2);
            }

            return kana;
        }
    }
}

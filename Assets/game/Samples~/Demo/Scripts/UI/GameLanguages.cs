using System;
using System.Collections.Generic;

namespace Shooter.Runtime
{
    public static class GameLanguages
    {
        private static Func<GameLanguageList> _source;

        public static Func<GameLanguageList> Source => _source;

        public static void Use(Func<GameLanguageList> source)
        {
            _source = source;
        }

        public static GameLanguageList Current => _source?.Invoke() ?? GameLanguageList.BuiltIn;

        public static IReadOnlyList<GameLanguage> All => Current.Languages;

        public static string Code(GameLanguage language)
        {
            if (!language.IsDefined)
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "the language has no code");
            }

            return language.Code;
        }

        public static bool TryParseCode(string code, out GameLanguage language)
        {
            return Current.TryFind(code, out language);
        }

        // 今の言語が読めない人でも自分の言語を見つけられるよう、言語の名前は文字列表からは引かない。
        public static string NativeName(GameLanguage language)
        {
            var code = Code(language);
            return Current.TryGet(language, out var entry) ? entry.NativeName : code;
        }

        public static GameLanguage Next(GameLanguage language)
        {
            var all = All;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i] == language)
                {
                    return all[(i + 1) % all.Count];
                }
            }

            return all[0];
        }
    }
}

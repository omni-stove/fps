using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// ゲームで選べる言語の一覧 (<see cref="GameLanguageList"/>) を引く先。一覧を返す関数を <see cref="Use"/> で受け取り、引くたびに呼ぶ。
    /// ゲームでは Fps.Runtime の Localizer が Resources の一覧から作って渡す。引き先が無い・null を返すときは <see cref="GameLanguageList.BuiltIn"/>。
    /// </summary>
    public static class GameLanguages
    {
        private static Func<GameLanguageList> _source;

        /// <summary>今の引き先 (<see cref="Use"/> で渡した関数)。無ければ null。テストが差し替えて戻すのに使う。</summary>
        public static Func<GameLanguageList> Source => _source;

        /// <summary>一覧を返す関数を source にする。null なら外す (以後は <see cref="GameLanguageList.BuiltIn"/>)。</summary>
        public static void Use(Func<GameLanguageList> source)
        {
            _source = source;
        }

        /// <summary>今の一覧。</summary>
        public static GameLanguageList Current => _source?.Invoke() ?? GameLanguageList.BuiltIn;

        /// <summary>設定画面で切り替える順。</summary>
        public static IReadOnlyList<GameLanguage> All => Current.Languages;

        /// <summary>保存した設定と文字列表のファイル名・フォルダー名 (ja.json・Localization/ja/) に使う言語コード。</summary>
        /// <exception cref="ArgumentOutOfRangeException">language が <c>default</c>。</exception>
        public static string Code(GameLanguage language)
        {
            if (!language.IsDefined)
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "the language has no code");
            }

            return language.Code;
        }

        /// <summary>言語コード code の、今の一覧にある言語。大文字小文字は区別し、一覧に無いコードなら false。</summary>
        public static bool TryParseCode(string code, out GameLanguage language)
        {
            return Current.TryFind(code, out language);
        }

        /// <summary>
        /// その言語自身での名前 (一覧の名前)。今の言語が読めない人でも自分の言語を見つけられるよう、文字列表からは引かない。
        /// 一覧に無い言語なら言語コード。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">language が <c>default</c>。</exception>
        public static string NativeName(GameLanguage language)
        {
            var code = Code(language);
            return Current.TryGet(language, out var entry) ? entry.NativeName : code;
        }

        /// <summary><see cref="All"/> の並びで language の次の言語。最後の次と、一覧に無い言語の次は最初に戻る。</summary>
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

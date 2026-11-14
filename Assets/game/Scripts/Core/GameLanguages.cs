using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="GameLanguage"/> の並び・保存する名前 (言語コード)・その言語自身での名前。
    /// </summary>
    public static class GameLanguages
    {
        private const string _japaneseCode = "ja";
        private const string _englishCode = "en";

        private static readonly GameLanguage[] _all = { GameLanguage.Japanese, GameLanguage.English };

        /// <summary>設定画面で切り替える順。</summary>
        public static IReadOnlyList<GameLanguage> All { get; } = new ReadOnlyCollection<GameLanguage>(_all);

        /// <summary>
        /// 保存した設定と文字列表のファイル名 (ja.json / en.json) に使う言語コード。
        /// enum の名前を変えても保存した形式とファイル名が変わらないよう、ここで固定する。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">language が定義済みの値でない。</exception>
        public static string Code(GameLanguage language)
        {
            switch (language)
            {
                case GameLanguage.Japanese:
                    return _japaneseCode;
                case GameLanguage.English:
                    return _englishCode;
                default:
                    throw new ArgumentOutOfRangeException(nameof(language), language, "unknown game language");
            }
        }

        /// <summary>言語コード code の言語。大文字小文字は区別し、知らないコードなら false。</summary>
        public static bool TryParseCode(string code, out GameLanguage language)
        {
            switch (code)
            {
                case _japaneseCode:
                    language = GameLanguage.Japanese;
                    return true;
                case _englishCode:
                    language = GameLanguage.English;
                    return true;
                default:
                    language = default;
                    return false;
            }
        }

        /// <summary>
        /// その言語自身での名前。今の言語が読めない人でも自分の言語を見つけられるよう、文字列表からは引かない。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">language が定義済みの値でない。</exception>
        public static string NativeName(GameLanguage language)
        {
            switch (language)
            {
                case GameLanguage.Japanese:
                    return "日本語";
                case GameLanguage.English:
                    return "English";
                default:
                    throw new ArgumentOutOfRangeException(nameof(language), language, "unknown game language");
            }
        }

        /// <summary><see cref="All"/> の並びで language の次の言語。最後の次は最初に戻る。</summary>
        public static GameLanguage Next(GameLanguage language)
        {
            var index = Array.IndexOf(_all, language);
            return _all[(index + 1) % _all.Length];
        }
    }
}

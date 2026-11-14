using System;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    /// <summary>
    /// ゲームが読む文字列表 (Assets/game/Localization/Resources/Strings/*.json に Assets/game/Resources/Localization/&lt;言語コード&gt;/*.json を重ねたもの) が読め、どの言語にも同じキーがあり、
    /// 同じキーが同じ番号の値を差し込むこと。片方の言語にだけ足したキーは、もう片方でキーそのものが画面に出てしまう。
    /// </summary>
    public sealed class LocalizationFilesTests
    {
        [Test]
        public void EveryLanguageHasAReadableTable()
        {
            foreach (var language in GameLanguages.All)
            {
                var table = Load(language);

                Assert.That(table.Count, Is.GreaterThan(0), GameLanguages.Code(language));
                foreach (var key in table.Keys)
                {
                    Assert.That(table.Get(key), Is.Not.Empty, $"{GameLanguages.Code(language)}: {key}");
                }
            }
        }

        [Test]
        public void EveryLanguageHasTheSameKeys()
        {
            var japanese = Load(GameLanguage.Japanese);
            foreach (var language in GameLanguages.All.Where(l => l != GameLanguage.Japanese))
            {
                var other = Load(language);
                var code = GameLanguages.Code(language);

                Assert.That(other.Keys.Except(japanese.Keys, StringComparer.Ordinal), Is.Empty, $"only in {code}.json");
                Assert.That(japanese.Keys.Except(other.Keys, StringComparer.Ordinal), Is.Empty, $"missing from {code}.json");
            }
        }

        [Test]
        public void EveryLanguageInsertsTheSameValues()
        {
            var japanese = Load(GameLanguage.Japanese);
            foreach (var language in GameLanguages.All.Where(l => l != GameLanguage.Japanese))
            {
                var other = Load(language);
                foreach (var key in japanese.Keys.Where(other.Contains))
                {
                    Assert.That(
                        StringTable.PlaceholdersOf(other.Get(key)),
                        Is.EqualTo(StringTable.PlaceholdersOf(japanese.Get(key))),
                        $"{GameLanguages.Code(language)}: {key}");
                }
            }
        }

        [Test]
        public void TheEnglishTableHasNoJapanese()
        {
            var english = Load(GameLanguage.English);
            foreach (var key in english.Keys)
            {
                Assert.That(english.Get(key).Any(IsJapanese), Is.False, $"en: {key} = {english.Get(key)}");
            }
        }

        private static StringTable Load(GameLanguage language)
        {
            return JapaneseGameTextSetUp.LoadTable(language);
        }

        // 和文の句読点・かな・漢字 (U+3000〜U+9FFF) と全角の記号 (U+FF00〜U+FFEF)。
        private static bool IsJapanese(char c)
        {
            return (c >= '　' && c <= '鿿') || (c >= '＀' && c <= '￯');
        }
    }
}

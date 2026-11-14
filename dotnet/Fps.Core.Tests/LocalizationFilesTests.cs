using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    /// <summary>
    /// ゲームが読む文字列表 (Assets/game/Localization/Resources/Strings/*.json と Assets/game/Resources/Localization/&lt;言語&gt;/*.json を重ねたもの)
    /// が読め、どの言語にも同じキーがあり、同じキーが同じ番号の値を差し込むこと。片方の言語にだけ足したキーは、もう片方でキーそのものが画面に出てしまう。
    /// </summary>
    public sealed class LocalizationFilesTests
    {
        // 重ねる表は後のものが勝つので、同じ言語の 2 つのファイルに同じキーがあると、片方を書き換えても画面が変わらないことがある。
        [Test]
        public void EveryKeyIsWrittenInOnlyOneFileOfALanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var owners = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var layer in JapaneseGameTextSetUp.LoadLayers(language))
                {
                    foreach (var key in layer.Value.Keys)
                    {
                        Assert.That(owners.TryGetValue(key, out var first), Is.False, $"{key} is in both {first} and {layer.Key}");
                        owners[key] = layer.Key;
                    }
                }
            }
        }

        [Test]
        public void TheMenuTextsAreReadFromTheJapaneseMenuTable()
        {
            var menu = JapaneseGameTextSetUp.LoadLayers(GameLanguage.Japanese)
                .Single(layer => layer.Key.EndsWith("menu.json", StringComparison.Ordinal))
                .Value;

            foreach (var prefix in new[] { "menu.", "pause.", "campaign.", "loadout." })
            {
                Assert.That(menu.Keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal)), Is.True, prefix);
            }

            Assert.That(menu.Get("campaign.continue"), Is.EqualTo("続きから"));
            Assert.That(menu.Get("loadout.title"), Is.EqualTo("ロードアウト"));
        }

        [Test]
        public void TheNetTextsAreReadFromTheNetTableOfEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var layers = JapaneseGameTextSetUp.LoadLayers(language);
                var net = layers.Single(layer => layer.Key.EndsWith("net.json", StringComparison.Ordinal)).Value;

                Assert.That(net.Keys.All(key => key.StartsWith("net.", StringComparison.Ordinal)), Is.True, GameLanguages.Code(language));
                Assert.That(net.Contains("net.stats.line"), Is.True, GameLanguages.Code(language));
                Assert.That(net.Contains("net.disconnect.serverStopped"), Is.True, GameLanguages.Code(language));
            }
        }

        [Test]
        public void TheSurvivalModeTextsAreReadFromTheJapaneseModesSurvivalTable()
        {
            var modes = JapaneseGameTextSetUp.LoadLayers(GameLanguage.Japanese)
                .Single(layer => layer.Key.EndsWith("modes-survival.json", StringComparison.Ordinal))
                .Value;

            foreach (var prefix in new[] { "mode.guardian.", "mode.gungame.", "mode.br." })
            {
                Assert.That(modes.Keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal)), Is.True, prefix);
            }

            Assert.That(modes.Get("mode.guardian.youAreLeader"), Is.EqualTo("あなたがリーダー"));
            Assert.That(modes.Get("mode.br.noWeapon"), Is.EqualTo("武器なし: 落ちている武器を拾う"));
        }

        [Test]
        public void TheWeaponNamesAreReadFromTheItemsTableOfEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var items = JapaneseGameTextSetUp.LoadLayers(language)
                    .Single(layer => layer.Key.EndsWith("items.json", StringComparison.Ordinal))
                    .Value;
                var code = GameLanguages.Code(language);

                Assert.That(items.Get("weapon.rifle"), Is.EqualTo("Rifle"), code);
                Assert.That(items.Get("weapon.handgun"), Is.EqualTo("Handgun"), code);
                Assert.That(items.Get("weapon.launcher"), Is.EqualTo("Launcher"), code);
            }
        }

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

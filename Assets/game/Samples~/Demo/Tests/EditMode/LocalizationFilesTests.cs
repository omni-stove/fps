using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // 片方の言語にだけ足したキーは、もう片方でキーそのものが画面に出てしまうので、どの言語にも同じキーがあることを確かめる。
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
        public void TheControlModeTextsAreReadFromTheJapaneseModesControlTable()
        {
            var modes = JapaneseGameTextSetUp.LoadLayers(GameLanguage.Japanese)
                .Single(layer => layer.Key.EndsWith("modes-control.json", StringComparison.Ordinal))
                .Value;

            foreach (var prefix in new[] { "mode.domination.", "mode.hardpoint.", "mode.killConfirmed.", "mode.freeForAll." })
            {
                Assert.That(modes.Keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal)), Is.True, prefix);
            }

            Assert.That(modes.Get("mode.hardpoint.contested"), Is.EqualTo("争奪中"));
            Assert.That(modes.Get("mode.killConfirmed.tagsOnField"), Is.EqualTo("落ちているタグ {0}"));
        }

        [Test]
        public void TheFlagModeTextsAreReadFromTheModesFlagTableOfEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var modes = JapaneseGameTextSetUp.LoadLayers(language)
                    .Single(layer => layer.Key.EndsWith("modes-flag.json", StringComparison.Ordinal))
                    .Value;
                var code = GameLanguages.Code(language);

                Assert.That(modes.Keys.All(key => key.StartsWith("mode.ctf.", StringComparison.Ordinal)), Is.True, code);
                Assert.That(modes.Contains("mode.ctf.dropped"), Is.True, code);
                Assert.That(modes.Contains("mode.ctf.carrying"), Is.True, code);
            }

            var japanese = JapaneseGameTextSetUp.LoadLayers(GameLanguage.Japanese)
                .Single(layer => layer.Key.EndsWith("modes-flag.json", StringComparison.Ordinal))
                .Value;
            Assert.That(japanese.Get("mode.ctf.atBase"), Is.EqualTo("{0} の旗: 台にある"));
        }

        [Test]
        public void TheInfectedModeTextsAreReadFromTheModesInfectedTableOfEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var modes = JapaneseGameTextSetUp.LoadLayers(language)
                    .Single(layer => layer.Key.EndsWith("modes-infected.json", StringComparison.Ordinal))
                    .Value;
                var code = GameLanguages.Code(language);

                Assert.That(modes.Keys.All(key => key.StartsWith("mode.infected.", StringComparison.Ordinal)), Is.True, code);
                Assert.That(modes.Contains("mode.infected.outbreakIn"), Is.True, code);
                Assert.That(modes.Contains("mode.infected.counts"), Is.True, code);
            }

            var japanese = JapaneseGameTextSetUp.LoadLayers(GameLanguage.Japanese)
                .Single(layer => layer.Key.EndsWith("modes-infected.json", StringComparison.Ordinal))
                .Value;
            Assert.That(japanese.Get("mode.infected.infected"), Is.EqualTo("感染者"));
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
        public void TheExtractionTextsAreReadFromTheExtractionTableOfEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var extraction = JapaneseGameTextSetUp.LoadLayers(language)
                    .Single(layer => layer.Key.EndsWith("extraction.json", StringComparison.Ordinal))
                    .Value;
                var code = GameLanguages.Code(language);

                Assert.That(extraction.Keys.All(key => key.StartsWith("extraction.", StringComparison.Ordinal)), Is.True, code);
                Assert.That(extraction.Contains("extraction.hud.extracting"), Is.True, code);
                Assert.That(extraction.Contains("extraction.stash.title"), Is.True, code);
            }

            var japanese = JapaneseGameTextSetUp.LoadLayers(GameLanguage.Japanese)
                .Single(layer => layer.Key.EndsWith("extraction.json", StringComparison.Ordinal))
                .Value;
            Assert.That(japanese.Get("extraction.hud.closedExit"), Is.EqualTo("この脱出地点は閉じている"));
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
        public void TheProgressionTextsAreReadFromTheProgressionTableOfEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var progression = JapaneseGameTextSetUp.LoadLayers(language)
                    .Single(layer => layer.Key.EndsWith("progression.json", StringComparison.Ordinal))
                    .Value;
                var code = GameLanguages.Code(language);

                Assert.That(progression.Keys.All(key => key.StartsWith("progression.", StringComparison.Ordinal)), Is.True, code);
                Assert.That(progression.Contains("progression.lockedUntil"), Is.True, code);
                Assert.That(progression.Contains("progression.attachmentLocked"), Is.True, code);
            }

            var japanese = JapaneseGameTextSetUp.LoadTable(GameLanguage.Japanese);
            Assert.That(japanese.Format("progression.xpToNext", 800, 3), Is.EqualTo("レベル 3 まで あと 800 XP"));
            Assert.That(japanese.Format("progression.lockedUntil", 4), Is.EqualTo("レベル 4 で解放"));
        }

        [Test]
        public void TheAssistTextsAreReadFromTheAssistTableOfEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var assist = JapaneseGameTextSetUp.LoadLayers(language)
                    .Single(layer => layer.Key.EndsWith("assist.json", StringComparison.Ordinal))
                    .Value;
                var code = GameLanguages.Code(language);

                Assert.That(assist.Keys.All(key => key.StartsWith("assist.", StringComparison.Ordinal)), Is.True, code);
                Assert.That(assist.Contains("assist.scoreboard.caption"), Is.True, code);
                Assert.That(assist.Contains("assist.killFeed.mark"), Is.True, code);
                Assert.That(assist.Contains("assist.profile.assists"), Is.True, code);
            }

            var japanese = JapaneseGameTextSetUp.LoadTable(GameLanguage.Japanese);
            Assert.That(japanese.Format("assist.profile.assists", 4), Is.EqualTo("アシスト 4"));
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

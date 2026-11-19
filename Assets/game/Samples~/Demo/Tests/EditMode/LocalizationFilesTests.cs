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
        public void TheCosmeticsTextsAreReadFromTheCosmeticsTableOfEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var cosmetics = JapaneseGameTextSetUp.LoadLayers(language)
                    .Single(layer => layer.Key.EndsWith("cosmetics.json", StringComparison.Ordinal))
                    .Value;
                var code = GameLanguages.Code(language);

                Assert.That(cosmetics.Keys.All(key => key.StartsWith("cosmetics.", StringComparison.Ordinal)), Is.True, code);
                Assert.That(cosmetics.Contains("cosmetics.title"), Is.True, code);
                Assert.That(cosmetics.Contains("cosmetics.locked"), Is.True, code);
            }

            var japanese = JapaneseGameTextSetUp.LoadLayers(GameLanguage.Japanese)
                .Single(layer => layer.Key.EndsWith("cosmetics.json", StringComparison.Ordinal))
                .Value;
            Assert.That(japanese.Format("cosmetics.locked", "砂漠", 3), Is.EqualTo("砂漠 (レベル 3 で解除)"));
        }

        [Test]
        public void TheEmoteTextsAreReadFromTheEmoteTableOfEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var emote = JapaneseGameTextSetUp.LoadLayers(language)
                    .Single(layer => layer.Key.EndsWith("emote.json", StringComparison.Ordinal))
                    .Value;
                var code = GameLanguages.Code(language);

                Assert.That(emote.Keys.All(key => key.StartsWith("emote.", StringComparison.Ordinal)), Is.True, code);
                Assert.That(emote.Contains(EmoteWheelView.CancelKey), Is.True, code);
                Assert.That(emote.Contains(EmoteWheelView.NameKeyOf("wave")), Is.True, code);
            }

            var japanese = JapaneseGameTextSetUp.LoadLayers(GameLanguage.Japanese)
                .Single(layer => layer.Key.EndsWith("emote.json", StringComparison.Ordinal))
                .Value;
            Assert.That(japanese.Get("emote.name.salute"), Is.EqualTo("敬礼"));
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
        public void TheLeaderboardTextsAreReadFromTheLeaderboardTableOfEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var leaderboard = JapaneseGameTextSetUp.LoadLayers(language)
                    .Single(layer => layer.Key.EndsWith("leaderboard.json", StringComparison.Ordinal))
                    .Value;
                var code = GameLanguages.Code(language);

                Assert.That(leaderboard.Keys.All(key => key.StartsWith("leaderboard.", StringComparison.Ordinal)), Is.True, code);
                foreach (var key in new[]
                {
                    LeaderboardText.TitleKey, LeaderboardText.LoadingKey, LeaderboardText.UnavailableKey, LeaderboardText.EmptyKey,
                    LeaderboardText.NoBoardsKey, LeaderboardText.RankKey, LeaderboardText.UnknownPlayerKey, LeaderboardText.YouKey,
                    LeaderboardText.RefreshKey, LeaderboardText.BackKey,
                })
                {
                    Assert.That(leaderboard.Contains(key), Is.True, code + " " + key);
                }

                // 既定のボードの名前のキーも同じ表に置く
                foreach (var board in LeaderboardSpec.Default.Boards)
                {
                    Assert.That(leaderboard.Contains(board.DisplayNameKey), Is.True, code + " " + board.DisplayNameKey);
                }
            }

            var japanese = JapaneseGameTextSetUp.LoadTable(GameLanguage.Japanese);
            Assert.That(japanese.Format(LeaderboardText.RankKey, 57), Is.EqualTo("57 位"));
            Assert.That(japanese.Resolve(LeaderboardText.PlayerName(new LeaderboardRow(57, "Me", 12, true))), Is.EqualTo("Me (自分)"));
            Assert.That(japanese.Get("leaderboard.kills"), Is.EqualTo("キル数"));
            var english = JapaneseGameTextSetUp.LoadTable(GameLanguage.English);
            Assert.That(english.Format(LeaderboardText.RankKey, 57), Is.EqualTo("#57"));
            Assert.That(english.Resolve(LeaderboardText.PlayerName(new LeaderboardRow(3, string.Empty, 0, true))), Is.EqualTo("Unnamed player (you)"));
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
        public void TheModerationTextsAreReadFromTheModerationTableOfEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var moderation = JapaneseGameTextSetUp.LoadLayers(language)
                    .Single(layer => layer.Key.EndsWith("moderation.json", StringComparison.Ordinal))
                    .Value;
                var code = GameLanguages.Code(language);

                Assert.That(moderation.Keys.All(key => key.StartsWith("moderation.", StringComparison.Ordinal)), Is.True, code);
                Assert.That(moderation.Keys, Is.EquivalentTo(ModerationText.Keys), code);
            }

            var japanese = JapaneseGameTextSetUp.LoadTable(GameLanguage.Japanese);
            Assert.That(japanese.Get("moderation.block"), Is.EqualTo("ブロック"));
            Assert.That(japanese.Format("moderation.reportPrompt", "Player 2"), Is.EqualTo("Player 2 を通報する理由"));
            var english = JapaneseGameTextSetUp.LoadTable(GameLanguage.English);
            Assert.That(english.Format("moderation.reportPrompt", "Player 2"), Is.EqualTo("Why are you reporting Player 2?"));
        }

        [TestCase("hud.json", "hud.wallet")]
        [TestCase("hud.json", "hud.vision.nightVision")]
        [TestCase("hud.json", "hud.vision.thermal")]
        [TestCase("hud.json", "hud.drone.battery")]
        [TestCase("hud.json", "hud.drone.outOfRange")]
        [TestCase("hud.json", "hud.drone.recall")]
        [TestCase("items.json", "ability.visionGoggles")]
        [TestCase("items.json", "deployable.reconDrone")]
        [TestCase("items.json", "weapon.riotShield")]
        [TestCase("items.json", "item.thermalScope")]
        [TestCase("moderation.json", ModerationText.VoteKickStartKey)]
        [TestCase("moderation.json", ModerationText.VoteKickCalledKey)]
        [TestCase("moderation.json", ModerationText.VoteKickYesKey)]
        [TestCase("moderation.json", ModerationText.VoteKickNoKey)]
        [TestCase("moderation.json", ModerationText.VoteKickRemainingKey)]
        [TestCase("moderation.json", ModerationText.VoteKickPassedKey)]
        [TestCase("moderation.json", ModerationText.VoteKickFailedKey)]
        [TestCase("net.json", "net.disconnect.voteKicked")]
        public void TheVoteKickWalletVisionDroneAndShieldTextsAreInTheirTableOfEveryLanguage(string file, string key)
        {
            foreach (var language in GameLanguages.All)
            {
                var table = JapaneseGameTextSetUp.LoadLayers(language)
                    .Single(layer => string.Equals(System.IO.Path.GetFileName(layer.Key), file, StringComparison.Ordinal))
                    .Value;

                Assert.That(table.Contains(key), Is.True, $"{GameLanguages.Code(language)}: {key} in {file}");
                Assert.That(table.Get(key), Is.Not.Empty, $"{GameLanguages.Code(language)}: {key}");
            }
        }

        [Test]
        public void TheVoteKickTextsInsertTheirValues()
        {
            var japanese = JapaneseGameTextSetUp.LoadTable(GameLanguage.Japanese);
            var english = JapaneseGameTextSetUp.LoadTable(GameLanguage.English);

            Assert.That(japanese.Format(ModerationText.VoteKickCalledKey, "Player 2", "Player 5"), Is.EqualTo("Player 2 が Player 5 のキックの投票を始めた"));
            Assert.That(english.Format(ModerationText.VoteKickCalledKey, "Player 2", "Player 5"), Is.EqualTo("Player 2 started a vote to kick Player 5"));
            Assert.That(japanese.Format(ModerationText.VoteKickRemainingKey, 12), Is.EqualTo("残り 12 秒"));
            Assert.That(english.Format(ModerationText.VoteKickRemainingKey, 12), Is.EqualTo("12 s left"));
            Assert.That(japanese.Format("hud.wallet", 3400), Is.EqualTo("所持金 $3400"));
            Assert.That(english.Format("hud.wallet", 3400), Is.EqualTo("$3400"));
            Assert.That(english.Format("hud.drone.battery", 40), Is.EqualTo("Battery 40%"));
        }

        [Test]
        public void TheStatsTextsAndTheShippedAchievementNamesAreReadFromTheStatsTableOfEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var stats = JapaneseGameTextSetUp.LoadLayers(language)
                    .Single(layer => layer.Key.EndsWith("stats.json", StringComparison.Ordinal))
                    .Value;
                var code = GameLanguages.Code(language);

                Assert.That(
                    stats.Keys.All(key => key.StartsWith("stats.", StringComparison.Ordinal) || key.StartsWith("achievement.", StringComparison.Ordinal)),
                    Is.True,
                    code);
                Assert.That(stats.Contains("stats.playTime"), Is.True, code);
                foreach (var achievement in AchievementSpec.DefaultAchievements)
                {
                    Assert.That(stats.Contains(achievement.DisplayNameKey), Is.True, $"{code}: {achievement.DisplayNameKey}");
                    Assert.That(stats.Contains(achievement.DescriptionKey), Is.True, $"{code}: {achievement.DescriptionKey}");
                }
            }

            var japanese = JapaneseGameTextSetUp.LoadTable(GameLanguage.Japanese);
            Assert.That(japanese.Format("stats.playTime", 2, 3), Is.EqualTo("遊んだ時間 2 時間 3 分"));
            Assert.That(japanese.Format("stats.achievement.progress", "名射手", 7, 50), Is.EqualTo("名射手: 7/50"));
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

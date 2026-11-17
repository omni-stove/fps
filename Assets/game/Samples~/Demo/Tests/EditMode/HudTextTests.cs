using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class HudTextTests
    {
        [Test]
        public void TeamScoreRejectsNegativeAndUnknownTeams()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.TeamScore(Team.Alpha, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.TeamName((Team)99));
        }

        [TestCase(10, "次の試合まで 10 秒")]
        [TestCase(1, "次の試合まで 1 秒")]
        [TestCase(0, "次の試合まで 0 秒")]
        public void NextMatchShowsTheSecondsLeft(int seconds, string expected)
        {
            Assert.That(GameText.Resolve(HudText.NextMatch(seconds)), Is.EqualTo(expected));
        }

        [Test]
        public void NextMatchRejectsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.NextMatch(-1));
        }

        [TestCase(12, "ダウン中 あと 12 秒")]
        [TestCase(1, "ダウン中 あと 1 秒")]
        [TestCase(0, "ダウン中 あと 0 秒")]
        public void DownedShowsTheBleedoutSecondsLeft(int seconds, string expected)
        {
            Assert.That(GameText.Resolve(HudText.Downed(seconds)), Is.EqualTo(expected));
        }

        [TestCase(12, 40, "ダウン中 あと 12 秒 (蘇生 40%)")]
        [TestCase(3, 99, "ダウン中 あと 3 秒 (蘇生 99%)")]
        [TestCase(12, 0, "ダウン中 あと 12 秒")]
        public void DownedAddsTheReviveProgressOnlyWhileSomeoneRevives(int seconds, int percent, string expected)
        {
            Assert.That(GameText.Resolve(HudText.Downed(seconds, percent)), Is.EqualTo(expected));
        }

        [Test]
        public void DownedRejectsNegativeSecondsAndPercentOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.Downed(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.Downed(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.Downed(5, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.Downed(5, 101));
        }

        [TestCase(0, "蘇生中 0%")]
        [TestCase(40, "蘇生中 40%")]
        [TestCase(100, "蘇生中 100%")]
        public void RevivingShowsThePercent(int percent, string expected)
        {
            Assert.That(GameText.Resolve(HudText.Reviving(percent)), Is.EqualTo(expected));
        }

        [Test]
        public void RevivingRejectsPercentOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.Reviving(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.Reviving(101));
        }

        [TestCase(1, "保護中 あと 1 秒")]
        [TestCase(2, "保護中 あと 2 秒")]
        public void SpawnProtectedShowsTheSeconds(int seconds, string expected)
        {
            Assert.That(GameText.Resolve(HudText.SpawnProtected(seconds)), Is.EqualTo(expected));
        }

        [Test]
        public void SpawnProtectedRejectsNegativeSeconds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.SpawnProtected(-1));
        }

        [Test]
        public void StatusEffectShowsTheKindAndTheSeconds()
        {
            Assert.That(GameText.Resolve(HudText.StatusEffect(StatusEffectKinds.Burn, 3)), Is.EqualTo("燃焼中 あと 3 秒"));
        }

        [Test]
        public void StatusEffectRejectsAnEmptyKindAndNegativeSeconds()
        {
            Assert.Throws<ArgumentException>(() => HudText.StatusEffect(string.Empty, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.StatusEffect(StatusEffectKinds.Burn, -1));
        }

        [TestCase(15, "操作がありません あと 15 秒で切断されます")]
        [TestCase(1, "操作がありません あと 1 秒で切断されます")]
        public void IdleWarningShowsTheSecondsUntilTheKick(int seconds, string expected)
        {
            Assert.That(GameText.Resolve(HudText.IdleWarning(seconds)), Is.EqualTo(expected));
        }

        [Test]
        public void IdleWarningRejectsNegativeSeconds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.IdleWarning(-1));
        }

        [TestCase(RespawnMode.Individual, 3, "リスポーンまで 3 秒")]
        [TestCase(RespawnMode.Wave, 7, "リスポーンまで 7 秒")]
        [TestCase(RespawnMode.None, 0, "次の試合まで復活なし")]
        [TestCase(RespawnMode.None, -1, "次の試合まで復活なし")]
        public void RespawnNoticeFollowsTheRespawnMode(RespawnMode mode, int seconds, string expected)
        {
            Assert.That(GameText.Resolve(HudText.RespawnNotice(mode, seconds)), Is.EqualTo(expected));
        }

        [Test]
        public void RespawnNoticeRejectsNegativeSecondsAndUnknownModes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.RespawnNotice(RespawnMode.Wave, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.RespawnNotice((RespawnMode)99, 1));
        }

        [TestCase(3, "チェックポイントから再開まで 3 秒")]
        [TestCase(0, "チェックポイントから再開まで 0 秒")]
        public void CheckpointRestartShowsTheSecondsLeft(int seconds, string expected)
        {
            Assert.That(GameText.Resolve(HudText.CheckpointRestart(seconds)), Is.EqualTo(expected));
        }

        [Test]
        public void CheckpointRestartRejectsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.CheckpointRestart(-1));
        }

        [TestCase(Team.Alpha, 0, "ALPHA 0")]
        [TestCase(Team.Bravo, 3, "BRAVO 3")]
        [TestCase(Team.Alpha, 12, "ALPHA 12")]
        public void TeamScoreIsTheTeamNameAndTheScore(Team team, int score, string expected)
        {
            Assert.That(GameText.Resolve(HudText.TeamScore(team, score)), Is.EqualTo(expected));
        }

        [Test]
        public void ResultNamesTheWinnerOrADraw()
        {
            Assert.That(GameText.Resolve(HudText.Result(Team.Alpha)), Is.EqualTo("ALPHA の勝ち"));
            Assert.That(GameText.Resolve(HudText.Result(Team.Bravo)), Is.EqualTo("BRAVO の勝ち"));
            Assert.That(HudText.Result(null), Is.EqualTo(HudText.Draw));
            Assert.That(GameText.Resolve(HudText.Draw), Is.EqualTo("引き分け"));
        }

        [Test]
        public void SoloResultSaysClearedOrFailedWithTheRestartKey()
        {
            Assert.That(GameText.Resolve(HudText.SoloResult(MatchOutcome.Won)), Is.EqualTo("クリア"));
            Assert.That(GameText.Resolve(HudText.SoloResult(MatchOutcome.Lost)), Is.EqualTo("失敗"));
            Assert.That(HudText.SoloResult(MatchOutcome.Won), Is.EqualTo(new LocalizedText("hud.solo.won")));
            Assert.That(HudText.SoloResult(MatchOutcome.Draw), Is.EqualTo(HudText.Draw));
            Assert.That(GameText.Resolve(HudText.Restart("R")), Is.EqualTo("[R] でやり直す"));
            Assert.That(HudText.Restart("R"), Is.EqualTo(new LocalizedText("hud.solo.restart", "R")));
            Assert.Throws<ArgumentException>(() => HudText.Restart(string.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.SoloResult((MatchOutcome)99));
        }

        [Test]
        public void WordsAreKeysWithTheirArguments()
        {
            Assert.That(HudText.TeamName(Team.Alpha), Is.EqualTo(new LocalizedText("hud.team.alpha")));
            Assert.That(HudText.TeamScore(Team.Bravo, 3), Is.EqualTo(new LocalizedText("hud.score", new LocalizedText("hud.team.bravo"), 3)));
            Assert.That(HudText.Result(Team.Alpha), Is.EqualTo(new LocalizedText("hud.wins", new LocalizedText("hud.team.alpha"))));
            Assert.That(HudText.Draw, Is.EqualTo(new LocalizedText("hud.draw")));
            Assert.That(HudText.Respawn(3), Is.EqualTo(new LocalizedText("hud.respawn", 3)));
            Assert.That(HudText.Downed(12, 40), Is.EqualTo(new LocalizedText("hud.downedWithRevive", new LocalizedText("hud.downed", 12), 40)));
            Assert.That(HudNumberFormat.Reloading, Is.EqualTo(new LocalizedText("hud.reloading")));
        }

        [TestCase(3, "リスポーンまで 3 秒")]
        [TestCase(0, "リスポーンまで 0 秒")]
        public void RespawnShowsTheSecondsLeft(int seconds, string expected)
        {
            Assert.That(GameText.Resolve(HudText.Respawn(seconds)), Is.EqualTo(expected));
        }

        [Test]
        public void RespawnRejectsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.Respawn(-1));
        }

        [TestCase(0, "グレネード 0")]
        [TestCase(2, "グレネード 2")]
        [TestCase(12, "グレネード 12")]
        public void GrenadesIsThePrefixAndTheCount(int remaining, string expected)
        {
            Assert.That(GameText.Resolve(HudText.Grenades(remaining)), Is.EqualTo(expected));
        }

        [Test]
        public void GrenadesRejectsANegativeCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.Grenades(-1));
        }

        [TestCase(3.42f, "3.5")]
        [TestCase(3f, "3.0")]
        [TestCase(0.01f, "0.1")]
        [TestCase(0f, "0.0")]
        public void AbilityWaitSecondsShowsTheTenthsRoundedUp(float seconds, string expected)
        {
            Assert.That(HudText.AbilityWaitSeconds(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void AbilityWaitSecondsRejectsNegativeAndInfiniteSeconds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.AbilityWaitSeconds(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.AbilityWaitSeconds(float.PositiveInfinity));
        }

        [Test]
        public void AbilityShowsChargesWaitAndUse()
        {
            var slot = new AbilitySlot(new AbilitySpec(2, 4f, 1f, 0f));
            Assert.That(HudText.Ability(1, slot), Is.EqualTo(new LocalizedText("hud.ability", 1, 2, 2)));

            slot.TryStart();
            Assert.That(HudText.Ability(1, slot), Is.EqualTo(new LocalizedText("hud.ability.active", 1, 1, 2)));

            slot.End();
            slot.Tick(0.42f);
            Assert.That(HudText.Ability(2, slot), Is.EqualTo(new LocalizedText("hud.ability.wait", 2, 1, 2, "0.6")));

            slot.Tick(1f);
            Assert.That(HudText.Ability(2, slot), Is.EqualTo(new LocalizedText("hud.ability", 2, 1, 2)));
        }

        [Test]
        public void AbilityShowsOnlyTheCountWhenChargesNeverReturn()
        {
            var slot = new AbilitySlot(new AbilitySpec(1, 0f, 0f, 0f));
            slot.TryStart();
            slot.End();

            Assert.That(HudText.Ability(1, slot), Is.EqualTo(new LocalizedText("hud.ability", 1, 0, 1)));
        }

        [Test]
        public void AbilityRejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudText.Ability(0, new AbilitySlot(AbilitySpec.Default)));
            Assert.Throws<ArgumentNullException>(() => HudText.Ability(1, null));
        }
    }
}

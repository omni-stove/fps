using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class HordeHudFormatterTests
    {
        private const float RestSeconds = 5f;

        private static readonly string[] _fightingLines = { "WAVE 1/2", "残りの敵 2", "通貨 20", "[E] Wall-North を直す (30)" };
        private static readonly string[] _awaitingLines = { "WAVE 1/2", "残りの敵 2", "通貨 20", "波を越えると復活" };

        [Test]
        public void TheSingleLinesReadAsDocumented()
        {
            Assert.That(Localizer.Resolve(HordeHudFormatter.Wave(2, 5)), Is.EqualTo("WAVE 2/5"));
            Assert.That(Localizer.Resolve(HordeHudFormatter.Wave(2, null)), Is.EqualTo("WAVE 2"));
            Assert.That(Localizer.Resolve(HordeHudFormatter.Wave(0, 5)), Is.EqualTo("WAVE -/5"));
            Assert.That(Localizer.Resolve(HordeHudFormatter.RemainingEnemies(4)), Is.EqualTo("残りの敵 4"));
            Assert.That(Localizer.Resolve(HordeHudFormatter.NextWave(4.2f)), Is.EqualTo("次の波まで 5 秒"));
            Assert.That(Localizer.Resolve(HordeHudFormatter.Currency(120)), Is.EqualTo("通貨 120"));
            Assert.That(Localizer.Resolve(HordeHudFormatter.Victory), Is.EqualTo("防衛成功"));
            Assert.That(Localizer.Resolve(HordeHudFormatter.Defeat), Is.EqualTo("防衛失敗"));
            Assert.That(Localizer.Resolve(HordeHudFormatter.ReturnsAfterWave), Is.EqualTo("波を越えると復活"));

            Assert.That(
                Localizer.Resolve(HordeHudFormatter.Prompt(Offer(HordeFortificationAction.Repair, "Wall-North", 30, HordePurchaseVerdict.Ok, "E"))),
                Is.EqualTo("[E] Wall-North を直す (30)"));
            Assert.That(
                Localizer.Resolve(HordeHudFormatter.Prompt(Offer(HordeFortificationAction.Build, "Barricade NE", 40, HordePurchaseVerdict.Ok, null))),
                Is.EqualTo("Barricade_NE を置く (40)"));
            Assert.That(
                Localizer.Resolve(HordeHudFormatter.Prompt(Offer(HordeFortificationAction.Repair, "Wall-North", 30, HordePurchaseVerdict.WaveInProgress, "E"))),
                Is.EqualTo("Wall-North を直す (30): 波の間だけ"));
            Assert.That(
                Localizer.Resolve(HordeHudFormatter.Prompt(Offer(HordeFortificationAction.Build, "Wall-North", 30, HordePurchaseVerdict.NotEnoughCurrency, "E"))),
                Is.EqualTo("Wall-North を置く (30): 通貨が足りない"));

            var english = Localizer.TableOf(GameLanguage.English);
            Assert.That(english.Resolve(HordeHudFormatter.RemainingEnemies(4)), Is.EqualTo("Enemies left 4"));
            Assert.That(
                english.Resolve(HordeHudFormatter.Prompt(Offer(HordeFortificationAction.Repair, "Wall-North", 30, HordePurchaseVerdict.NotEnoughCurrency, "E"))),
                Is.EqualTo("Repair Wall-North (30): not enough currency"));
        }

        [Test]
        public void DuringAWaveTheLinesShowTheWaveTheEnemiesTheCurrencyAndTheOffer()
        {
            var horde = Playing(2);
            horde.Session.Tick(3.4f);
            var offer = Offer(HordeFortificationAction.Repair, "Wall-North", 30, HordePurchaseVerdict.Ok, "E");
            var status = Status(new HordeStatus(horde, 0, offer), false);
            var formatter = new HordeHudFormatter();

            Assert.That(Resolve(Lines(status)), Is.EqualTo(_fightingLines));
            Assert.That(Localizer.Resolve(formatter.Time(status)), Is.EqualTo("00:04"), "the horde counts the time up");
        }

        [Test]
        public void ADownedPlayerIsToldTheyReturnAfterTheWave()
        {
            var horde = Playing(2);
            horde.Session.ReportDeath(0);
            var offer = Offer(HordeFortificationAction.Repair, "Wall-North", 30, HordePurchaseVerdict.Ok, "E");

            Assert.That(Resolve(Lines(Status(new HordeStatus(horde, 0, offer), false))), Is.EqualTo(_awaitingLines));
        }

        [Test]
        public void TheEndShowsTheDefeat()
        {
            var horde = Playing(1);
            horde.Session.ReportDeath(0);
            horde.Session.Tick(0f);

            var lines = Resolve(Lines(Status(new HordeStatus(horde, 0, null), true)));

            Assert.That(horde.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(lines, Does.Contain("防衛失敗").And.Not.Contain("波を越えると復活"));
        }

        [Test]
        public void WithoutALocalPlayerTheOfferIsDropped()
        {
            var horde = Playing(1);
            var detail = new HordeStatus(horde, null, Offer(HordeFortificationAction.Build, "Wall-North", 30, HordePurchaseVerdict.Ok, "E"));

            Assert.That(detail.Offer, Is.Null);
            Assert.That(Resolve(Lines(Status(detail, false))), Has.Length.EqualTo(3));
        }

        // オンライン (ClientHordeStatus) はスナップショットの値で HordeStatus を作るので、Horde が無くても同じ行になる
        [Test]
        public void AStatusFromTheServerReadsTheSameLines()
        {
            var fighting = new HordeStatus(1, 2, WavePhase.Fighting, 2, 0f, 20, false, false, 3.4d, 0, false);
            var awaiting = new HordeStatus(1, 2, WavePhase.Fighting, 2, 0f, 20, false, false, 3.4d, 0, true);
            var resting = new HordeStatus(1, 2, WavePhase.Resting, 0, 4.2f, 40, false, false, 30d, 0, false);
            var won = new HordeStatus(2, 2, WavePhase.Completed, 0, 0f, 60, true, true, 90d, 0, false);
            var formatter = new HordeHudFormatter();

            Assert.That(Resolve(Lines(Status(fighting, false))), Is.EqualTo(new[] { "WAVE 1/2", "残りの敵 2", "通貨 20" }));
            Assert.That(Localizer.Resolve(formatter.Time(Status(fighting, false))), Is.EqualTo("00:04"));
            Assert.That(Resolve(Lines(Status(awaiting, false))), Is.EqualTo(_awaitingLines));
            Assert.That(Resolve(Lines(Status(resting, false))), Is.EqualTo(new[] { "WAVE 1/2", "次の波まで 5 秒", "通貨 40" }));
            Assert.That(Resolve(Lines(Status(won, true))), Is.EqualTo(new[] { "WAVE 2/2", "通貨 60", "防衛成功" }));
        }

        private static HordeFortificationOffer Offer(HordeFortificationAction action, string name, int cost, HordePurchaseVerdict verdict, string key)
        {
            return new HordeFortificationOffer(action, name, cost, verdict, key);
        }

        private static Horde Playing(int players)
        {
            var schedule = WaveSchedule.FromTable(new[] { new Wave(2, 1f, 2, RestSeconds), new Wave(2, 1f, 2, RestSeconds) });
            var horde = new Horde(schedule, new HordeSpec(Side.Alpha, Side.Bravo, 20, 10, 5, true));
            for (var id = 0; id < players; id++)
            {
                horde.Session.AddPlayer(id, Side.Alpha);
            }

            horde.Session.Start();
            schedule.Start();
            return horde;
        }

        private static ModeStatus Status(HordeStatus detail, bool isEnded)
        {
            return new ModeStatus(null, null, Side.Alpha, null, isEnded, null, detail);
        }

        private static List<LocalizedText> Lines(ModeStatus status)
        {
            var lines = new List<LocalizedText>();
            new HordeHudFormatter().AppendLines(status, lines);
            return lines;
        }

        private static string[] Resolve(IReadOnlyList<LocalizedText> lines)
        {
            var resolved = new string[lines.Count];
            for (var i = 0; i < lines.Count; i++)
            {
                resolved[i] = Localizer.Resolve(lines[i]);
            }

            return resolved;
        }
    }
}

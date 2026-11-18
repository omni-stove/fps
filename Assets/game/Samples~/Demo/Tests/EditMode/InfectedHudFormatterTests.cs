using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class InfectedHudFormatterTests
    {
        private static readonly string[] _expectedBeforeOutbreak = { "感染開始まで 4 秒", "生存者 5 / 感染者 0" };
        private static readonly string[] _expectedInfected = { "リスポーンまで 2 秒", "生存者 3 / 感染者 2", "あなたは感染者: 生存者を全員感染させる" };
        private static readonly string[] _expectedSurvivor = { "生存者 3 / 感染者 2", "あなたは生存者: 時間切れまで生き延びる" };
        private static readonly string[] _expectedBotsOnly = { "生存者 3 / 感染者 2" };
        private static readonly string[] _expectedScores = { "感染者 4", "生存者 0" };

        [Test]
        public void TheCountdownShowsUntilTheOutbreak()
        {
            var lines = InfectedHudFormatter.HudLines(new InfectedStatus(false, 3.2f, 5, 0, false));

            Assert.That(lines.Select(Localizer.Resolve), Is.EqualTo(_expectedBeforeOutbreak));
            Assert.Throws<ArgumentNullException>(() => InfectedHudFormatter.HudLines(null));
        }

        [Test]
        public void AfterTheOutbreakTheLocalPlayerIsToldWhichSideTheyAreOn()
        {
            var infected = Status(new InfectedStatus(true, 0f, 3, 2, true), Infected.InfectedSide, 1.5f);
            var survivor = Status(new InfectedStatus(true, 0f, 3, 2, false), Infected.SurvivorSide, null);
            var botsOnly = Status(new InfectedStatus(true, 0f, 3, 2, null), null, null);

            Assert.That(ModeHudFormatters.For(infected), Is.InstanceOf<InfectedHudFormatter>());
            Assert.That(Lines(infected), Is.EqualTo(_expectedInfected));
            Assert.That(Lines(survivor), Is.EqualTo(_expectedSurvivor));
            Assert.That(Lines(botsOnly), Is.EqualTo(_expectedBotsOnly));
        }

        [Test]
        public void TheSidesAreCalledSurvivorsAndInfected()
        {
            var status = new ModeStatus(
                30f,
                new[] { new SideScore(Infected.SurvivorSide, 0d), new SideScore(Infected.InfectedSide, 4d) },
                Infected.InfectedSide,
                null,
                false,
                null,
                new InfectedStatus(true, 0f, 1, 4, true));
            var formatter = ModeHudFormatters.For(status);

            var scores = formatter.Scores(status);

            Assert.That(scores.Select(line => Localizer.Resolve(line.Text)), Is.EqualTo(_expectedScores));
            Assert.That(scores[0].IsOwn, Is.True);
            Assert.That(scores[0].IsLeading, Is.True);
            Assert.That(Localizer.Resolve(formatter.Time(status)), Is.EqualTo("00:30"));
            Assert.That(Localizer.Resolve(InfectedHudFormatter.SideName(new Side(4))), Is.EqualTo(Localizer.Resolve(ModeHudText.SideName(new Side(4)))));
        }

        [Test]
        public void TheResultNamesTheWinningSide()
        {
            var formatter = new InfectedHudFormatter();
            var detail = new InfectedStatus(true, 0f, 0, 3, true);

            Assert.That(Localizer.Resolve(formatter.Result(new ModeStatus(0f, null, null, null, true, Infected.InfectedSide, detail))), Is.EqualTo("感染者 の勝ち"));
            Assert.That(Localizer.Resolve(formatter.Result(new ModeStatus(0f, null, null, null, true, Infected.SurvivorSide, detail))), Is.EqualTo("生存者 の勝ち"));
            Assert.That(Localizer.Resolve(formatter.Result(new ModeStatus(0f, null, null, null, true, null, detail))), Is.EqualTo("引き分け"));
            Assert.That(formatter.Result(new ModeStatus(10f, null, null, null, false, null, detail)), Is.EqualTo(default(LocalizedText)));
        }

        private static ModeStatus Status(InfectedStatus detail, Side? ownSide, float? respawnSeconds)
        {
            return new ModeStatus(120f, null, ownSide, respawnSeconds, false, null, detail);
        }

        private static IEnumerable<string> Lines(ModeStatus status)
        {
            var lines = new List<LocalizedText>();
            ModeHudFormatters.For(status).AppendLines(status, lines);
            return lines.Select(Localizer.Resolve);
        }
    }
}

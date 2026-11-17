using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class FreeForAllHudFormatterTests
    {
        [Test]
        public void TheRankNamesTheRankAndTheGap()
        {
            Assert.That(Localizer.Resolve(FreeForAllHudFormatter.Rank(new FreeForAllStanding(3, 6, 1, -4))), Is.EqualTo("順位 3/6  1位と -4"));
            Assert.That(Localizer.Resolve(FreeForAllHudFormatter.Rank(new FreeForAllStanding(1, 6, 5, 2))), Is.EqualTo("順位 1/6  2位と +2"));
            Assert.That(Localizer.Resolve(FreeForAllHudFormatter.Rank(new FreeForAllStanding(1, 6, 5, 0))), Is.EqualTo("順位 1/6  1位と同点"));
            Assert.That(Localizer.Resolve(FreeForAllHudFormatter.Rank(new FreeForAllStanding(1, 1, 0, null))), Is.EqualTo("順位 1/1"));
        }

        [Test]
        public void TheRankReadsTheStandingOfTheSession()
        {
            var session = FreeForAllMode.Create(300f, 15, 3f);
            for (var id = 0; id < 3; id++)
            {
                session.Join(id);
            }

            session.Start();
            session.ReportKill(2, 0);

            Assert.That(Localizer.Resolve(FreeForAllHudFormatter.Rank(FreeForAllMode.Standing(session, new Side(0)))), Is.EqualTo("順位 2/3  1位と -1"));
            Assert.That(Localizer.Resolve(FreeForAllHudFormatter.Rank(FreeForAllMode.Standing(session, new Side(2)))), Is.EqualTo("順位 1/3  2位と +1"));
        }

        [Test]
        public void TheRespawnCountdownComesBeforeTheRank()
        {
            var status = Status(new FreeForAllStanding(2, 2, 0, -1), respawn: 1f);

            Assert.That(ModeHudFormatters.For(status), Is.InstanceOf<FreeForAllHudFormatter>());
            Assert.That(Lines(status), Is.EqualTo(new[] { "リスポーンまで 1 秒", "順位 2/2  1位と -1" }));
        }

        [Test]
        public void TheScoresAndResultAreTheModeDefaults()
        {
            var formatter = new FreeForAllHudFormatter();
            var status = new ModeStatus(
                0f,
                new[] { new SideScore(new Side(2), 1d), new SideScore(new Side(3), 3d) },
                new Side(2),
                null,
                true,
                new Side(3),
                new FreeForAllStanding(2, 2, 1, -2));

            Assert.That(formatter.Scores(status).Select(s => Localizer.Resolve(s.Text)), Is.EqualTo(new[] { "SIDE 2 1", "SIDE 3 3" }));
            Assert.That(Localizer.Resolve(formatter.Result(status)), Is.EqualTo("SIDE 3 の勝ち"));
            Assert.That(Lines(status), Is.EqualTo(new[] { "順位 2/2  1位と -2" }));
        }

        private static string[] Lines(ModeStatus status)
        {
            var lines = new List<LocalizedText>();
            new FreeForAllHudFormatter().AppendLines(status, lines);
            return lines.Select(Localizer.Resolve).ToArray();
        }

        private static ModeStatus Status(FreeForAllStanding standing, float? respawn)
        {
            return new ModeStatus(60f, null, new Side(0), respawn, false, null, standing);
        }
    }
}

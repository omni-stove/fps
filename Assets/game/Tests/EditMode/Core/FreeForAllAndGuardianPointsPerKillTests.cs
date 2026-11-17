using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class FreeForAllAndGuardianPointsPerKillTests
    {
        [Test]
        public void TheFreeForAllDefaultIsOnePointPerKill()
        {
            var session = FreeForAllMode.Create(60f, 5, 1f);
            session.AddPlayer(1, new Side(0));
            session.AddPlayer(2, new Side(1));
            session.Start();

            session.ReportKill(1, 2);

            Assert.That(FreeForAllMode.DefaultPointsPerKill, Is.EqualTo(1));
            Assert.That(session.GetScore(new Side(0)), Is.EqualTo(1d));
        }

        [Test]
        public void AFreeForAllKillScoresThePointsPerKillAndReachesTheLimitSooner()
        {
            var session = FreeForAllMode.Create(60f, 6, 0f, 3);
            session.AddPlayer(1, new Side(0));
            session.AddPlayer(2, new Side(1));
            session.Start();

            session.ReportKill(1, 2);
            Assert.That(session.GetScore(new Side(0)), Is.EqualTo(3d));
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));

            session.ReportKill(1, 2);
            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo(new Side(0)));
            Assert.That(FreeForAllMode.Standing(session, new Side(0)).Score, Is.EqualTo(6));
        }

        [Test]
        public void AGuardianKillScoresThePointsPerKill()
        {
            var session = GuardianMode.CreateSession(60f, 1f, _ => 0, out _, 2);
            session.AddPlayer(1, Side.Alpha);
            session.AddPlayer(2, Side.Alpha);
            session.AddPlayer(3, Side.Bravo);
            session.AddPlayer(4, Side.Bravo);
            session.Start();

            session.ReportKill(1, 4);

            Assert.That(GuardianMode.DefaultPointsPerKill, Is.EqualTo(1));
            Assert.That(session.GetScore(Side.Alpha), Is.EqualTo(2d));
        }

        [Test]
        public void AGuardianSessionWithRandomLeadersPassesThePointsPerKill()
        {
            var session = GuardianMode.CreateSession(60f, 1f, new Random(3), out _, 5);
            session.AddPlayer(1, Side.Alpha);
            session.AddPlayer(2, Side.Bravo);
            session.Start();

            session.ReportKill(1, 2);

            Assert.That(session.GetScore(Side.Alpha), Is.EqualTo(5d));
        }

        [Test]
        public void ZeroPointsPerKillIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FreeForAllMode.Create(60f, 5, 1f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => GuardianMode.CreateSession(60f, 1f, _ => 0, out _, 0));
        }
    }
}

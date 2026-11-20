using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DominationSessionTests
    {
        // 拠点 2 つ、1 秒で占領、1 秒に 10 点、30 点で勝ち、制限時間 60 秒。
        private static DominationSpec Spec() => new DominationSpec(2, 1f, 10f, 30, 60f);

        [Test]
        public void APlayerAddedToTheSessionCanStandOnAPointAndCaptureIt()
        {
            var domination = new Domination(Spec());
            domination.Session.AddPlayer(0, Side.Alpha);
            domination.Session.AddPlayer(1, Side.Bravo);
            domination.Session.Start();

            Assert.That(domination.GetPlayerPoint(0), Is.Null);
            domination.SetPlayerPoint(0, 1);
            domination.Session.Tick(1f);

            Assert.That(domination.GetPlayerPoint(0), Is.EqualTo(1));
            Assert.That(domination.GetOwner(1), Is.EqualTo((Team?)Team.Alpha));
            Assert.That(domination.GetOwner(0), Is.Null);

            domination.Session.Tick(1f);

            Assert.That(domination.Session.GetScore(Side.Alpha), Is.EqualTo(10d));
            Assert.That(domination.GetScore(Team.Alpha), Is.EqualTo(10f));
        }

        [Test]
        public void AnUnknownPlayerOrPointIsRejected()
        {
            var domination = new Domination(Spec());
            domination.Session.AddPlayer(0, Side.Alpha);

            Assert.Throws<ArgumentOutOfRangeException>(() => domination.SetPlayerPoint(5, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => domination.GetPlayerPoint(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => domination.SetPlayerPoint(0, 2));
        }

        [Test]
        public void TheRespawnPolicyGoesToTheSession()
        {
            Assert.That(new Domination(Spec()).Session.TracksDowns, Is.False, "the server respawns on its own");
            Assert.That(new Domination(Spec(), new IndividualRespawn(2f)).Session.TracksDowns, Is.True);
        }
    }
}

using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DominationStatusTests
    {
        // 拠点 2 つ、1 秒で占領、1 秒に 10 点、30 点で勝ち、制限時間 60 秒。
        private static Domination TwoPointMatch()
        {
            var domination = new Domination(new DominationSpec(2, 1f, 10f, 30, 60f));
            domination.AddPlayer(0, Team.Alpha);
            domination.AddPlayer(1, Team.Bravo);
            domination.AddPlayer(2, Team.Bravo);
            return domination;
        }

        [Test]
        public void OfCopiesTheScoresTimeAndEachPoint()
        {
            var domination = TwoPointMatch();
            domination.Start();
            domination.SetPlayerPoint(0, 0);
            domination.Tick(1f);
            domination.Tick(0.55f);
            domination.SetPlayerPoint(1, 1);
            domination.SetPlayerPoint(2, 0);
            domination.Tick(0.5f);

            var status = DominationStatus.Of(domination);

            Assert.That(status.AlphaScore, Is.EqualTo(domination.GetScore(Team.Alpha)));
            Assert.That(status.BravoScore, Is.EqualTo(0f));
            Assert.That(status.GetScore(Team.Alpha), Is.EqualTo(status.AlphaScore));
            Assert.That(status.RemainingSeconds, Is.EqualTo(domination.RemainingSeconds));
            Assert.That(status.Points.Count, Is.EqualTo(2));
            Assert.That(status.Points[0], Is.EqualTo(new ControlPointStatus(Team.Alpha, 100, true)));
            Assert.That(status.Points[1], Is.EqualTo(new ControlPointStatus(null, -50, false)));
            Assert.That(status.IsEnded, Is.False);
            Assert.That(status.Winner, Is.Null);
        }

        [Test]
        public void OfCarriesTheWinnerOnceTheMatchEnds()
        {
            var domination = TwoPointMatch();
            domination.Start();
            domination.SetPlayerPoint(0, 0);
            domination.Tick(1f);
            domination.Tick(3f);

            var status = DominationStatus.Of(domination);

            Assert.That(status.IsEnded, Is.True);
            Assert.That(status.Winner, Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void OfAMatchThatHasNotStartedIsNotEnded()
        {
            var status = DominationStatus.Of(TwoPointMatch());

            Assert.That(status.IsEnded, Is.False);
            Assert.That(status.RemainingSeconds, Is.EqualTo(60f));
            Assert.That(status.Points[0], Is.EqualTo(new ControlPointStatus(null, 0, false)));
        }

        [Test]
        public void TheWinnerIsDroppedBeforeTheEnd()
        {
            var status = new DominationStatus(1f, 2f, 10f, null, false, Team.Bravo);

            Assert.That(status.Winner, Is.Null);
            Assert.That(status.Points, Is.Empty);
        }

        [Test]
        public void InvalidValuesAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => DominationStatus.Of(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DominationStatus(0f, 0f, -1f, null, false, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DominationStatus(0f, 0f, float.NaN, null, false, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DominationStatus(0f, 0f, 1f, null, false, null).GetScore((Team)99));
        }

        [Test]
        public void PercentOfTruncatesTowardZeroAndClamps()
        {
            Assert.That(ControlPointStatus.PercentOf(0.509f), Is.EqualTo(50));
            Assert.That(ControlPointStatus.PercentOf(-0.509f), Is.EqualTo(-50));
            Assert.That(ControlPointStatus.PercentOf(0.009f), Is.EqualTo(0));
            Assert.That(ControlPointStatus.PercentOf(2f), Is.EqualTo(100));
            Assert.That(ControlPointStatus.PercentOf(-2f), Is.EqualTo(-100));
            Assert.That(ControlPointStatus.PercentOf(float.NaN), Is.EqualTo(0));
        }
    }
}

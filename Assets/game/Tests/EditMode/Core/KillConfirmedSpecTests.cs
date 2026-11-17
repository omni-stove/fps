using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class KillConfirmedSpecTests
    {
        private const int _alpha = 1;
        private const int _bravo = 2;

        [Test]
        public void TheConfirmPointsAndTagRadiusDefaultToTheOldValues()
        {
            var spec = new KillConfirmedSpec(10, 300f, 3f, 10f);

            Assert.That(spec.PointsPerConfirm, Is.EqualTo(1));
            Assert.That(spec.TagPickupRadius, Is.EqualTo(1f));
            Assert.That(KillConfirmedSpec.DefaultPointsPerConfirm, Is.EqualTo(1));
            Assert.That(KillConfirmedSpec.DefaultTagPickupRadius, Is.EqualTo(1f));
        }

        [Test]
        public void TheSpecKeepsTheConfirmPointsAndTagRadiusItWasGiven()
        {
            var spec = new KillConfirmedSpec(10, 300f, 3f, 10f, 3, 4f);

            Assert.That(spec.PointsPerConfirm, Is.EqualTo(3));
            Assert.That(spec.TagPickupRadius, Is.EqualTo(4f));
        }

        [Test]
        public void TheSpecRejectsInvalidConfirmPointsAndTagRadius()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillConfirmedSpec(10, 300f, 3f, 10f, 0, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillConfirmedSpec(10, 300f, 3f, 10f, -1, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillConfirmedSpec(10, 300f, 3f, 10f, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillConfirmedSpec(10, 300f, 3f, 10f, 1, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillConfirmedSpec(10, 300f, 3f, 10f, 1, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillConfirmedSpec(10, 300f, 3f, 10f, 1, float.PositiveInfinity));
        }

        [Test]
        public void OneConfirmScoresThePointsPerConfirm()
        {
            var mode = Start(scoreLimit: 10, pointsPerConfirm: 3);
            var tag = mode.DropTag(_bravo);

            Assert.That(mode.Collect(tag.Id, _alpha), Is.EqualTo(KillConfirmedResult.Confirmed));

            Assert.That(mode.Session.GetScore(Side.Alpha), Is.EqualTo(3d));
            Assert.That(mode.GetConfirmed(Side.Alpha), Is.EqualTo(1), "the confirm count still goes up by one");
        }

        [Test]
        public void ADenialScoresNothingWhateverThePointsPerConfirm()
        {
            var mode = Start(scoreLimit: 10, pointsPerConfirm: 3);
            var tag = mode.DropTag(_bravo);

            Assert.That(mode.Collect(tag.Id, _bravo), Is.EqualTo(KillConfirmedResult.Denied));

            Assert.That(mode.Session.GetScore(Side.Alpha), Is.Zero);
            Assert.That(mode.Session.GetScore(Side.Bravo), Is.Zero);
        }

        [Test]
        public void TheScoreLimitCountsPointsNotConfirms()
        {
            var mode = Start(scoreLimit: 5, pointsPerConfirm: 3);

            mode.Collect(mode.DropTag(_bravo).Id, _alpha);
            Assert.That(mode.Session.State, Is.EqualTo(GameModeState.Playing), "3 points are short of 5");

            mode.Collect(mode.DropTag(_bravo).Id, _alpha);
            Assert.That(mode.Session.GetScore(Side.Alpha), Is.EqualTo(6d));
            Assert.That(mode.Session.State, Is.EqualTo(GameModeState.Ended), "the second confirm passes the limit");
            Assert.That(mode.Session.Winner, Is.EqualTo((Side?)Side.Alpha));
        }

        private static KillConfirmed Start(int scoreLimit, int pointsPerConfirm)
        {
            var mode = new KillConfirmed(new KillConfirmedSpec(scoreLimit, 300f, 3f, 10f, pointsPerConfirm));
            mode.Session.AddPlayer(_alpha, Side.Alpha);
            mode.Session.AddPlayer(_bravo, Side.Bravo);
            mode.Session.Start();
            return mode;
        }
    }
}

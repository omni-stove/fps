using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    // 期待値は SkillSpec.Default (K 32・下限 0・上限 3000) で、変化 = K × (結果 - 1 / (1 + 10^((相手 - 自分) / 400))) を四捨五入して出す。
    public sealed class SkillRatingTests
    {
        private readonly SkillRating _rating = new SkillRating(SkillSpec.Default);

        [TestCase(MatchOutcome.Won, 1016)]
        [TestCase(MatchOutcome.Lost, 984)]
        [TestCase(MatchOutcome.Draw, 1000)]
        public void AnEvenMatchMovesByHalfOfK(MatchOutcome outcome, int expected)
        {
            Assert.That(_rating.Next(1000, 1000, outcome), Is.EqualTo(expected));
        }

        [TestCase(MatchOutcome.Won, 1208)]
        [TestCase(MatchOutcome.Lost, 1176)]
        [TestCase(MatchOutcome.Draw, 1192)]
        public void TheStrongerSideGainsLittleAndLosesMuch(MatchOutcome outcome, int expected)
        {
            Assert.That(_rating.Next(1200, 1000, outcome), Is.EqualTo(expected));
        }

        [TestCase(MatchOutcome.Won, 1024)]
        [TestCase(MatchOutcome.Lost, 992)]
        [TestCase(MatchOutcome.Draw, 1008)]
        public void TheWeakerSideGainsMuchAndLosesLittle(MatchOutcome outcome, int expected)
        {
            Assert.That(_rating.Next(1000, 1200, outcome), Is.EqualTo(expected));
        }

        [Test]
        public void TheExpectedScoreFollowsTheDifference()
        {
            Assert.That(SkillRating.Expected(1000, 1000), Is.EqualTo(0.5d));
            Assert.That(SkillRating.Expected(1200, 1000), Is.EqualTo(0.759746926648d).Within(1e-9));
            Assert.That(SkillRating.Expected(1000, 1200), Is.EqualTo(0.240253073352d).Within(1e-9));
        }

        [Test]
        public void TheScoresAreOneHalfAndZero()
        {
            Assert.That(SkillRating.ScoreOf(MatchOutcome.Won), Is.EqualTo(1d));
            Assert.That(SkillRating.ScoreOf(MatchOutcome.Draw), Is.EqualTo(0.5d));
            Assert.That(SkillRating.ScoreOf(MatchOutcome.Lost), Is.EqualTo(0d));
        }

        [Test]
        public void TheRatingStopsAtTheCeiling()
        {
            Assert.That(_rating.Next(2990, 2990, MatchOutcome.Won), Is.EqualTo(3000));
            Assert.That(_rating.Next(3000, 3000, MatchOutcome.Won), Is.EqualTo(3000));
        }

        [Test]
        public void TheRatingStopsAtTheFloor()
        {
            Assert.That(_rating.Next(10, 10, MatchOutcome.Lost), Is.EqualTo(0));
            Assert.That(_rating.Next(0, 0, MatchOutcome.Lost), Is.EqualTo(0));
        }

        [Test]
        public void ARatingOutsideTheBoundsIsBroughtBackIn()
        {
            var narrow = new SkillRating(new SkillSpec(1000, 32f, 500, 1500));

            Assert.That(narrow.Next(2000, 2000, MatchOutcome.Draw), Is.EqualTo(1500));
            Assert.That(narrow.Next(100, 100, MatchOutcome.Draw), Is.EqualTo(500));
            Assert.That(narrow.Clamp(long.MaxValue), Is.EqualTo(1500));
            Assert.That(narrow.Clamp(long.MinValue), Is.EqualTo(500));
        }

        [Test]
        public void TheKFactorScalesTheChange()
        {
            var slow = new SkillRating(new SkillSpec(1500, 16f, 100, 2000));

            Assert.That(slow.Next(1500, 1500, MatchOutcome.Won), Is.EqualTo(1508));
            Assert.That(slow.Next(1500, 1500, MatchOutcome.Lost), Is.EqualTo(1492));
        }

        [Test]
        public void AHalfPointIsRoundedAwayFromZero()
        {
            var tiny = new SkillRating(new SkillSpec(1000, 1f, 0, 3000));

            Assert.That(tiny.Next(1000, 1000, MatchOutcome.Won), Is.EqualTo(1001));
            Assert.That(tiny.Next(1000, 1000, MatchOutcome.Lost), Is.EqualTo(999));
        }

        [Test]
        public void RejectsMissingOrUnknownValues()
        {
            Assert.Throws<ArgumentNullException>(() => new SkillRating(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rating.Next(1000, 1000, (MatchOutcome)99));
        }
    }
}

using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MatchResultTests
    {
        [Test]
        public void KeepsTheOutcomeAndTheCounts()
        {
            var result = new MatchResult(MatchOutcome.Draw, 12, 4, 1500);

            Assert.That(result.Outcome, Is.EqualTo(MatchOutcome.Draw));
            Assert.That(result.Kills, Is.EqualTo(12));
            Assert.That(result.Deaths, Is.EqualTo(4));
            Assert.That(result.OwnScore, Is.EqualTo(1500));
        }

        [Test]
        public void ZeroCountsAreAllowed()
        {
            var result = new MatchResult(MatchOutcome.Lost, 0, 0, 0);

            Assert.That(result, Is.EqualTo(new MatchResult(MatchOutcome.Lost, 0, 0, 0)));
            Assert.That(result == new MatchResult(MatchOutcome.Won, 0, 0, 0), Is.False);
        }

        [Test]
        public void RejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchResult((MatchOutcome)99, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchResult(MatchOutcome.Won, -1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchResult(MatchOutcome.Won, 0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchResult(MatchOutcome.Won, 0, 0, -1));
        }
    }
}

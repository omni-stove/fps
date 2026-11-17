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
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchResult(MatchOutcome.Won, 0, 0, 0, true, -1));
        }

        [Test]
        public void AResultIsUnratedUnlessSaid()
        {
            var result = new MatchResult(MatchOutcome.Won, 1, 0, 10);

            Assert.That(result.Rated, Is.False);
            Assert.That(result.OpponentRating, Is.Null);
            Assert.That(result.ToString(), Is.EqualTo("Won 1/0 score 10"));
            Assert.That(default(MatchResult).Rated, Is.False);
        }

        [Test]
        public void ARatedResultKeepsTheOpponentRatingIfGiven()
        {
            var known = new MatchResult(MatchOutcome.Lost, 1, 2, 10, true, 1200);
            var unknown = new MatchResult(MatchOutcome.Lost, 1, 2, 10, true);

            Assert.That(known.Rated, Is.True);
            Assert.That(known.OpponentRating, Is.EqualTo(1200));
            Assert.That(known.ToString(), Is.EqualTo("Lost 1/2 score 10 rated vs 1200"));
            Assert.That(unknown.Rated, Is.True);
            Assert.That(unknown.OpponentRating, Is.Null);
            Assert.That(unknown.ToString(), Is.EqualTo("Lost 1/2 score 10 rated vs ?"));
        }

        [Test]
        public void EqualityComparesTheRating()
        {
            var rated = new MatchResult(MatchOutcome.Won, 0, 0, 0, true, 1000);

            Assert.That(rated, Is.EqualTo(new MatchResult(MatchOutcome.Won, 0, 0, 0, true, 1000)));
            Assert.That(rated.GetHashCode(), Is.EqualTo(new MatchResult(MatchOutcome.Won, 0, 0, 0, true, 1000).GetHashCode()));
            Assert.That(rated == new MatchResult(MatchOutcome.Won, 0, 0, 0, true, 1001), Is.False);
            Assert.That(rated == new MatchResult(MatchOutcome.Won, 0, 0, 0, true), Is.False);
            Assert.That(new MatchResult(MatchOutcome.Won, 0, 0, 0, true) == new MatchResult(MatchOutcome.Won, 0, 0, 0), Is.False);
        }

        [Test]
        public void AnOpponentRatingNeedsARatedMatch()
        {
            var error = Assert.Throws<ArgumentException>(() => new MatchResult(MatchOutcome.Won, 0, 0, 0, false, 1000));

            Assert.That(error.ParamName, Is.EqualTo("opponentRating"));
        }
    }
}

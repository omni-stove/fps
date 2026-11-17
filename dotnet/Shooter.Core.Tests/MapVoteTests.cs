using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // 期待値は種を 1 から始まる MINSTD (x ← x * 48271 mod 2147483647) の列で手で引いたもの。種 s の最初の状態は s + 1。
    // 種 0 の列: 48271, 182605794, 1291394886, 1914720637。種 1 は各項の 2 倍、種 2 は 3 倍 (mod 2147483647)。
    public sealed class MapVoteTests
    {
        private static readonly string[] _fivePool = { "NetArena01", "NetArena02", "NetArena03", "NetArena04", "NetArena05" };
        private static readonly string[] _threePool = { "NetArena01", "NetArena02", "NetArena03" };
        private static readonly string[] _onePool = { "NetArena01" };
        private static readonly string[] _duplicatePool = { "NetArena01", "NetArena01" };
        private static readonly string[] _blankPool = { "NetArena01", " " };
        private static readonly string[] _seedTwoExpected = { "NetArena01", "NetArena03", "NetArena05" };
        private static readonly string[] _fewExpected = { "NetArena02", "NetArena03" };

        [Test]
        public void PicksThreeCandidatesWithoutTheCurrentMapBySeed()
        {
            // 種 0: 48271 % 4 = 3, 182605794 % 3 = 0, 1291394886 % 2 = 0 で 01 が外れる。
            var seedZero = new MapVote(_fivePool, "NetArena02", 15f, 0);
            Assert.That(seedZero.Candidates, Is.EqualTo(new[] { "NetArena03", "NetArena04", "NetArena05" }));

            // 種 2: 144813 % 4 = 1, 547817382 % 3 = 0, 1726701011 % 2 = 1 で 04 が外れる。
            var seedTwo = new MapVote(_fivePool, "NetArena02", 15f, 2);
            Assert.That(seedTwo.Candidates, Is.EqualTo(_seedTwoExpected));
        }

        [Test]
        public void SameSeedPicksTheSameCandidates()
        {
            var first = new MapVote(_fivePool, "NetArena02", 15f, 12345);
            var second = new MapVote(_fivePool, "NetArena02", 15f, 12345);

            Assert.That(second.Candidates, Is.EqualTo(first.Candidates));
            Assert.That(first.Candidates, Has.Count.EqualTo(3));
            Assert.That(first.Candidates, Has.No.Member("NetArena02"));
        }

        [Test]
        public void FewCandidatesAreAllOffered()
        {
            var vote = new MapVote(_threePool, "NetArena01", 15f, 0);

            Assert.That(vote.Candidates, Is.EqualTo(_fewExpected));
        }

        [Test]
        public void ConfiguredCandidateCountIsUsed()
        {
            var vote = new MapVote(_fivePool, null, 15f, 0, candidateCount: 5);

            Assert.That(vote.Candidates, Is.EqualTo(_fivePool));
        }

        [Test]
        public void RecastReplacesTheEarlierVote()
        {
            var vote = new MapVote(_threePool, "NetArena09", 15f, 0);

            Assert.That(vote.Cast(1, "NetArena01"), Is.EqualTo(MapVoteCast.Cast));
            Assert.That(vote.Cast(1, "NetArena03"), Is.EqualTo(MapVoteCast.Changed));
            Assert.That(vote.Cast(1, "NetArena03"), Is.EqualTo(MapVoteCast.Unchanged));

            Assert.That(vote.VoteOf(1), Is.EqualTo("NetArena03"));
            Assert.That(vote.VoteCount, Is.EqualTo(1));
            Assert.That(vote.Tally(), Is.EqualTo(new[]
            {
                new MapVoteCandidate("NetArena01", 0),
                new MapVoteCandidate("NetArena02", 0),
                new MapVoteCandidate("NetArena03", 1),
            }));
        }

        [Test]
        public void VoteForAMapThatIsNotOfferedIsRefused()
        {
            var vote = new MapVote(_fivePool, "NetArena02", 15f, 0);

            Assert.That(vote.Cast(1, "NetArena01"), Is.EqualTo(MapVoteCast.NotCandidate));
            Assert.That(vote.Cast(1, "NetArena02"), Is.EqualTo(MapVoteCast.NotCandidate));
            Assert.That(vote.Cast(1, null), Is.EqualTo(MapVoteCast.NotCandidate));
            Assert.That(vote.VoteCount, Is.EqualTo(0));
        }

        [Test]
        public void LeavingDropsThePlayersVote()
        {
            var vote = new MapVote(_threePool, "NetArena09", 15f, 0);
            vote.Cast(1, "NetArena01");
            vote.Cast(2, "NetArena02");
            vote.Cast(3, "NetArena02");

            Assert.That(vote.Leave(2), Is.EqualTo("NetArena02"));
            Assert.That(vote.Leave(3), Is.EqualTo("NetArena02"));
            Assert.That(vote.Leave(4), Is.Empty);
            Assert.That(vote.VotesFor("NetArena02"), Is.EqualTo(0));

            var decision = vote.Close();
            Assert.That(decision.MapId, Is.EqualTo("NetArena01"));
            Assert.That(decision.Reason, Is.EqualTo(MapVoteDecisionReason.Majority));
            Assert.That(decision.Votes, Is.EqualTo(1));
        }

        [Test]
        public void MostVotesWins()
        {
            var vote = new MapVote(_threePool, "NetArena09", 15f, 0);
            vote.Cast(1, "NetArena02");
            vote.Cast(2, "NetArena02");
            vote.Cast(3, "NetArena03");

            var decision = vote.Close();

            Assert.That(decision.MapId, Is.EqualTo("NetArena02"));
            Assert.That(decision.Reason, Is.EqualTo(MapVoteDecisionReason.Majority));
            Assert.That(decision.Votes, Is.EqualTo(2));
        }

        // 候補が 3 つだけなので選ぶのに乱数を使わず、同数の決め方が列の最初の項を使う。
        [TestCase(0, "NetArena03")] // 48271 % 2 = 1
        [TestCase(1, "NetArena01")] // 96542 % 2 = 0
        public void TieIsBrokenAmongTheTiedBySeed(int seed, string expected)
        {
            var vote = new MapVote(_threePool, "NetArena09", 15f, seed);
            vote.Cast(1, "NetArena01");
            vote.Cast(2, "NetArena03");
            vote.Cast(3, "NetArena01");
            vote.Cast(4, "NetArena03");

            var decision = vote.Close();

            Assert.That(decision.MapId, Is.EqualTo(expected));
            Assert.That(decision.Reason, Is.EqualTo(MapVoteDecisionReason.Tie));
            Assert.That(decision.Votes, Is.EqualTo(2));
        }

        [TestCase(0, "NetArena02")] // 48271 % 3 = 1
        [TestCase(2, "NetArena01")] // 144813 % 3 = 0
        public void NoVotesPicksAnOfferedCandidateBySeed(int seed, string expected)
        {
            var vote = new MapVote(_threePool, "NetArena09", 15f, seed);

            var decision = vote.Close();

            Assert.That(decision.MapId, Is.EqualTo(expected));
            Assert.That(decision.Reason, Is.EqualTo(MapVoteDecisionReason.NoVotes));
            Assert.That(decision.Votes, Is.EqualTo(0));
        }

        [Test]
        public void NoVotesDrawsAfterThePicking()
        {
            // 候補選びで 3 項を使い、4 項目の 1914720637 % 3 = 1 で 03, 04, 05 の 04 になる。
            var vote = new MapVote(_fivePool, "NetArena02", 15f, 0);

            var decision = vote.Close();

            Assert.That(decision.MapId, Is.EqualTo("NetArena04"));
            Assert.That(decision.Reason, Is.EqualTo(MapVoteDecisionReason.NoVotes));
        }

        [Test]
        public void OnlyTheCurrentMapKeepsIt()
        {
            var vote = new MapVote(_onePool, "NetArena01", 15f, 0);

            Assert.That(vote.Candidates, Is.Empty);
            var decision = vote.Close();
            Assert.That(decision.KeepsCurrentMap, Is.True);
            Assert.That(decision.Reason, Is.EqualTo(MapVoteDecisionReason.NoCandidates));
        }

        [Test]
        public void TickClosesAtTheDeadline()
        {
            var vote = new MapVote(_threePool, "NetArena09", 10f, 0);
            vote.Cast(1, "NetArena03");

            Assert.That(vote.Tick(4f), Is.False);
            Assert.That(vote.RemainingSeconds, Is.EqualTo(6f));
            Assert.That(vote.IsClosed, Is.False);

            Assert.That(vote.Tick(7f), Is.True);
            Assert.That(vote.IsClosed, Is.True);
            Assert.That(vote.RemainingSeconds, Is.EqualTo(0f));
            Assert.That(vote.Decision.MapId, Is.EqualTo("NetArena03"));

            Assert.That(vote.Tick(1f), Is.False);
        }

        [Test]
        public void ClosedVoteKeepsItsDecision()
        {
            var vote = new MapVote(_threePool, "NetArena09", 15f, 0);
            vote.Cast(1, "NetArena01");
            vote.Cast(2, "NetArena03");
            var first = vote.Close();

            Assert.That(vote.Cast(3, "NetArena03"), Is.EqualTo(MapVoteCast.Closed));
            Assert.That(vote.Leave(1), Is.Empty);
            var second = vote.Close();

            Assert.That(second.MapId, Is.EqualTo(first.MapId));
            Assert.That(first.MapId, Is.EqualTo("NetArena03"));
            Assert.That(vote.VotesFor("NetArena01"), Is.EqualTo(1));
        }

        [Test]
        public void StatusShowsTheTallyMyVoteAndDecision()
        {
            var vote = new MapVote(_threePool, "NetArena09", 15f, 0);
            vote.Cast(1, "NetArena02");
            vote.Tick(5f);

            var open = vote.StatusFor(1);
            Assert.That(open.MyVote, Is.EqualTo("NetArena02"));
            Assert.That(open.RemainingSeconds, Is.EqualTo(10f));
            Assert.That(open.IsDecided, Is.False);
            Assert.That(open.Candidates, Is.EqualTo(new[]
            {
                new MapVoteCandidate("NetArena01", 0),
                new MapVoteCandidate("NetArena02", 1),
                new MapVoteCandidate("NetArena03", 0),
            }));
            Assert.That(vote.StatusFor(2).HasVoted, Is.False);

            vote.Close();
            Assert.That(vote.StatusFor(2).DecidedMapId, Is.EqualTo("NetArena02"));
        }

        [Test]
        public void BadArgumentsAreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new MapVote(null, "NetArena01", 15f, 0));
            Assert.Throws<ArgumentException>(() => _ = new MapVote(_duplicatePool, null, 15f, 0));
            Assert.Throws<ArgumentException>(() => _ = new MapVote(_blankPool, null, 15f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new MapVote(_threePool, null, -1f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new MapVote(_threePool, null, float.NaN, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new MapVote(_threePool, null, 15f, 0, candidateCount: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MapVote(_threePool, null, 15f, 0).Tick(-1f));
        }
    }
}

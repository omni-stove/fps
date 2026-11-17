using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class MapVoteLogTests
    {
        private static readonly string[] _pool = { "NetArena01", "NetArena02", "NetArena03", "NetArena04", "NetArena05" };

        [Test]
        public void OpenedListsTheCurrentMapCandidatesAndSeconds()
        {
            var vote = new MapVote(_pool, "NetArena02", 15f, 0);

            Assert.That(
                MapVoteLog.Opened(vote),
                Is.EqualTo("[MapVote] opened current=NetArena02 candidates=[NetArena03, NetArena04, NetArena05] seconds=15.0"));
        }

        [Test]
        public void OpenedWithoutACurrentMapSaysNone()
        {
            var vote = new MapVote(new[] { "NetArena01" }, null, 7.5f, 0);

            Assert.That(MapVoteLog.Opened(vote), Is.EqualTo("[MapVote] opened current=none candidates=[NetArena01] seconds=7.5"));
        }

        [Test]
        public void CastShowsThePlayerMapAndResult()
        {
            Assert.That(MapVoteLog.Cast(1, "NetArena03", MapVoteCast.Changed), Is.EqualTo("[MapVote] cast player=1 map=NetArena03 result=Changed"));
            Assert.That(MapVoteLog.Cast(2, "bad map", MapVoteCast.NotCandidate), Is.EqualTo("[MapVote] cast player=2 map=bad_map result=NotCandidate"));
            Assert.That(MapVoteLog.Cast(3, null, MapVoteCast.NotCandidate), Is.EqualTo("[MapVote] cast player=3 map=none result=NotCandidate"));
        }

        [Test]
        public void LeftShowsTheDroppedVote()
        {
            Assert.That(MapVoteLog.Left(1, "NetArena03"), Is.EqualTo("[MapVote] left player=1 dropped=NetArena03"));
            Assert.That(MapVoteLog.Left(4, string.Empty), Is.EqualTo("[MapVote] left player=4 dropped=none"));
        }

        [Test]
        public void DecidedShowsTheMapReasonAndVotes()
        {
            Assert.That(
                MapVoteLog.Decided(new MapVoteDecision("NetArena04", MapVoteDecisionReason.Tie, 2)),
                Is.EqualTo("[MapVote] decided map=NetArena04 reason=Tie votes=2"));
            Assert.That(
                MapVoteLog.Decided(new MapVoteDecision(string.Empty, MapVoteDecisionReason.NoCandidates, 0)),
                Is.EqualTo("[MapVote] decided map=none reason=NoCandidates votes=0"));
        }

        [Test]
        public void OpenedRefusesNull()
        {
            Assert.Throws<ArgumentNullException>(() => MapVoteLog.Opened(null));
        }
    }
}

using NUnit.Framework;
using Shooter.Core;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientMapChangeTests
    {
        private ClientMapChange _change;

        [SetUp]
        public void SetUp()
        {
            _change = new ClientMapChange();
            _change.Enter("NetArena01");
        }

        [Test]
        public void TheDecidedMapIsTakenWhenTheNextMatchStarts()
        {
            Assert.That(_change.TryTakeChange(GameModeState.Playing, out _), Is.False);
            Assert.That(_change.TryTakeChange(GameModeState.Ended, out _), Is.False);

            _change.Decide("NetArena02");

            Assert.That(_change.PendingMap, Is.EqualTo("NetArena02"));
            Assert.That(_change.TryTakeChange(GameModeState.Ended, out _), Is.False, "the server still runs the old map between matches");
            Assert.That(_change.TryTakeChange(GameModeState.Waiting, out var map), Is.True);
            Assert.That(map, Is.EqualTo("NetArena02"));
            Assert.That(_change.PendingMap, Is.Null);
            Assert.That(_change.TryTakeChange(GameModeState.Playing, out _), Is.False, "the change is taken once");
        }

        [Test]
        public void AMatchStartingStraightIntoPlayingAlsoTakesTheChange()
        {
            _change.TryTakeChange(GameModeState.Ended, out _);
            _change.Decide("NetArena03");

            Assert.That(_change.TryTakeChange(GameModeState.Playing, out var map), Is.True);
            Assert.That(map, Is.EqualTo("NetArena03"));
        }

        [Test]
        public void KeepingTheCurrentMapChangesNothing()
        {
            _change.TryTakeChange(GameModeState.Ended, out _);
            _change.Decide("NetArena01");

            Assert.That(_change.PendingMap, Is.Null);
            Assert.That(_change.TryTakeChange(GameModeState.Waiting, out _), Is.False);
        }

        [Test]
        public void AnUndecidedVoteDropsTheEarlierDecision()
        {
            _change.TryTakeChange(GameModeState.Ended, out _);
            _change.Decide("NetArena02");
            _change.Decide("");

            Assert.That(_change.TryTakeChange(GameModeState.Waiting, out _), Is.False);
        }

        [Test]
        public void JoiningBetweenMatchesStillChangesOnTheFirstStartSeen()
        {
            var late = new ClientMapChange();
            late.Enter("NetArena01");
            late.Decide("NetArena02");

            Assert.That(late.TryTakeChange(GameModeState.Ended, out _), Is.False);
            Assert.That(late.TryTakeChange(GameModeState.Waiting, out var map), Is.True);
            Assert.That(map, Is.EqualTo("NetArena02"));
        }

        [Test]
        public void ADecisionKeptThroughASceneLoadIsTakenOnTheFirstSnapshotAfterEnded()
        {
            _change.Decide("NetArena02");

            Assert.That(_change.TryTakeChange(GameModeState.Waiting, out var map), Is.True, "the Ended state passed while the scene was loading");
            Assert.That(map, Is.EqualTo("NetArena02"));
        }

        [Test]
        public void EnteringTheNewMapForgetsTheOldStateAndMakesItCurrent()
        {
            _change.TryTakeChange(GameModeState.Ended, out _);
            _change.Decide("NetArena02");
            _change.TryTakeChange(GameModeState.Waiting, out _);

            _change.Enter("NetArena02");

            Assert.That(_change.CurrentMap, Is.EqualTo("NetArena02"));
            _change.Decide("NetArena02");
            Assert.That(_change.PendingMap, Is.Null);
            Assert.That(_change.TryTakeChange(GameModeState.Playing, out _), Is.False);
        }
    }
}

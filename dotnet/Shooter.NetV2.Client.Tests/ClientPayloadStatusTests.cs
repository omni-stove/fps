using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientPayloadStatusTests
    {
        private const int _self = 3;
        private const int _checkpoints = 3;

        private ClientPayloadStatus _status;

        [SetUp]
        public void SetUp()
        {
            _status = new ClientPayloadStatus(_self);
        }

        [Test]
        public void APayloadSnapshotIsShownInTheSameShapeAsOffline()
        {
            var changed = _status.Apply(
                Snapshot(tick: 10, progress: 0.4f, passed: 1, pushers: 2, attackers: Team.Bravo, alpha: 1, bravo: 2, remaining: 240), _checkpoints);

            Assert.That(changed, Is.True);
            var status = _status.Status;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.Detail, Is.InstanceOf<PayloadStatus>(), "the HUD formatter registered for PayloadStatus reads it");
            Assert.That(status.Detail, Is.SameAs(_status.Payload));
            var payload = (PayloadStatus)status.Detail;
            Assert.That(payload.Attackers, Is.EqualTo(Side.Bravo), "the cart item's team is the attackers");
            Assert.That(payload.Progress, Is.EqualTo(0.4f));
            Assert.That(payload.CheckpointsReached, Is.EqualTo(1));
            Assert.That(payload.CheckpointCount, Is.EqualTo(_checkpoints));
            Assert.That(payload.Pushers, Is.EqualTo(2));
            Assert.That(payload.IsContested, Is.False, "the first snapshot has nothing to compare with");
            Assert.That(payload.IsRollingBack, Is.False);
            Assert.That(status.RemainingSeconds, Is.EqualTo(240f));
            Assert.That(status.SideScores, Has.Count.EqualTo(2));
            Assert.That(status.SideScores[0].Side, Is.EqualTo(Side.Alpha));
            Assert.That(status.SideScores[0].Score, Is.EqualTo(1d));
            Assert.That(status.SideScores[1].Side, Is.EqualTo(Side.Bravo));
            Assert.That(status.SideScores[1].Score, Is.EqualTo(2d));
            Assert.That(status.OwnSide, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(status.RespawnSeconds, Is.Null, "the snapshot carries no owner part");
            Assert.That(status.IsEnded, Is.False);
            Assert.That(status.Winner, Is.Null);
        }

        [Test]
        public void TheSameValuesKeepTheShownStatus()
        {
            _status.Apply(Snapshot(tick: 10, progress: 0.25f, passed: 0, pushers: 0), _checkpoints);
            var shown = _status.Status;

            var changed = _status.Apply(Snapshot(tick: 11, progress: 0.25f, passed: 0, pushers: 0), _checkpoints);

            Assert.That(changed, Is.False);
            Assert.That(_status.Status, Is.SameAs(shown));
        }

        [Test]
        public void AMovingCartPublishesANewStatus()
        {
            _status.Apply(Snapshot(tick: 10, progress: 0.25f, passed: 0, pushers: 1), _checkpoints);
            var shown = _status.Status;

            var changed = _status.Apply(Snapshot(tick: 11, progress: 0.3f, passed: 0, pushers: 1), _checkpoints);

            Assert.That(changed, Is.True);
            Assert.That(_status.Status, Is.Not.SameAs(shown), "the status already handed out keeps its values");
            Assert.That(((PayloadStatus)shown.Detail).Progress, Is.EqualTo(0.25f));
            Assert.That(_status.Payload.Progress, Is.EqualTo(0.3f));
            Assert.That(_status.Payload.IsContested, Is.False, "pushed forward, so not held back");
            Assert.That(_status.Payload.IsRollingBack, Is.False);
        }

        [Test]
        public void PushersThatDoNotMoveTheCartAreContested()
        {
            _status.Apply(Snapshot(tick: 10, progress: 0.5f, passed: 1, pushers: 2), _checkpoints);

            _status.Apply(Snapshot(tick: 11, progress: 0.5f, passed: 1, pushers: 2), _checkpoints);

            Assert.That(_status.Payload.IsContested, Is.True);
            Assert.That(_status.Payload.IsRollingBack, Is.False);

            _status.Apply(Snapshot(tick: 12, progress: 0.55f, passed: 1, pushers: 2), _checkpoints);

            Assert.That(_status.Payload.IsContested, Is.False, "the defenders left and the cart moves again");
        }

        [Test]
        public void ACartGoingBackWithoutPushersIsRollingBack()
        {
            _status.Apply(Snapshot(tick: 10, progress: 0.5f, passed: 1, pushers: 0), _checkpoints);

            _status.Apply(Snapshot(tick: 11, progress: 0.45f, passed: 1, pushers: 0), _checkpoints);

            Assert.That(_status.Payload.IsRollingBack, Is.True);
            Assert.That(_status.Payload.IsContested, Is.False);

            _status.Apply(Snapshot(tick: 12, progress: 0.45f, passed: 1, pushers: 0), _checkpoints);

            Assert.That(_status.Payload.IsRollingBack, Is.False, "stopped at the checkpoint floor");
        }

        [Test]
        public void TheSameOrAnOlderTickKeepsTheMotion()
        {
            _status.Apply(Snapshot(tick: 10, progress: 0.5f, passed: 1, pushers: 0), _checkpoints);
            _status.Apply(Snapshot(tick: 11, progress: 0.45f, passed: 1, pushers: 0), _checkpoints);

            _status.Apply(Snapshot(tick: 11, progress: 0.45f, passed: 1, pushers: 0), _checkpoints);

            Assert.That(_status.Payload.IsRollingBack, Is.True, "the same tick again is not a stop");

            _status.Apply(Snapshot(tick: 9, progress: 0.5f, passed: 1, pushers: 0), _checkpoints);

            Assert.That(_status.Payload.IsRollingBack, Is.True, "an older tick is not compared");
        }

        [Test]
        public void ANewMatchStartingFromZeroIsNotRollingBack()
        {
            _status.Apply(Snapshot(tick: 10, progress: 0.8f, passed: 2, pushers: 0, state: GameModeState.Ended), _checkpoints);

            _status.Apply(Snapshot(tick: 11, progress: 0f, passed: 0, pushers: 0), _checkpoints);

            Assert.That(_status.Payload.IsRollingBack, Is.False);
            Assert.That(_status.Payload.Progress, Is.Zero);
        }

        [Test]
        public void ADeliveredCartIsNotContested()
        {
            _status.Apply(Snapshot(tick: 10, progress: 1f, passed: _checkpoints, pushers: 1), _checkpoints);

            _status.Apply(Snapshot(tick: 11, progress: 1f, passed: _checkpoints, pushers: 1), _checkpoints);

            Assert.That(_status.Payload.IsContested, Is.False);
        }

        [Test]
        public void OutsideOfPlayNothingMoves()
        {
            _status.Apply(Snapshot(tick: 10, progress: 0.5f, passed: 1, pushers: 2), _checkpoints);
            _status.Apply(Snapshot(tick: 11, progress: 0.5f, passed: 1, pushers: 2), _checkpoints);
            Assert.That(_status.Payload.IsContested, Is.True);

            _status.Apply(Snapshot(tick: 12, progress: 0.5f, passed: 1, pushers: 2, state: GameModeState.Ended), _checkpoints);

            Assert.That(_status.Payload.IsContested, Is.False);
        }

        [Test]
        public void TheEndShowsTheServersWinner()
        {
            _status.Apply(
                Snapshot(tick: 10, progress: 1f, passed: _checkpoints, pushers: 1, state: GameModeState.Ended, winner: Team.Alpha), _checkpoints);

            Assert.That(_status.Status.IsEnded, Is.True);
            Assert.That(_status.Status.Winner, Is.EqualTo((Side?)Side.Alpha));
        }

        [Test]
        public void AWinnerIsNotShownBeforeTheEnd()
        {
            _status.Apply(Snapshot(tick: 10, progress: 0.5f, passed: 1, pushers: 1, winner: Team.Alpha), _checkpoints);

            Assert.That(_status.Status.Winner, Is.Null);
        }

        [Test]
        public void TheOwnersRespawnCountdownIsTheRespawnSeconds()
        {
            var snapshot = Snapshot(tick: 10, progress: 0.5f, passed: 1, pushers: 1);
            snapshot.HasOwner = true;
            snapshot.Owner.RespawnSeconds = 4;

            _status.Apply(snapshot, _checkpoints);

            Assert.That(_status.Status.RespawnSeconds, Is.EqualTo(4f));
        }

        [Test]
        public void MoreCheckpointsThanTheLocalRouteAreShownAsTheLast()
        {
            _status.Apply(Snapshot(tick: 10, progress: 0.9f, passed: 5, pushers: 1), _checkpoints);

            Assert.That(_status.CheckpointMismatch, Is.True);
            Assert.That(_status.Payload.CheckpointsReached, Is.EqualTo(_checkpoints));
            Assert.That(_status.Payload.CheckpointCount, Is.EqualTo(_checkpoints));

            _status.Apply(Snapshot(tick: 11, progress: 0.9f, passed: 2, pushers: 1), _checkpoints);

            Assert.That(_status.CheckpointMismatch, Is.False);
        }

        [Test]
        public void ARouteWithoutCheckpointsIsShown()
        {
            _status.Apply(Snapshot(tick: 10, progress: 0.5f, passed: 0, pushers: 1), 0);

            Assert.That(_status.Payload.CheckpointCount, Is.Zero);
            Assert.That(_status.Payload.CheckpointsReached, Is.Zero);
        }

        [Test]
        public void ASpectatorHasNoOwnSide()
        {
            _status.Apply(Snapshot(tick: 10, progress: 0.5f, passed: 0, pushers: 1, selfTeam: null), _checkpoints);

            Assert.That(_status.Status.OwnSide, Is.Null);
        }

        [Test]
        public void WithoutTheCartItemNothingIsShown()
        {
            var changed = _status.Apply(Snapshot(tick: 10, progress: 0.5f, passed: 0, pushers: 1, attackers: null), _checkpoints);

            Assert.That(changed, Is.False);
            Assert.That(_status.Status, Is.Null, "the attackers are not known without the cart");
        }

        [Test]
        public void AnotherObjectiveClearsTheStatus()
        {
            _status.Apply(Snapshot(tick: 10, progress: 0.5f, passed: 1, pushers: 1), _checkpoints);
            var snapshot = Snapshot(tick: 11, progress: 0.5f, passed: 1, pushers: 1);
            snapshot.Match.Objective = MatchObjective.Hardpoint;

            Assert.That(_status.Apply(snapshot, _checkpoints), Is.True);
            Assert.That(_status.Status, Is.Null);
            Assert.That(_status.Apply(snapshot, _checkpoints), Is.False, "already cleared");
        }

        [Test]
        public void ClearForgetsTheMotion()
        {
            _status.Apply(Snapshot(tick: 10, progress: 0.5f, passed: 1, pushers: 0), _checkpoints);

            Assert.That(_status.Clear(), Is.True);
            Assert.That(_status.Clear(), Is.False);

            _status.Apply(Snapshot(tick: 11, progress: 0.4f, passed: 1, pushers: 0), _checkpoints);

            Assert.That(_status.Payload.IsRollingBack, Is.False, "nothing to compare with after clearing");
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => _status.Apply(null, _checkpoints));
            Assert.Throws<ArgumentOutOfRangeException>(() => _status.Apply(Snapshot(tick: 10, progress: 0f, passed: 0, pushers: 0), -1));
        }

        private static SnapshotMessage Snapshot(
            int tick,
            float progress,
            int passed,
            int pushers,
            Team? attackers = Team.Alpha,
            int alpha = 0,
            int bravo = 0,
            int remaining = 300,
            GameModeState state = GameModeState.Playing,
            Team? winner = null,
            Team? selfTeam = Team.Bravo)
        {
            var snapshot = new SnapshotMessage { Tick = tick };
            ref var match = ref snapshot.Match;
            match.State = state;
            match.HasWinner = winner.HasValue;
            match.Winner = winner ?? Team.Alpha;
            match.RemainingSeconds = remaining;
            match.Objective = MatchObjective.Payload;
            match.AlphaPoints = alpha;
            match.BravoPoints = bravo;
            match.PayloadProgress = progress;
            match.PayloadCheckpointsPassed = passed;
            match.PayloadPushers = pushers;
            if (attackers.HasValue)
            {
                ref var cart = ref snapshot.ObjectiveItems.Add();
                cart.ItemId = 0;
                cart.Present = ObjectiveItemFields.All;
                cart.Kind = ObjectiveItemKind.Cart;
                cart.Team = attackers.Value;
                cart.State = ObjectiveItemState.Home;
            }

            if (selfTeam.HasValue)
            {
                ref var self = ref snapshot.Players.Add();
                self.PlayerId = _self;
                self.Team = selfTeam.Value;
                self.WeaponId = "";
            }

            ref var other = ref snapshot.Players.Add();
            other.PlayerId = _self + 1;
            other.Team = Team.Alpha;
            other.WeaponId = "";
            return snapshot;
        }
    }
}

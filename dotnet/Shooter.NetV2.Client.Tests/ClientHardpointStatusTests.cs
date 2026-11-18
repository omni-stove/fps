using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientHardpointStatusTests
    {
        private const int _self = 3;

        private ClientHardpointStatus _status;

        [SetUp]
        public void SetUp()
        {
            _status = new ClientHardpointStatus(_self);
        }

        [Test]
        public void AHardpointSnapshotIsShownInTheSameShapeAsOffline()
        {
            var changed = _status.Apply(Snapshot(points: 3, active: 1, secondsUntilMove: 17, holder: Team.Bravo, alpha: 12, bravo: 30, remaining: 240));

            Assert.That(changed, Is.True);
            var status = _status.Status;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.Detail, Is.InstanceOf<Hardpoint>(), "the HUD formatter registered for Hardpoint reads it");
            Assert.That(status.Detail, Is.SameAs(_status.Hardpoint));
            var hardpoint = (Hardpoint)status.Detail;
            Assert.That(hardpoint.PointCount, Is.EqualTo(3));
            Assert.That(hardpoint.ActivePoint, Is.EqualTo(1));
            Assert.That(hardpoint.NextPoint, Is.EqualTo(2));
            Assert.That(hardpoint.SecondsUntilMove, Is.EqualTo(17f));
            Assert.That(hardpoint.Status, Is.EqualTo(HardpointStatus.Held));
            Assert.That(hardpoint.Holder, Is.EqualTo(Side.Bravo));
            Assert.That(status.RemainingSeconds, Is.EqualTo(240f));
            Assert.That(status.SideScores, Has.Count.EqualTo(2));
            Assert.That(status.SideScores[0].Side, Is.EqualTo(Side.Alpha));
            Assert.That(status.SideScores[0].Score, Is.EqualTo(12d));
            Assert.That(status.SideScores[1].Side, Is.EqualTo(Side.Bravo));
            Assert.That(status.SideScores[1].Score, Is.EqualTo(30d));
            Assert.That(status.OwnSide, Is.EqualTo(Side.Bravo));
            Assert.That(status.RespawnSeconds, Is.Null);
            Assert.That(status.IsEnded, Is.False);
            Assert.That(status.Winner, Is.Null);
        }

        [Test]
        public void TheSameValuesKeepTheShownStatus()
        {
            _status.Apply(Snapshot(points: 3, active: 0, secondsUntilMove: 20, holder: Team.Alpha));
            var shown = _status.Status;

            var changed = _status.Apply(Snapshot(points: 3, active: 0, secondsUntilMove: 20, holder: Team.Alpha));

            Assert.That(changed, Is.False);
            Assert.That(_status.Status, Is.SameAs(shown));
        }

        [Test]
        public void ScoresChangeTheStatusButKeepTheHardpoint()
        {
            _status.Apply(Snapshot(points: 3, active: 0, secondsUntilMove: 20, holder: Team.Alpha, alpha: 4));
            var shown = _status.Status;
            var hardpoint = _status.Hardpoint;

            var changed = _status.Apply(Snapshot(points: 3, active: 0, secondsUntilMove: 20, holder: Team.Alpha, alpha: 5));

            Assert.That(changed, Is.True);
            Assert.That(_status.Status, Is.Not.SameAs(shown));
            Assert.That(_status.Status.SideScores[0].Score, Is.EqualTo(5d));
            Assert.That(_status.Hardpoint, Is.SameAs(hardpoint));
            Assert.That(hardpoint.SecondsUntilMove, Is.EqualTo(20f));
        }

        [Test]
        public void TheCountdownAndTheMoveAreFollowed()
        {
            _status.Apply(Snapshot(points: 3, active: 0, secondsUntilMove: 2, holder: Team.Alpha));
            var hardpoint = _status.Hardpoint;

            _status.Apply(Snapshot(points: 3, active: 0, secondsUntilMove: 1, holder: Team.Alpha));
            Assert.That(hardpoint.ActivePoint, Is.Zero);
            Assert.That(hardpoint.SecondsUntilMove, Is.EqualTo(1f));

            _status.Apply(Snapshot(points: 3, active: 1, secondsUntilMove: 30, holder: null));
            Assert.That(_status.Hardpoint, Is.SameAs(hardpoint));
            Assert.That(hardpoint.ActivePoint, Is.EqualTo(1));
            Assert.That(hardpoint.SecondsUntilMove, Is.EqualTo(30f));
            Assert.That(hardpoint.Status, Is.EqualTo(HardpointStatus.Empty));
            Assert.That(hardpoint.Holder, Is.Null);
        }

        [Test]
        public void TheLastPointIsFollowedByTheFirst()
        {
            _status.Apply(Snapshot(points: 3, active: 2, secondsUntilMove: 5, holder: null));

            Assert.That(_status.Hardpoint.ActivePoint, Is.EqualTo(2));
            Assert.That(_status.Hardpoint.NextPoint, Is.Zero);
        }

        [Test]
        public void AContestedPointHasNoHolder()
        {
            _status.Apply(Snapshot(points: 2, active: 1, secondsUntilMove: 9, holder: null, contested: true));

            Assert.That(_status.Hardpoint.Status, Is.EqualTo(HardpointStatus.Contested));
            Assert.That(_status.Hardpoint.Holder, Is.Null);
        }

        [Test]
        public void TheEndAndTheWinnerAreShown()
        {
            _status.Apply(Snapshot(points: 3, active: 1, secondsUntilMove: 4, holder: Team.Alpha, alpha: 100, bravo: 60, state: GameModeState.Ended, winner: Team.Alpha));

            Assert.That(_status.Status.IsEnded, Is.True);
            Assert.That(_status.Status.Winner, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void AWinnerIsNotShownBeforeTheEnd()
        {
            _status.Apply(Snapshot(points: 3, active: 1, secondsUntilMove: 4, holder: null, winner: Team.Alpha));

            Assert.That(_status.Status.IsEnded, Is.False);
            Assert.That(_status.Status.Winner, Is.Null);
        }

        [Test]
        public void WithoutTheLocalPlayerThereIsNoOwnSide()
        {
            _status.Apply(Snapshot(points: 3, active: 0, secondsUntilMove: 4, holder: null, selfTeam: null));

            Assert.That(_status.Status.OwnSide, Is.Null);
        }

        // 0 秒の拠点は Hardpoint では「もう移った」になるので写せない。サーバーは送らないが、来たら 1 秒として出す
        [TestCase(0, 1f)]
        [TestCase(HudFormat.MaxDisplayedSeconds, HudFormat.MaxDisplayedSeconds)]
        [TestCase(int.MaxValue, HudFormat.MaxDisplayedSeconds)]
        public void TheSecondsUntilTheMoveAreKeptToWhatCanBeShown(int sent, float shown)
        {
            _status.Apply(Snapshot(points: 16, active: 15, secondsUntilMove: sent, holder: null));

            Assert.That(_status.Hardpoint.ActivePoint, Is.EqualTo(15));
            Assert.That(_status.Hardpoint.SecondsUntilMove, Is.EqualTo(shown));
        }

        [Test]
        public void ADifferentNumberOfPointsBuildsANewHardpoint()
        {
            _status.Apply(Snapshot(points: 3, active: 0, secondsUntilMove: 4, holder: null));
            var first = _status.Hardpoint;

            _status.Apply(Snapshot(points: 4, active: 3, secondsUntilMove: 4, holder: Team.Alpha));

            Assert.That(_status.Hardpoint, Is.Not.SameAs(first));
            Assert.That(_status.Hardpoint.PointCount, Is.EqualTo(4));
            Assert.That(_status.Hardpoint.ActivePoint, Is.EqualTo(3));
            Assert.That(_status.Hardpoint.Holder, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void AnotherObjectiveClearsTheStatus()
        {
            _status.Apply(Snapshot(points: 3, active: 0, secondsUntilMove: 4, holder: null));
            var other = Snapshot(points: 3, active: 0, secondsUntilMove: 4, holder: null);
            other.Match.Objective = MatchObjective.Domination;

            Assert.That(_status.Apply(other), Is.True);
            Assert.That(_status.Status, Is.Null);
            Assert.That(_status.Hardpoint, Is.Null);
            Assert.That(_status.Apply(other), Is.False);
        }

        [Test]
        public void ClearForgetsTheStatusAndTheNextSnapshotShowsItAgain()
        {
            _status.Apply(Snapshot(points: 3, active: 0, secondsUntilMove: 4, holder: Team.Bravo));

            Assert.That(_status.Clear(), Is.True);
            Assert.That(_status.Status, Is.Null);
            Assert.That(_status.Clear(), Is.False);

            Assert.That(_status.Apply(Snapshot(points: 3, active: 0, secondsUntilMove: 4, holder: Team.Bravo)), Is.True);
            Assert.That(_status.Hardpoint.Holder, Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void ASnapshotWithoutAUsableActivePointShowsNothing()
        {
            var noActive = Snapshot(points: 3, active: 0, secondsUntilMove: 4, holder: null);
            noActive.Match.HasActivePoint = false;
            Assert.That(_status.Apply(noActive), Is.False);

            var outside = Snapshot(points: 3, active: 0, secondsUntilMove: 4, holder: null);
            outside.Match.ActivePointIndex = 3;
            Assert.That(_status.Apply(outside), Is.False);

            var onePoint = Snapshot(points: 1, active: 0, secondsUntilMove: 4, holder: null);
            Assert.That(_status.Apply(onePoint), Is.False);
            Assert.That(_status.Status, Is.Null);
        }

        [Test]
        public void ASnapshotIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => _status.Apply(null));
        }

        // 届いた欄は、サーバーの HardpointRules が書くのと同じ形 (有効な拠点だけに持ち主か取り合い) にする
        private static SnapshotMessage Snapshot(
            int points,
            int active,
            int secondsUntilMove,
            Team? holder,
            bool contested = false,
            int alpha = 0,
            int bravo = 0,
            int remaining = 300,
            GameModeState state = GameModeState.Playing,
            Team? winner = null,
            Team? selfTeam = Team.Bravo)
        {
            var snapshot = new SnapshotMessage { Tick = 10 };
            ref var match = ref snapshot.Match;
            match.State = state;
            match.HasWinner = winner.HasValue;
            match.Winner = winner ?? Team.Alpha;
            match.RemainingSeconds = remaining;
            match.Objective = MatchObjective.Hardpoint;
            match.AlphaPoints = alpha;
            match.BravoPoints = bravo;
            match.HasActivePoint = true;
            match.ActivePointIndex = active;
            match.ObjectiveRemainingSeconds = secondsUntilMove;
            for (var i = 0; i < points; i++)
            {
                ref var point = ref snapshot.ControlPoints.Add();
                var held = i == active && holder.HasValue;
                point.Index = i;
                point.Present = ControlPointFields.All;
                point.HasOwner = held;
                point.Owner = holder ?? Team.Alpha;
                point.ControlPercent = (sbyte)(!held ? 0 : holder == Team.Alpha ? 100 : -100);
                point.Contested = i == active && contested;
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

using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    // 道筋は X 軸に沿った 30 m の直線で、10 m と 20 m の点が中継点。台車は 1 m/秒、押す人数の倍率は 1・1.5・2。
    public sealed class PayloadTests
    {
        private const float _tolerance = 1e-5f;

        private const int _attacker1 = 1;
        private const int _attacker2 = 2;
        private const int _attacker3 = 3;
        private const int _attacker4 = 4;
        private const int _defender = 10;

        private const float _timeLimit = 60f;
        private const float _bonus = 30f;
        private const float _rollbackDelay = 5f;
        private const float _rollbackSpeed = 0.5f;
        private const float _respawnSeconds = 3f;

        private static readonly Vector3[] _line =
        {
            Vector3.Zero, new Vector3(10f, 0f, 0f), new Vector3(20f, 0f, 0f), new Vector3(30f, 0f, 0f),
        };

        private static readonly int[] _checkpoints = { 1, 2 };
        private static readonly float[] _multipliers = { 1f, 1.5f, 2f };
        private static readonly int[] _expectedFirstCheckpoint = { 0 };
        private static readonly int[] _expectedBothCheckpoints = { 0, 1 };

        [Test]
        public void TheConstructorRejectsMissingPartsAndANonTeamAttacker()
        {
            var route = new PayloadRoute(_line, _checkpoints);

            Assert.Throws<ArgumentNullException>(() => new Payload(null, route, Side.Alpha, _respawnSeconds));
            Assert.Throws<ArgumentNullException>(() => new Payload(PayloadSpec.Default, null, Side.Alpha, _respawnSeconds));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Payload(PayloadSpec.Default, route, new Side(2), _respawnSeconds));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Payload(PayloadSpec.Default, route, Side.Alpha, -1f));
        }

        [Test]
        public void TheDefendersAreTheOtherTeamAndTheClockIsTheSpecs()
        {
            var alpha = Create();
            var bravo = new Payload(Spec(), new PayloadRoute(_line, _checkpoints), Side.Bravo, _respawnSeconds);

            Assert.That(alpha.Attackers, Is.EqualTo(Side.Alpha));
            Assert.That(alpha.Defenders, Is.EqualTo(Side.Bravo));
            Assert.That(bravo.Attackers, Is.EqualTo(Side.Bravo));
            Assert.That(bravo.Defenders, Is.EqualTo(Side.Alpha));
            Assert.That(alpha.Session.TimeLimitSeconds, Is.EqualTo(_timeLimit));
            Assert.That(alpha.Distance, Is.Zero);
            Assert.That(alpha.Progress, Is.Zero);
            Assert.That(alpha.CheckpointsReached, Is.Zero);
        }

        [Test]
        public void AnAttackerAloneNearTheCartPushesItAtTheCartSpeed()
        {
            var payload = Create();
            payload.SetNearCart(_attacker1, true);

            payload.Session.Tick(2f);

            Assert.That(payload.Distance, Is.EqualTo(2d).Within(_tolerance));
            Assert.That(payload.Progress, Is.EqualTo(2f / 30f).Within(_tolerance));
            Assert.That(payload.Pushers, Is.EqualTo(1));
            Assert.That(payload.IsContested, Is.False);
            Assert.That(payload.IsRollingBack, Is.False);
            Assert.That(payload.CurrentSpeed, Is.EqualTo(1f));
        }

        [Test]
        public void MorePushersPushFasterUpToTheLastMultiplier()
        {
            Assert.That(DistanceAfterTwoSecondsWith(_attacker1, _attacker2), Is.EqualTo(3d).Within(_tolerance));
            Assert.That(DistanceAfterTwoSecondsWith(_attacker1, _attacker2, _attacker3), Is.EqualTo(4d).Within(_tolerance));
            Assert.That(
                DistanceAfterTwoSecondsWith(_attacker1, _attacker2, _attacker3, _attacker4),
                Is.EqualTo(4d).Within(_tolerance),
                "pushers past the list use its last multiplier");
        }

        [Test]
        public void FewerThanTheMinimumPushersHoldTheCartWithoutRollingItBack()
        {
            var payload = Create(Spec(minPushers: 2));
            payload.SetNearCart(_attacker1, true);
            payload.SetNearCart(_attacker2, true);
            payload.Session.Tick(2f);
            Assert.That(payload.Distance, Is.EqualTo(2d).Within(_tolerance), "two pushers move at the first multiplier");

            payload.SetNearCart(_attacker2, false);
            payload.Session.Tick(20f);

            Assert.That(payload.Distance, Is.EqualTo(2d).Within(_tolerance), "one attacker cannot push but keeps the cart from rolling back");
            Assert.That(payload.CurrentSpeed, Is.Zero);
            Assert.That(payload.IsRollingBack, Is.False);
        }

        [Test]
        public void ADefenderNearTheCartStopsItAndKeepsItFromRollingBack()
        {
            var payload = Create();
            payload.SetNearCart(_attacker1, true);
            payload.Session.Tick(4f);

            payload.SetNearCart(_defender, true);
            payload.Session.Tick(20f);

            Assert.That(payload.Distance, Is.EqualTo(4d).Within(_tolerance));
            Assert.That(payload.IsContested, Is.True);
            Assert.That(payload.IsRollingBack, Is.False, "an attacker is still at the cart");
            Assert.That(payload.CurrentSpeed, Is.Zero);
        }

        [Test]
        public void ADownedDefenderDoesNotContest()
        {
            var payload = Create();
            payload.SetNearCart(_attacker1, true);
            payload.SetNearCart(_defender, true);
            payload.Session.ReportKill(_attacker1, _defender);

            payload.Session.Tick(1f);

            Assert.That(payload.IsContested, Is.False);
            Assert.That(payload.Distance, Is.EqualTo(1d).Within(_tolerance));
        }

        [Test]
        public void TheCartRollsBackAfterTheDelayWhenNoAttackerIsNear()
        {
            var payload = Create();
            payload.SetNearCart(_attacker1, true);
            payload.Session.Tick(4f);
            payload.SetNearCart(_attacker1, false);

            payload.Session.Tick(_rollbackDelay);
            Assert.That(payload.Distance, Is.EqualTo(4d).Within(_tolerance), "the cart waits the whole delay");
            Assert.That(payload.IsRollingBack, Is.False);

            payload.Session.Tick(2f);
            Assert.That(payload.Distance, Is.EqualTo(4d - (_rollbackSpeed * 2d)).Within(_tolerance));
            Assert.That(payload.IsRollingBack, Is.True);

            payload.Session.Tick(100f);
            Assert.That(payload.Distance, Is.Zero, "the cart stops at the start");
        }

        [Test]
        public void OneLongIdleTickRollsBackOnlyForTheTimePastTheDelay()
        {
            var payload = Create();
            payload.SetNearCart(_attacker1, true);
            payload.Session.Tick(4f);
            payload.SetNearCart(_attacker1, false);

            payload.Session.Tick(_rollbackDelay + 2f);

            Assert.That(payload.Distance, Is.EqualTo(3d).Within(_tolerance));
        }

        [Test]
        public void AnAttackerComingBackRestartsTheDelay()
        {
            var payload = Create(Spec(rollbackDelay: 5f));
            payload.SetNearCart(_attacker1, true);
            payload.Session.Tick(4f);
            payload.SetNearCart(_attacker1, false);
            payload.Session.Tick(4f);

            payload.SetNearCart(_attacker1, true);
            payload.Session.Tick(1f);
            payload.SetNearCart(_attacker1, false);
            payload.Session.Tick(4f);

            Assert.That(payload.Distance, Is.EqualTo(5d).Within(_tolerance), "the four idle seconds after the push are inside a fresh delay");
            Assert.That(payload.IsRollingBack, Is.False);
        }

        [Test]
        public void NoRollbackSpeedKeepsTheCartWhereItWasLeft()
        {
            var payload = Create(Spec(rollbackSpeed: 0f));
            payload.SetNearCart(_attacker1, true);
            payload.Session.Tick(4f);
            payload.SetNearCart(_attacker1, false);

            payload.Session.Tick(30f);

            Assert.That(payload.Distance, Is.EqualTo(4d).Within(_tolerance));
            Assert.That(payload.IsRollingBack, Is.False);
        }

        [Test]
        public void TheCartDoesNotRollBackPastTheLastCheckpoint()
        {
            var payload = Create();
            payload.SetNearCart(_attacker1, true);
            payload.Session.Tick(12f);
            payload.SetNearCart(_attacker1, false);

            payload.Session.Tick(40f);

            Assert.That(payload.CheckpointsReached, Is.EqualTo(1));
            Assert.That(payload.RollbackFloor, Is.EqualTo(10d).Within(_tolerance));
            Assert.That(payload.Distance, Is.EqualTo(10d).Within(_tolerance));
        }

        [Test]
        public void PassingACheckpointExtendsTheTimeOnce()
        {
            var payload = Create();
            var reached = Record(payload);
            payload.SetNearCart(_attacker1, true);

            payload.Session.Tick(10f);
            Assert.That(reached, Is.EqualTo(_expectedFirstCheckpoint));
            Assert.That(payload.CheckpointsReached, Is.EqualTo(1));
            Assert.That(payload.Session.RemainingSeconds, Is.EqualTo(_timeLimit - 10f + _bonus).Within(_tolerance));

            payload.Session.Tick(5f);
            Assert.That(reached, Is.EqualTo(_expectedFirstCheckpoint), "moving on past the checkpoint does not extend the time again");
            Assert.That(payload.Session.RemainingSeconds, Is.EqualTo(_timeLimit - 15f + _bonus).Within(_tolerance));
            Assert.That(payload.Session.TimeLimitSeconds, Is.EqualTo(_timeLimit), "only the remaining time is extended");
        }

        [Test]
        public void RollingBackOverACheckpointAndPushingAgainDoesNotExtendTheTimeTwice()
        {
            var payload = Create();
            var reached = Record(payload);
            payload.SetNearCart(_attacker1, true);
            payload.Session.Tick(12f);
            payload.SetNearCart(_attacker1, false);
            payload.Session.Tick(_rollbackDelay + 10f);
            payload.SetNearCart(_attacker1, true);
            payload.Session.Tick(1f);

            Assert.That(reached, Is.EqualTo(_expectedFirstCheckpoint));
            Assert.That(payload.Distance, Is.EqualTo(11d).Within(_tolerance));
        }

        [Test]
        public void OneLongTickPassesEveryCheckpointOnTheWay()
        {
            var payload = Create();
            var reached = Record(payload);
            payload.SetNearCart(_attacker1, true);

            payload.Session.Tick(25f);

            Assert.That(reached, Is.EqualTo(_expectedBothCheckpoints));
            Assert.That(payload.Distance, Is.EqualTo(25d).Within(_tolerance));
            Assert.That(payload.Session.RemainingSeconds, Is.EqualTo(_timeLimit - 25f + (2f * _bonus)).Within(_tolerance));
        }

        [Test]
        public void ReachingTheEndWinsForTheAttackers()
        {
            var payload = Create();
            payload.SetNearCart(_attacker1, true);

            payload.Session.Tick(45f);

            Assert.That(payload.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(payload.Session.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(payload.IsDelivered, Is.True);
            Assert.That(payload.Distance, Is.EqualTo(30d), "the cart stops at the end");
            Assert.That(payload.Progress, Is.EqualTo(1f));
            Assert.That(payload.CheckpointsReached, Is.EqualTo(2));
        }

        [Test]
        public void TimeUpWinsForTheDefenders()
        {
            var payload = Create();
            payload.SetNearCart(_attacker1, true);
            payload.Session.Tick(5f);
            payload.SetNearCart(_defender, true);

            payload.Session.Tick(_timeLimit);

            Assert.That(payload.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(payload.Session.Winner, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(payload.IsDelivered, Is.False);
        }

        [Test]
        public void TheExtendedTimeAlsoRunsOutForTheDefenders()
        {
            var payload = Create();
            payload.SetNearCart(_attacker1, true);
            payload.Session.Tick(10f);
            payload.SetNearCart(_attacker1, false);

            payload.Session.Tick(_timeLimit - 10f);
            Assert.That(payload.Session.State, Is.EqualTo(GameModeState.Playing), "the checkpoint bonus is still left");

            payload.Session.Tick(_bonus);
            Assert.That(payload.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(payload.Session.Winner, Is.EqualTo((Side?)Side.Bravo));
        }

        [Test]
        public void BravoCanAttack()
        {
            var payload = new Payload(Spec(), new PayloadRoute(_line, _checkpoints), Side.Bravo, _respawnSeconds);
            payload.Session.AddPlayer(_attacker1, Side.Alpha);
            payload.Session.AddPlayer(_defender, Side.Bravo);
            payload.Session.Start();
            payload.SetNearCart(_defender, true);

            payload.Session.Tick(30f);

            Assert.That(payload.Session.Winner, Is.EqualTo((Side?)Side.Bravo));
        }

        [Test]
        public void NothingMovesBeforeTheStart()
        {
            var payload = new Payload(Spec(), new PayloadRoute(_line, _checkpoints), Side.Alpha, _respawnSeconds);
            payload.Session.AddPlayer(_attacker1, Side.Alpha);
            payload.SetNearCart(_attacker1, true);

            payload.Session.Tick(5f);

            Assert.That(payload.Distance, Is.Zero);
        }

        [Test]
        public void ResetPutsTheCartBackAndForgetsWhoWasNear()
        {
            var payload = Create();
            payload.SetNearCart(_attacker1, true);
            payload.Session.Tick(12f);

            payload.Reset();

            Assert.That(payload.Distance, Is.Zero);
            Assert.That(payload.CheckpointsReached, Is.Zero);
            Assert.That(payload.IsNearCart(_attacker1), Is.False);
            Assert.That(payload.Session.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(payload.Session.RemainingSeconds, Is.EqualTo(_timeLimit), "the checkpoint bonus is gone");
        }

        [Test]
        public void PresenceIsOnlyForPlayersInTheMatch()
        {
            var payload = Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => payload.SetNearCart(99, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => payload.IsNearCart(99));

            payload.SetNearCart(_attacker1, true);
            payload.Session.RemovePlayer(_attacker1);
            Assert.That(payload.Pushers, Is.Zero, "a player who left is not counted");
        }

        [Test]
        public void ExtendTimeAddsOnlyWhilePlaying()
        {
            var payload = new Payload(Spec(), new PayloadRoute(_line, _checkpoints), Side.Alpha, _respawnSeconds);
            var session = payload.Session;

            session.ExtendTime(10f);
            Assert.That(session.RemainingSeconds, Is.EqualTo(_timeLimit), "a match that has not started keeps its clock");

            session.Start();
            session.ExtendTime(10f);
            Assert.That(session.RemainingSeconds, Is.EqualTo(_timeLimit + 10f));

            Assert.Throws<ArgumentOutOfRangeException>(() => session.ExtendTime(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ExtendTime(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ExtendTime(float.PositiveInfinity));
        }

        [Test]
        public void TheStatusCopiesTheCartAtThatMoment()
        {
            var payload = Create();
            payload.SetNearCart(_attacker1, true);
            payload.SetNearCart(_attacker2, true);
            payload.Session.Tick(10f);
            payload.SetNearCart(_defender, true);

            var status = PayloadStatus.Of(payload);
            payload.Session.Tick(1f);
            payload.SetNearCart(_defender, false);
            payload.Session.Tick(1f);

            Assert.That(status.Attackers, Is.EqualTo(Side.Alpha));
            Assert.That(status.Progress, Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(status.CheckpointsReached, Is.EqualTo(1));
            Assert.That(status.CheckpointCount, Is.EqualTo(2));
            Assert.That(status.Pushers, Is.EqualTo(2));
            Assert.That(status.IsContested, Is.True);
            Assert.That(status.IsRollingBack, Is.False);
            Assert.Throws<ArgumentNullException>(() => PayloadStatus.Of(null));
        }

        [Test]
        public void TheStatusRejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PayloadStatus(Side.Alpha, -0.1f, 0, 0, 0, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PayloadStatus(Side.Alpha, 1.1f, 0, 0, 0, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PayloadStatus(Side.Alpha, float.NaN, 0, 0, 0, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PayloadStatus(Side.Alpha, 0f, 3, 2, 0, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PayloadStatus(Side.Alpha, 0f, 0, -1, 0, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PayloadStatus(Side.Alpha, 0f, 0, 0, -1, false, false));
        }

        [Test]
        public void TheLogLinesCarryTheCartState()
        {
            var payload = Create();
            payload.SetNearCart(_attacker1, true);

            Assert.That(PayloadLog.Started(payload), Is.EqualTo("[Mode] payload started attackers=0 length=30.0m checkpoints=2 limit=60.0s"));

            payload.Session.Tick(15f);
            Assert.That(
                PayloadLog.Checkpoint(payload, 0),
                Is.EqualTo("[Mode] payload checkpoint=1/2 progress=0.33 bonus=30.0s remaining=75.0s"));
            Assert.That(PayloadLog.Motion(payload), Is.EqualTo("[Mode] payload cart=pushing pushers=1 defenders=0 progress=0.50"));

            payload.SetNearCart(_defender, true);
            Assert.That(PayloadLog.MotionName(payload), Is.EqualTo(PayloadLog.Contested));

            payload.SetNearCart(_attacker1, false);
            payload.SetNearCart(_defender, false);
            payload.Session.Tick(_rollbackDelay + 1f);
            Assert.That(PayloadLog.MotionName(payload), Is.EqualTo(PayloadLog.RollingBack));

            payload.Session.Tick(20f);
            Assert.That(payload.Distance, Is.EqualTo(10d).Within(_tolerance));
            payload.Session.Tick(1f);
            Assert.That(PayloadLog.MotionName(payload), Is.EqualTo(PayloadLog.Idle), "the cart rests at the checkpoint");

            payload.SetNearCart(_attacker1, true);
            payload.Session.Tick(30f);
            Assert.That(PayloadLog.Ended(payload, payload.Session.Winner), Is.EqualTo("[Mode] payload ended winner=0 delivered=true progress=1.00 checkpoints=2/2"));
            Assert.That(PayloadLog.Ended(payload, null), Does.Contain("winner=draw"));
        }

        private static float DistanceAfterTwoSecondsWith(params int[] pushers)
        {
            var payload = Create();
            foreach (var id in pushers)
            {
                payload.SetNearCart(id, true);
            }

            payload.Session.Tick(2f);
            return (float)payload.Distance;
        }

        private static List<int> Record(Payload payload)
        {
            var reached = new List<int>();
            payload.CheckpointReached += reached.Add;
            return reached;
        }

        private static PayloadSpec Spec(int minPushers = 1, float rollbackDelay = _rollbackDelay, float rollbackSpeed = _rollbackSpeed)
        {
            return new PayloadSpec(1f, minPushers, _multipliers, rollbackDelay, rollbackSpeed, _bonus, _timeLimit);
        }

        private static Payload Create(PayloadSpec spec = null)
        {
            var payload = new Payload(spec ?? Spec(), new PayloadRoute(_line, _checkpoints), Side.Alpha, _respawnSeconds);
            payload.Session.AddPlayer(_attacker1, Side.Alpha);
            payload.Session.AddPlayer(_attacker2, Side.Alpha);
            payload.Session.AddPlayer(_attacker3, Side.Alpha);
            payload.Session.AddPlayer(_attacker4, Side.Alpha);
            payload.Session.AddPlayer(_defender, Side.Bravo);
            payload.Session.Start();
            return payload;
        }
    }
}

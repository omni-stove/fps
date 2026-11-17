using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientSpectateTests
    {
        private const int _self = 1;
        private const int _tickRate = 60;
        private const int _health = 100;

        private static readonly StanceGeometry _stance = new StanceGeometry(1.8f, 0.9f, 1.6f, 1.2f, 0.4f);

        // 送る知らせは使い回しなので、送ったときの向きを写しておく
        private readonly List<SpectateStep> _sent = new List<SpectateStep>();
        private bool _connected;
        private ClientSpectate _spectate;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _connected = true;
            _spectate = new ClientSpectate(_self, _tickRate, _stance, message =>
            {
                if (!_connected)
                {
                    return false;
                }

                Assert.That(message, Is.InstanceOf<SpectateRequestMessage>());
                _sent.Add(((SpectateRequestMessage)message).Step);
                return true;
            });
        }

        [Test]
        public void WhileAliveNothingIsWatched()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, _health), Row(4, Team.Alpha, _health)));

            Assert.That(_spectate.Update(0f, null), Is.False);
            Assert.That(_spectate.Status, Is.Null);
            Assert.That(_spectate.TryGetView(out _, out _), Is.False);
        }

        [Test]
        public void AfterDyingTheLowestIdLivingAllyIsWatchedAndEnemiesAndTheDeadAreSkipped()
        {
            _spectate.Apply(Snapshot(
                10,
                DeathCamera.Ally,
                Row(_self, Team.Alpha, 0),
                Row(2, Team.Bravo, _health),
                Row(3, Team.Alpha, 0),
                Row(7, Team.Alpha, _health),
                Row(5, Team.Alpha, _health)));

            Assert.That(_spectate.Update(0f, null), Is.True);

            Assert.That(_spectate.Status, Is.EqualTo(new SpectateStatus(5, 2, SpectateReason.Dead)));
            Assert.That(_sent, Is.Empty, "choosing the first ally does not ask the server");
        }

        [Test]
        public void ADownedAllyCanBeWatched()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(4, Team.Alpha, 0, downed: true)));

            _spectate.Update(0f, null);

            Assert.That(_spectate.TargetId, Is.EqualTo(4));
        }

        [Test]
        public void BeingDownedIsNotDeadSoItDoesNotStartWatching()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, 0, downed: true), Row(4, Team.Alpha, _health)));

            _spectate.Update(0f, null);

            Assert.That(_spectate.Status, Is.Null);
        }

        [TestCase(DeathCamera.None)]
        [TestCase(DeathCamera.Killer)]
        [TestCase(DeathCamera.Free)]
        [TestCase(DeathCamera.KillCam)]
        [TestCase(DeathCamera.Fallen)]
        public void DyingWatchesAlliesOnlyWhenTheRulesDeathCameraIsAlly(DeathCamera rule)
        {
            _spectate.Apply(Snapshot(10, rule, Row(_self, Team.Alpha, 0), Row(4, Team.Alpha, _health)));

            _spectate.Update(0f, null);

            Assert.That(_spectate.Status, Is.Null);
        }

        [Test]
        public void BetweenRoundsAlliesAreWatchedEvenWhileAlive()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.None, Row(_self, Team.Bravo, _health), Row(4, Team.Bravo, _health), Row(6, Team.Alpha, _health)));

            _spectate.Update(0f, RoundPhase.Preparing);

            Assert.That(_spectate.Status, Is.EqualTo(new SpectateStatus(4, 1, SpectateReason.BetweenRounds)));
        }

        [TestCase(RoundPhase.Waiting)]
        [TestCase(RoundPhase.Live)]
        [TestCase(RoundPhase.Ended)]
        public void OtherRoundPhasesDoNotStartWatchingWhileAlive(RoundPhase phase)
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, _health), Row(4, Team.Alpha, _health)));

            _spectate.Update(0f, phase);

            Assert.That(_spectate.Status, Is.Null);
        }

        [Test]
        public void TheSnapshotsRoundPhaseStartsAndEndsWatchingBetweenRounds()
        {
            var preparing = Snapshot(10, DeathCamera.None, Row(_self, Team.Alpha, _health), Row(4, Team.Alpha, _health));
            preparing.Match.HasRound = true;
            preparing.Match.Round = RoundPhase.Preparing;
            _spectate.Apply(preparing);

            Assert.That(_spectate.Round, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(_spectate.Update(0f), Is.True);
            Assert.That(_spectate.Status, Is.EqualTo(new SpectateStatus(4, 1, SpectateReason.BetweenRounds)));

            var live = Snapshot(11, DeathCamera.None, Row(_self, Team.Alpha, _health), Row(4, Team.Alpha, _health));
            live.Match.HasRound = true;
            live.Match.Round = RoundPhase.Live;
            _spectate.Apply(live);

            Assert.That(_spectate.Round, Is.EqualTo(RoundPhase.Live));
            Assert.That(_spectate.Update(0f), Is.True);
            Assert.That(_spectate.Status, Is.Null);
        }

        [Test]
        public void ASnapshotWithoutRoundsHasNoRoundPhaseEvenIfThePhaseByteSaysPreparing()
        {
            var snapshot = Snapshot(10, DeathCamera.None, Row(_self, Team.Alpha, _health), Row(4, Team.Alpha, _health));
            snapshot.Match.HasRound = false;
            snapshot.Match.Round = RoundPhase.Preparing;
            _spectate.Apply(snapshot);

            _spectate.Update(0f);

            Assert.That(_spectate.Round, Is.Null);
            Assert.That(_spectate.Status, Is.Null);
        }

        [Test]
        public void ResetForgetsTheRoundPhase()
        {
            var snapshot = Snapshot(10, DeathCamera.None, Row(_self, Team.Alpha, _health), Row(4, Team.Alpha, _health));
            snapshot.Match.HasRound = true;
            snapshot.Match.Round = RoundPhase.Preparing;
            _spectate.Apply(snapshot);

            _spectate.Reset();

            Assert.That(_spectate.Round, Is.Null);
        }

        [Test]
        public void NextAndPreviousCycleThroughAlliesAndTellTheServer()
        {
            _spectate.Apply(Snapshot(
                10, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(2, Team.Alpha, _health), Row(4, Team.Alpha, _health), Row(6, Team.Alpha, _health)));
            _spectate.Update(0f, null);
            Assert.That(_spectate.TargetId, Is.EqualTo(2));

            Assert.That(_spectate.Step(SpectateStep.Next), Is.True);
            Assert.That(_spectate.TargetId, Is.EqualTo(4));
            Assert.That(_spectate.Step(SpectateStep.Next), Is.True);
            Assert.That(_spectate.TargetId, Is.EqualTo(6));
            Assert.That(_spectate.Step(SpectateStep.Next), Is.True);
            Assert.That(_spectate.TargetId, Is.EqualTo(2), "Next wraps around to the first ally");
            Assert.That(_spectate.Step(SpectateStep.Previous), Is.True);
            Assert.That(_spectate.TargetId, Is.EqualTo(6), "Previous wraps around to the last ally");

            Assert.That(_sent, Is.EqualTo(new[] { SpectateStep.Next, SpectateStep.Next, SpectateStep.Next, SpectateStep.Previous }));
            Assert.That(_spectate.Status.Value.CandidateCount, Is.EqualTo(3));
        }

        [Test]
        public void SwitchingStaysOnTheAllyAfterUpdates()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(2, Team.Alpha, _health), Row(4, Team.Alpha, _health)));
            _spectate.Update(0f, null);
            _spectate.Step(SpectateStep.Next);

            _spectate.Apply(Snapshot(11, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(2, Team.Alpha, _health), Row(4, Team.Alpha, _health)));

            Assert.That(_spectate.Update(1f / _tickRate, null), Is.False);
            Assert.That(_spectate.TargetId, Is.EqualTo(4));
        }

        [Test]
        public void WhenTheWatchedAllyDiesTheNextAllyIsWatchedWithoutAskingTheServer()
        {
            _spectate.Apply(Snapshot(
                10, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(2, Team.Alpha, _health), Row(4, Team.Alpha, _health), Row(6, Team.Alpha, _health)));
            _spectate.Update(0f, null);
            _spectate.Step(SpectateStep.Next);
            _sent.Clear();

            _spectate.Apply(Snapshot(
                11, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(2, Team.Alpha, _health), Row(4, Team.Alpha, 0), Row(6, Team.Alpha, _health)));

            Assert.That(_spectate.Update(1f / _tickRate, null), Is.True);
            Assert.That(_spectate.Status, Is.EqualTo(new SpectateStatus(6, 2, SpectateReason.Dead)));
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void WhenTheWatchedAllyLeavesTheNextAllyIsWatched()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(2, Team.Alpha, _health), Row(4, Team.Alpha, _health)));
            _spectate.Update(0f, null);

            _spectate.Apply(Snapshot(11, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(4, Team.Alpha, _health)));
            _spectate.Update(1f / _tickRate, null);

            Assert.That(_spectate.Status, Is.EqualTo(new SpectateStatus(4, 1, SpectateReason.Dead)));
        }

        [Test]
        public void WhenTheLastAllyDiesThereIsNoTargetAndNoView()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(4, Team.Alpha, _health)));
            _spectate.Update(0f, null);
            Assert.That(_spectate.TryGetView(out _, out _), Is.True);

            _spectate.Apply(Snapshot(11, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(4, Team.Alpha, 0)));
            _spectate.Update(1f / _tickRate, null);

            Assert.That(_spectate.Status, Is.EqualTo(new SpectateStatus(null, 0, SpectateReason.Dead)));
            Assert.That(_spectate.TryGetView(out _, out _), Is.False, "without an ally the death camera stays as it is");
        }

        [Test]
        public void WithoutCandidatesNextAndPreviousChangeNothingAndSendNothing()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(2, Team.Bravo, _health)));
            _spectate.Update(0f, null);

            Assert.That(_spectate.Step(SpectateStep.Next), Is.False);
            Assert.That(_spectate.Step(SpectateStep.Previous), Is.False);

            Assert.That(_spectate.Status, Is.EqualTo(new SpectateStatus(null, 0, SpectateReason.Dead)));
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void NextIsIgnoredWhileNotWatching()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, _health), Row(2, Team.Alpha, _health)));
            _spectate.Update(0f, null);

            Assert.That(_spectate.Step(SpectateStep.Next), Is.False);
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void ANewAllyAfterNoneIsPickedUp()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, 0)));
            _spectate.Update(0f, null);
            Assert.That(_spectate.TargetId, Is.Null);

            _spectate.Apply(Snapshot(11, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(9, Team.Alpha, _health)));
            _spectate.Update(1f / _tickRate, null);

            Assert.That(_spectate.Status, Is.EqualTo(new SpectateStatus(9, 1, SpectateReason.Dead)));
        }

        [Test]
        public void RespawningStopsWatching()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(4, Team.Alpha, _health)));
            _spectate.Update(0f, null);

            _spectate.Apply(Snapshot(11, DeathCamera.Ally, Row(_self, Team.Alpha, _health), Row(4, Team.Alpha, _health)));

            Assert.That(_spectate.Update(1f / _tickRate, null), Is.True);
            Assert.That(_spectate.Status, Is.Null);
            Assert.That(_spectate.TryGetView(out _, out _), Is.False);
        }

        [Test]
        public void TheViewIsTheAllysEyeWithTheInterpolatedFacing()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(4, Team.Alpha, _health, new Vector3(0f, 0f, 0f), 0f)));
            _spectate.Apply(Snapshot(12, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(4, Team.Alpha, _health, new Vector3(2f, 0f, 0f), 1f)));

            _spectate.Update(0f, null);
            _spectate.Update(1f / _tickRate, null);

            Assert.That(_spectate.TryGetView(out var eye, out var yaw), Is.True);
            Assert.That(eye.X, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(eye.Y, Is.EqualTo(_stance.Standing.EyeY).Within(1e-5f));
            Assert.That(eye.Z, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(yaw, Is.EqualTo(0.5f).Within(1e-3f));
        }

        [Test]
        public void TheEyeFollowsTheAllysStance()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(4, Team.Alpha, _health, Vector3.Zero, 0f, crouched: true)));
            _spectate.Update(0f, null);

            Assert.That(_spectate.TryGetView(out var eye, out _), Is.True);
            Assert.That(eye.Y, Is.EqualTo(_stance.Crouched.EyeY).Within(1e-5f));
        }

        [Test]
        public void AFailedSendStillSwitchesButReportsItWasNotSent()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(2, Team.Alpha, _health), Row(4, Team.Alpha, _health)));
            _spectate.Update(0f, null);
            _connected = false;

            Assert.That(_spectate.Step(SpectateStep.Next), Is.False);
            Assert.That(_spectate.TargetId, Is.EqualTo(4));
        }

        [Test]
        public void ResetStopsWatchingAndForgetsEveryone()
        {
            _spectate.Apply(Snapshot(10, DeathCamera.Ally, Row(_self, Team.Alpha, 0), Row(4, Team.Alpha, _health)));
            _spectate.Update(0f, null);

            Assert.That(_spectate.Reset(), Is.True);
            Assert.That(_spectate.Status, Is.Null);

            _spectate.Update(0f, null);
            Assert.That(_spectate.Status, Is.Null, "without a snapshot the client does not know it is dead");
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new ClientSpectate(_self, _tickRate, null, _ => true));
            Assert.Throws<ArgumentNullException>(() => new ClientSpectate(_self, _tickRate, _stance, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientSpectate(_self, 0, _stance, _ => true));
            Assert.Throws<ArgumentNullException>(() => _spectate.Apply(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _spectate.Update(-1f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _spectate.Update(float.NaN, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _spectate.Step((SpectateStep)9));
        }

        private static SnapshotMessage Snapshot(int tick, DeathCamera deathCamera, params PlayerSnapshot[] rows)
        {
            var snapshot = new SnapshotMessage { Tick = tick };
            var rules = snapshot.Match.Rules;
            rules.DeathCamera = deathCamera;
            snapshot.Match.Rules = rules;
            foreach (var source in rows)
            {
                ref var row = ref snapshot.Players.Add();
                row = source;
            }

            return snapshot;
        }

        private static PlayerSnapshot Row(int id, Team team, int health, bool downed = false)
        {
            return Row(id, team, health, new Vector3(id, 0f, 0f), 0f, downed: downed);
        }

        private static PlayerSnapshot Row(
            int id, Team team, int health, Vector3 position, float yaw, bool crouched = false, bool downed = false)
        {
            return new PlayerSnapshot
            {
                PlayerId = id,
                Team = team,
                Health = health,
                Position = position,
                Yaw = yaw,
                IsCrouched = crouched,
                Downed = new DownedSync(downed, downed ? 10 : 0, 0),
                WeaponId = "",
            };
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using NVector3 = System.Numerics.Vector3;

namespace Shooter.Net.Tests
{
    public sealed class NetV2LockOnWarningPartTests
    {
        private const int _localId = 4;
        private const int _missileId = 21;

        private readonly List<string> _logs = new List<string>();
        private NVector3? _self;
        private float _heading;
        private NetV2LockOnWarningPart _part;

        [SetUp]
        public void SetUp()
        {
            GameSignals.LockOnWarning.Reset();
            _logs.Clear();
            _self = NVector3.Zero;
            _heading = 0f;
            _part = new NetV2LockOnWarningPart(() => _localId, () => _self, () => _heading, _logs.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            GameSignals.LockOnWarning.Reset();
        }

        [Test]
        public void ALockedWarningFromTheServerIsPublishedTowardTheMissile()
        {
            Assert.That(_part.Handle(Locked(new NVector3(10f, 0f, 0f))), Is.True);

            var published = GameSignals.LockOnWarning.Value;
            Assert.That(published.HasValue, Is.True);
            Assert.That(published.Value.Stage, Is.EqualTo(LockOnWarningStage.Locked));
            Assert.That(published.Value.BearingDegrees, Is.EqualTo(90f).Within(1e-3f));
            Assert.That(_logs, Does.Contain("[Combat] lock-on warning locked bearing=90"));
        }

        [Test]
        public void TurningTowardTheMissileTurnsTheWarningAhead()
        {
            _part.Handle(Locked(new NVector3(10f, 0f, 0f)));

            _heading = 90f;
            _part.Tick(0.016f);

            Assert.That(GameSignals.LockOnWarning.Value.Value.BearingDegrees, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void TheMissileInTheSnapshotMovesTheWarning()
        {
            _part.Handle(Locked(new NVector3(10f, 0f, 0f)));

            _part.Apply(Snapshot(new NVector3(-10f, 0f, 0f)));
            _part.Tick(0.016f);

            Assert.That(GameSignals.LockOnWarning.Value.Value.BearingDegrees, Is.EqualTo(-90f).Within(1e-3f));
        }

        [Test]
        public void TheServerClearingItTakesTheWarningDown()
        {
            _part.Handle(Locked(new NVector3(10f, 0f, 0f)));

            _part.Handle(new LockOnWarningMessage { Stage = LockOnWarningStage.None });

            Assert.That(GameSignals.LockOnWarning.Value, Is.Null);
            Assert.That(_part.Published, Is.Null);
            Assert.That(_logs, Does.Contain("[Combat] lock-on warning cleared"));
        }

        [Test]
        public void WithoutABodyNothingIsShownUntilItComesBack()
        {
            _self = null;
            _part.Handle(Locked(new NVector3(10f, 0f, 0f)));

            Assert.That(GameSignals.LockOnWarning.Value, Is.Null);

            _self = NVector3.Zero;
            _part.Tick(0.016f);

            Assert.That(GameSignals.LockOnWarning.Value.HasValue, Is.True);
        }

        [Test]
        public void AWarningSomeoneElsePublishedIsNotCleared()
        {
            _part.Handle(Locked(new NVector3(10f, 0f, 0f)));
            var other = new LockOnWarningStatus(LockOnWarningStage.Tracking, 15f);
            GameSignals.LockOnWarning.Publish(other);

            _part.Dispose();

            Assert.That(GameSignals.LockOnWarning.Value, Is.EqualTo((LockOnWarningStatus?)other));
        }

        [Test]
        public void DisposingTakesTheWarningDownAndIgnoresLaterMessages()
        {
            _part.Handle(Locked(new NVector3(10f, 0f, 0f)));

            _part.Dispose();
            Assert.That(GameSignals.LockOnWarning.Value, Is.Null);

            Assert.That(_part.Handle(Locked(new NVector3(10f, 0f, 0f))), Is.True, "the message is still taken so no one else handles it");
            Assert.That(GameSignals.LockOnWarning.Value, Is.Null);
        }

        [Test]
        public void OtherMessagesAreLeftForOtherParts()
        {
            Assert.That(_part.Handle(new ShotMessage()), Is.False);
        }

        [Test]
        public void TheRegistrationGivesNoPartForAReplay()
        {
            using var client = new MatchClient(new SilentTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add);
            var replay = new NetV2ClientPartsContext(client, _ => true, _logs.Add) { Replay = true };
            var live = new NetV2ClientPartsContext(client, _ => true, _logs.Add);

            Assert.That(NetV2LockOnWarningPart.Create(replay), Is.Null);
            var part = NetV2LockOnWarningPart.Create(live);
            Assert.That(part, Is.InstanceOf<NetV2LockOnWarningPart>());
            part.Dispose();
            Assert.Throws<ArgumentNullException>(() => NetV2LockOnWarningPart.Create(null));
            Assert.Throws<ArgumentNullException>(() => new NetV2LockOnWarningPart(null));
        }

        private static LockOnWarningMessage Locked(NVector3 source)
        {
            return new LockOnWarningMessage { Stage = LockOnWarningStage.Locked, ProjectileId = _missileId, Source = source };
        }

        private static SnapshotMessage Snapshot(NVector3 missile)
        {
            var snapshot = new SnapshotMessage { Tick = 3 };
            snapshot.Match.State = GameModeState.Playing;
            ref var me = ref snapshot.Players.Add();
            me.PlayerId = _localId;
            me.Health = 100;
            ref var row = ref snapshot.Projectiles.Add();
            row.ProjectileId = _missileId;
            row.Position = missile;
            return snapshot;
        }
    }
}

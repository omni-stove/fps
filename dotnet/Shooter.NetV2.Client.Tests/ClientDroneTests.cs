using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientDroneTests
    {
        private const int _localPlayerId = 1;
        private const string _droneId = "deploy-drone";

        // 送る知らせは使い回しなので、送ったときの中身を写しておく
        private readonly List<Sent> _sent = new List<Sent>();
        private readonly List<string> _log = new List<string>();
        private readonly List<int> _appeared = new List<int>();
        private readonly List<int> _gone = new List<int>();
        private ClientDrone _drone;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _log.Clear();
            _appeared.Clear();
            _gone.Clear();
            _drone = new ClientDrone(
                (message, channel) =>
                {
                    var control = (DroneControlMessage)message;
                    _sent.Add(new Sent(control.Deployable, control.Sequence, control.Move, control.Climb, control.Yaw, control.Recall, channel));
                },
                _log.Add);
            _drone.Appeared += _appeared.Add;
            _drone.Gone += _gone.Add;
        }

        [Test]
        public void AnOwnDroneAppearsWithItsLaunchPointAndYaw()
        {
            _drone.Apply(Snapshot(Row(4, DeployableKind.Drone, _localPlayerId, new Vector3(-3f, 0.5f, 0f), 90f)), _localPlayerId);

            Assert.That(_appeared, Is.EqualTo(new[] { 4 }));
            Assert.That(_drone.DroneId, Is.EqualTo(4));
            Assert.That(_drone.TypeId, Is.EqualTo(_droneId));
            Assert.That(_drone.Launch, Is.EqualTo(new Vector3(-3f, 0.5f, 0f)));
            Assert.That(_drone.Yaw, Is.EqualTo(90f));
            Assert.That(_drone.IsPiloting, Is.False, "the part decides when to start piloting");
        }

        [Test]
        public void SomeoneElsesDroneAndOtherKindsAreIgnored()
        {
            _drone.Apply(Snapshot(
                Row(4, DeployableKind.Drone, 2, Vector3.Zero, 0f),
                Row(5, DeployableKind.Shield, _localPlayerId, Vector3.Zero, 0f)), _localPlayerId);

            Assert.That(_appeared, Is.Empty);
            Assert.That(_drone.DroneId, Is.Null);
            Assert.That(_drone.Begin(), Is.False);
        }

        [Test]
        public void ADroneSeenBeforeDoesNotAppearAgain()
        {
            var first = Snapshot(Row(4, DeployableKind.Drone, _localPlayerId, Vector3.Zero, 0f));
            _drone.Apply(first, _localPlayerId);
            _drone.Apply(Snapshot(Row(4, DeployableKind.Drone, _localPlayerId, new Vector3(1f, 2f, 3f), 45f)), _localPlayerId);

            Assert.That(_appeared, Is.EqualTo(new[] { 4 }));
            Assert.That(_drone.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(_drone.Yaw, Is.EqualTo(45f));
            Assert.That(_drone.Launch, Is.EqualTo(Vector3.Zero), "the launch point stays where it was first seen");
        }

        [Test]
        public void SteeringSendsTheInputUnreliablyAndResendsItWhileMoving()
        {
            AppearAndPilot();

            Assert.That(_drone.Steer(new Vector2(0f, 1f), 0.5f, 120f, 0.016f), Is.True);
            Assert.That(_drone.Steer(new Vector2(0f, 1f), 0.5f, 120f, 0.05f), Is.False, "the same input is not sent again before the resend interval");
            Assert.That(_drone.Steer(new Vector2(0f, 1f), 0.5f, 120f, ClientDrone.ResendSeconds), Is.True);

            Assert.That(_sent, Has.Count.EqualTo(2));
            Assert.That(_sent[0].Deployable, Is.EqualTo(4));
            Assert.That(_sent[0].Move, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(_sent[0].Climb, Is.EqualTo(0.5f));
            Assert.That(_sent[0].Yaw, Is.EqualTo(120f));
            Assert.That(_sent[0].Recall, Is.False);
            Assert.That(_sent[0].Channel, Is.EqualTo(TransportChannel.Unreliable));
            Assert.That(_sent[1].Sequence, Is.GreaterThan(_sent[0].Sequence));
            Assert.That(_log, Does.Contain("[Drone] owner=1 kind=deploy-drone serial=4 piloting started"));
        }

        [Test]
        public void AChangedInputIsSentAtOnceAndAStillDroneSendsNothing()
        {
            AppearAndPilot();

            Assert.That(_drone.Steer(Vector2.Zero, 0f, 90f, 0.016f), Is.False, "still and facing the same way");
            Assert.That(_drone.Steer(Vector2.Zero, 0f, 100f, 0.016f), Is.True, "turning is sent");
            Assert.That(_drone.Steer(Vector2.Zero, 0f, 100f, 0.001f), Is.False, "too soon to confirm the turn");
            Assert.That(_drone.Steer(Vector2.Zero, 0f, 100f, ClientDrone.ResendSeconds), Is.True, "the last turn is confirmed once");
            Assert.That(_drone.Steer(Vector2.Zero, 0f, 100f, 1f), Is.False, "a still drone is not resent after that");
            Assert.That(_drone.Steer(new Vector2(1f, 0f), 0f, 100f, 0.001f), Is.True);

            Assert.That(_sent, Has.Count.EqualTo(3));
            Assert.That(_sent[0].Channel, Is.EqualTo(TransportChannel.Unreliable));
            Assert.That(_sent[1].Channel, Is.EqualTo(TransportChannel.Reliable));
            Assert.That(_sent[1].Yaw, Is.EqualTo(100f));
        }

        [Test]
        public void ALongMoveAndAStrongClimbAreClamped()
        {
            AppearAndPilot();

            _drone.Steer(new Vector2(3f, 4f), -5f, 0f, 0.016f);

            Assert.That(_sent[0].Move.X, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(_sent[0].Move.Y, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(_sent[0].Climb, Is.EqualTo(-1f));
        }

        [Test]
        public void NothingIsSentWhileNotPiloting()
        {
            _drone.Apply(Snapshot(Row(4, DeployableKind.Drone, _localPlayerId, Vector3.Zero, 0f)), _localPlayerId);

            Assert.That(_drone.Steer(new Vector2(0f, 1f), 0f, 0f, 0.016f), Is.False);
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void ANonFiniteInputIsRejected()
        {
            AppearAndPilot();

            Assert.Throws<ArgumentException>(() => _drone.Steer(new Vector2(float.NaN, 0f), 0f, 0f, 0.016f));
            Assert.Throws<ArgumentException>(() => _drone.Steer(Vector2.Zero, float.PositiveInfinity, 0f, 0.016f));
            Assert.Throws<ArgumentException>(() => _drone.Steer(Vector2.Zero, 0f, float.NaN, 0.016f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _drone.Steer(Vector2.Zero, 0f, 0f, -1f));
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void LeavingWhileMovingSendsAStopReliably()
        {
            AppearAndPilot();
            _drone.Steer(new Vector2(0f, 1f), 0f, 30f, 0.016f);

            Assert.That(_drone.Leave(DronePilotEnd.Interact), Is.True);

            Assert.That(_drone.IsPiloting, Is.False);
            Assert.That(_sent, Has.Count.EqualTo(2));
            Assert.That(_sent[1].Move, Is.EqualTo(Vector2.Zero));
            Assert.That(_sent[1].Climb, Is.Zero);
            Assert.That(_sent[1].Yaw, Is.EqualTo(30f));
            Assert.That(_sent[1].Channel, Is.EqualTo(TransportChannel.Reliable));
            Assert.That(_log, Does.Contain("[Drone] owner=1 kind=deploy-drone serial=4 piloting ended reason=Interact"));
            Assert.That(_drone.Leave(DronePilotEnd.Interact), Is.False);
        }

        [Test]
        public void LeavingAStillDroneSendsNothing()
        {
            AppearAndPilot();

            _drone.Leave(DronePilotEnd.Interact);

            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void RecallSendsTheFlagReliablyAndStopsPiloting()
        {
            AppearAndPilot();

            Assert.That(_drone.Recall(), Is.True);

            Assert.That(_drone.IsPiloting, Is.False);
            Assert.That(_sent, Has.Count.EqualTo(1));
            Assert.That(_sent[0].Recall, Is.True);
            Assert.That(_sent[0].Deployable, Is.EqualTo(4));
            Assert.That(_sent[0].Channel, Is.EqualTo(TransportChannel.Reliable));
            Assert.That(_log, Does.Contain("[Drone] owner=1 kind=deploy-drone serial=4 recalled at=(-3.00, 0.50, 0.00)"));
        }

        [Test]
        public void RecallWithoutADroneSendsNothing()
        {
            Assert.That(_drone.Recall(), Is.False);
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void ADroneMissingFromTheSnapshotIsGoneAndEndsPiloting()
        {
            AppearAndPilot();

            _drone.Apply(Snapshot(), _localPlayerId);

            Assert.That(_gone, Is.EqualTo(new[] { 4 }));
            Assert.That(_drone.DroneId, Is.Null);
            Assert.That(_drone.IsPiloting, Is.False);
            Assert.That(_log, Does.Contain("[Drone] owner=1 kind=deploy-drone serial=4 piloting ended reason=DroneGone"));
            Assert.That(_drone.Steer(new Vector2(0f, 1f), 0f, 0f, 0.016f), Is.False);
        }

        [Test]
        public void ANewerDroneReplacesTheOneBeingPiloted()
        {
            AppearAndPilot();

            _drone.Apply(Snapshot(
                Row(4, DeployableKind.Drone, _localPlayerId, Vector3.Zero, 0f),
                Row(9, DeployableKind.Drone, _localPlayerId, new Vector3(5f, 0.5f, 5f), 180f)), _localPlayerId);

            Assert.That(_appeared, Is.EqualTo(new[] { 4, 9 }));
            Assert.That(_drone.DroneId, Is.EqualTo(9));
            Assert.That(_drone.Launch, Is.EqualTo(new Vector3(5f, 0.5f, 5f)));
            Assert.That(_drone.IsPiloting, Is.False, "the part starts piloting the new one");
            Assert.That(_gone, Is.Empty, "the old drone is still in the air");
        }

        [Test]
        public void ResetForgetsTheDroneWithoutSending()
        {
            AppearAndPilot();
            _drone.Steer(new Vector2(0f, 1f), 0f, 0f, 0.016f);
            _sent.Clear();

            _drone.Reset();

            Assert.That(_drone.DroneId, Is.Null);
            Assert.That(_drone.IsPiloting, Is.False);
            Assert.That(_sent, Is.Empty);
            _drone.Apply(Snapshot(Row(4, DeployableKind.Drone, _localPlayerId, Vector3.Zero, 0f)), _localPlayerId);
            Assert.That(_appeared, Is.EqualTo(new[] { 4, 4 }), "after a reset the same drone is new again");
        }

        private void AppearAndPilot()
        {
            _drone.Apply(Snapshot(Row(4, DeployableKind.Drone, _localPlayerId, new Vector3(-3f, 0.5f, 0f), 90f)), _localPlayerId);
            Assert.That(_drone.Begin(), Is.True);
            Assert.That(_drone.Begin(), Is.False, "already piloting");
        }

        private static DeployableSnapshot Row(int id, DeployableKind kind, int ownerId, Vector3 position, float yaw)
        {
            return new DeployableSnapshot
            {
                DeployableId = id,
                Present = DeployableFields.All,
                Kind = kind,
                TypeId = kind == DeployableKind.Drone ? _droneId : "deploy-shield",
                Team = Team.Alpha,
                HasOwner = true,
                OwnerId = ownerId,
                Position = position,
                Yaw = yaw,
                Health = DroneSpec.DefaultMaxHealth,
            };
        }

        private static SnapshotMessage Snapshot(params DeployableSnapshot[] rows)
        {
            var snapshot = new SnapshotMessage();
            foreach (var row in rows)
            {
                snapshot.Deployables.Add() = row;
            }

            return snapshot;
        }

        private readonly struct Sent
        {
            public Sent(int deployable, int sequence, Vector2 move, float climb, float yaw, bool recall, TransportChannel channel)
            {
                Deployable = deployable;
                Sequence = sequence;
                Move = move;
                Climb = climb;
                Yaw = yaw;
                Recall = recall;
                Channel = channel;
            }

            public int Deployable { get; }

            public int Sequence { get; }

            public Vector2 Move { get; }

            public float Climb { get; }

            public float Yaw { get; }

            public bool Recall { get; }

            public TransportChannel Channel { get; }
        }
    }
}

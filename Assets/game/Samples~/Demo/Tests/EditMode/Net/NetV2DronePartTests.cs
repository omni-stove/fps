using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    // 入力は差し替えの口から渡し、操縦の始まり・送る入力・降りる・消える・GameSignals.Drone を確かめる
    public sealed class NetV2DronePartTests
    {
        private const int _localPlayerId = 1;
        private const string _droneId = "deploy-drone";

        private readonly List<(DroneControlMessage Control, TransportChannel Channel)> _sent = new List<(DroneControlMessage, TransportChannel)>();
        private readonly List<string> _logs = new List<string>();
        private readonly List<Object> _created = new List<Object>();
        private DroneDeployDefinition _definition;
        private DroneInput _input;
        private bool _canAct;
        private bool _leaveHeld;
        private NetV2DronePart _part;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _logs.Clear();
            _input = default;
            _canAct = true;
            _leaveHeld = false;
            GameSignals.Drone.Clear();
            _definition = ScriptableObject.CreateInstance<DroneDeployDefinition>();
            _definition.Id = _droneId;
            _definition.BatterySeconds = 10f;
            _definition.ControlRange = 20f;
            _created.Add(_definition);
            _part = new NetV2DronePart(
                (message, channel) =>
                {
                    var control = (DroneControlMessage)message;
                    _sent.Add((new DroneControlMessage
                    {
                        Deployable = control.Deployable,
                        Sequence = control.Sequence,
                        Move = control.Move,
                        Climb = control.Climb,
                        Yaw = control.Yaw,
                        Recall = control.Recall,
                    }, channel));
                },
                () => _localPlayerId,
                () => _canAct,
                new DeployAbilityDefinition[] { _definition },
                () => null,
                _logs.Add,
                _ => _input,
                () => _leaveHeld);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            GameSignals.Drone.Clear();
        }

        [Test]
        public void AnOwnDroneStartsPilotingWithAViewAtItsBody()
        {
            _part.Apply(Snapshot(Row(3, _localPlayerId, new System.Numerics.Vector3(-3f, 0.5f, 0f), 90f)));

            Assert.That(_part.IsPiloting, Is.True);
            Assert.That(_part.ViewCamera, Is.Not.Null);
            Assert.That(_part.ViewCamera.gameObject.activeSelf, Is.True);
            Assert.That(_part.ViewCamera.name, Is.EqualTo(NetV2DronePart.ViewObjectName));
            Assert.That(_part.ViewCamera.depth, Is.EqualTo(DroneControl.ViewDepth));
            var eye = DroneDeployDefinition.CollisionRadiusOf(_definition.ToDeploySpec());
            Assert.That(_part.ViewCamera.transform.position.y, Is.EqualTo(0.5f + eye).Within(1e-4f));
            Assert.That(_part.ViewCamera.transform.eulerAngles.y, Is.EqualTo(90f).Within(1e-3f));
            Assert.That(_logs, Does.Contain("[Drone] owner=1 kind=deploy-drone serial=3 piloting started"));
        }

        [Test]
        public void SomeoneElsesDroneIsNotPiloted()
        {
            _part.Apply(Snapshot(Row(3, 2, System.Numerics.Vector3.Zero, 0f)));

            Assert.That(_part.IsPiloting, Is.False);
            Assert.That(_part.ViewCamera, Is.Null);
            Assert.That(GameSignals.Drone.Value, Is.Null);
        }

        [Test]
        public void ADroneThatAppearsWhileTheOwnerCannotActIsNotPiloted()
        {
            _canAct = false;

            _part.Apply(Snapshot(Row(3, _localPlayerId, System.Numerics.Vector3.Zero, 0f)));
            _part.Tick(0.1f);

            Assert.That(_part.IsPiloting, Is.False);
            Assert.That(GameSignals.Drone.Value.HasValue, Is.True, "the drone still flies its battery down");
            Assert.That(GameSignals.Drone.Value.Value.Piloting, Is.False);
        }

        [Test]
        public void TheInputIsSentWithTheTurnedYaw()
        {
            _part.Apply(Snapshot(Row(3, _localPlayerId, System.Numerics.Vector3.Zero, 90f)));
            _input = new DroneInput(new Vector2(0f, 1f), new Vector2(30f, 10f), 1f, false);

            _part.Tick(0.016f);

            Assert.That(_sent, Has.Count.EqualTo(1));
            Assert.That(_sent[0].Control.Deployable, Is.EqualTo(3));
            Assert.That(_sent[0].Control.Move, Is.EqualTo(new System.Numerics.Vector2(0f, 1f)));
            Assert.That(_sent[0].Control.Climb, Is.EqualTo(1f));
            Assert.That(_sent[0].Control.Yaw, Is.EqualTo(120f).Within(1e-3f));
            Assert.That(_sent[0].Channel, Is.EqualTo(TransportChannel.Unreliable));
            Assert.That(_part.Pitch, Is.EqualTo(-10f).Within(1e-3f));
            Assert.That(_part.ViewCamera.transform.eulerAngles.y, Is.EqualTo(120f).Within(1e-3f));
        }

        [Test]
        public void ThePitchIsClampedLikeTheOfflineDrone()
        {
            _part.Apply(Snapshot(Row(3, _localPlayerId, System.Numerics.Vector3.Zero, 0f)));
            _input = new DroneInput(Vector2.zero, new Vector2(0f, 500f), 0f, false);

            _part.Tick(0.016f);

            Assert.That(_part.Pitch, Is.EqualTo(-DroneControl.MaxPitchDegrees).Within(1e-3f));
        }

        [Test]
        public void TheViewFollowsTheServerPosition()
        {
            _part.Apply(Snapshot(Row(3, _localPlayerId, System.Numerics.Vector3.Zero, 0f)));
            _part.Apply(Snapshot(Row(3, _localPlayerId, new System.Numerics.Vector3(4f, 0.5f, 0f), 0f)));

            _part.Tick(0.05f);
            var partway = _part.ViewCamera.transform.position.x;
            for (var i = 0; i < 60; i++)
            {
                _part.Tick(0.05f);
            }

            Assert.That(partway, Is.GreaterThan(0f).And.LessThan(4f), "the view moves toward the new position without jumping");
            Assert.That(_part.ViewCamera.transform.position.x, Is.EqualTo(4f).Within(1e-3f));
        }

        [Test]
        public void LeavingStopsTheDroneHidesTheViewAndKeepsTheDrone()
        {
            _part.Apply(Snapshot(Row(3, _localPlayerId, System.Numerics.Vector3.Zero, 0f)));
            _input = new DroneInput(new Vector2(0f, 1f), Vector2.zero, 0f, false);
            _part.Tick(0.016f);

            _input = new DroneInput(Vector2.zero, Vector2.zero, 0f, true);
            _part.Tick(0.016f);

            Assert.That(_part.IsPiloting, Is.False);
            Assert.That(_part.ViewCamera.gameObject.activeSelf, Is.False);
            Assert.That(_sent, Has.Count.EqualTo(2));
            Assert.That(_sent[1].Control.Move, Is.EqualTo(System.Numerics.Vector2.Zero));
            Assert.That(_sent[1].Channel, Is.EqualTo(TransportChannel.Reliable));
            Assert.That(_logs, Does.Contain("[Drone] owner=1 kind=deploy-drone serial=3 piloting ended reason=Interact"));
            Assert.That(GameSignals.Drone.Value.HasValue, Is.True);
            Assert.That(GameSignals.Drone.Value.Value.Piloting, Is.False);
        }

        [Test]
        public void FallingWhilePilotingEndsIt()
        {
            _part.Apply(Snapshot(Row(3, _localPlayerId, System.Numerics.Vector3.Zero, 0f)));

            _canAct = false;
            _part.Apply(Snapshot(Row(3, _localPlayerId, System.Numerics.Vector3.Zero, 0f)));

            Assert.That(_part.IsPiloting, Is.False);
            Assert.That(_logs, Does.Contain("[Drone] owner=1 kind=deploy-drone serial=3 piloting ended reason=Incapacitated"));
        }

        [Test]
        public void AGoneDroneClearsTheSignalAndTheView()
        {
            _part.Apply(Snapshot(Row(3, _localPlayerId, System.Numerics.Vector3.Zero, 0f)));
            _part.Tick(0.016f);
            Assert.That(GameSignals.Drone.Value.HasValue, Is.True);

            _part.Apply(Snapshot());
            _part.Tick(0.016f);

            Assert.That(_part.IsPiloting, Is.False);
            Assert.That(_part.ViewCamera.gameObject.activeSelf, Is.False);
            Assert.That(GameSignals.Drone.Value, Is.Null);
        }

        [Test]
        public void TheBatteryAndRangeAreCountedWithTheDefinition()
        {
            _part.Apply(Snapshot(Row(3, _localPlayerId, System.Numerics.Vector3.Zero, 0f)));
            _part.Apply(Snapshot(Row(3, _localPlayerId, new System.Numerics.Vector3(5f, 0f, 0f), 0f)));

            _part.Tick(2.5f);

            var status = GameSignals.Drone.Value.Value;
            Assert.That(status.BatteryFraction, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(status.RangeFraction, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(status.Piloting, Is.True);
        }

        [Test]
        public void RecallSendsTheFlagAndEndsPiloting()
        {
            _part.Apply(Snapshot(Row(3, _localPlayerId, System.Numerics.Vector3.Zero, 0f)));

            Assert.That(_part.Recall(), Is.True);

            Assert.That(_part.IsPiloting, Is.False);
            Assert.That(_sent, Has.Count.EqualTo(1));
            Assert.That(_sent[0].Control.Recall, Is.True);
            Assert.That(_sent[0].Channel, Is.EqualTo(TransportChannel.Reliable));
        }

        [Test]
        public void DisposingWhilePilotingRemovesTheView()
        {
            _part.Apply(Snapshot(Row(3, _localPlayerId, System.Numerics.Vector3.Zero, 0f)));
            var view = _part.ViewCamera.gameObject;

            _part.Dispose();

            Assert.That(view == null, Is.True);
            Assert.That(_part.IsPiloting, Is.False);
            Assert.That(_logs, Does.Contain("[Drone] owner=1 kind=deploy-drone serial=3 piloting ended reason=Disabled"));
        }

        [Test]
        public void TheRegistrationGivesNoPartForAReplay()
        {
            using var client = new MatchClient(new SilentTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add);
            var replay = new NetV2ClientPartsContext(client, _ => true, _logs.Add) { Replay = true };
            var live = new NetV2ClientPartsContext(client, _ => true, _logs.Add);

            Assert.That(NetV2DronePart.Create(replay), Is.Null);
            var part = NetV2DronePart.Create(live);
            Assert.That(part, Is.InstanceOf<NetV2DronePart>());
            part.Dispose();
            Assert.Throws<ArgumentNullException>(() => NetV2DronePart.Create(null));
        }

        private static DeployableSnapshot Row(int id, int ownerId, System.Numerics.Vector3 position, float yaw)
        {
            return new DeployableSnapshot
            {
                DeployableId = id,
                Present = DeployableFields.All,
                Kind = DeployableKind.Drone,
                TypeId = _droneId,
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

        private sealed class SilentTransport : IMatchTransport
        {
            public bool IsActive => false;

            public void Listen(string address, ushort port)
            {
                throw new InvalidOperationException("a client transport does not listen");
            }

            public void Connect(string address, ushort port)
            {
            }

            public void Disconnect(ConnectionId connection, string reason)
            {
            }

            public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
            {
            }

            public int GetMaxPayloadSize(TransportChannel channel)
            {
                return MessageWriter.DefaultMaxLength;
            }

            public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
            {
                milliseconds = 0;
                return false;
            }

            public bool TryReceive(out TransportEvent transportEvent)
            {
                transportEvent = default;
                return false;
            }

            public void Shutdown(string reason)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}

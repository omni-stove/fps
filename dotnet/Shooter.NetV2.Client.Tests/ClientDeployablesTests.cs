using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientDeployablesTests
    {
        private const int _localPlayerId = 1;

        // 送る知らせは使い回しなので、送ったときの中身を写しておく
        private readonly List<(string TypeId, Vector3 Position, float Yaw)> _sent = new List<(string TypeId, Vector3 Position, float Yaw)>();
        private readonly List<string> _log = new List<string>();
        private FakeViews _views;
        private bool _connected;
        private ClientDeployables _deployables;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _log.Clear();
            _connected = true;
            _views = new FakeViews();
            _deployables = new ClientDeployables(
                message =>
                {
                    if (!_connected)
                    {
                        return false;
                    }

                    var request = (DeployRequestMessage)message;
                    _sent.Add((request.TypeId, request.Position, request.Yaw));
                    return true;
                },
                _log.Add);
            _deployables.Attach(_views);
        }

        [Test]
        public void ARequestSendsTheTypeThePointAndTheYaw()
        {
            Assert.That(_deployables.Request("deploy-turret", new Vector3(4f, 0.5f, -2f), 135f), Is.True);

            Assert.That(_sent, Has.Count.EqualTo(1));
            Assert.That(_sent[0].TypeId, Is.EqualTo("deploy-turret"));
            Assert.That(_sent[0].Position, Is.EqualTo(new Vector3(4f, 0.5f, -2f)));
            Assert.That(_sent[0].Yaw, Is.EqualTo(135f));
            Assert.That(_log, Does.Contain("[Deploy] net request kind=deploy-turret pos=(4.00, 0.50, -2.00) yaw=135.0 sent=True"));
            Assert.That(_views.Shown, Is.Empty, "nothing is shown until the server lists it");
        }

        [Test]
        public void ARequestWhileDisconnectedIsNotSent()
        {
            _connected = false;

            Assert.That(_deployables.Request("deploy-shield", Vector3.Zero, 0f), Is.False);
            Assert.That(_log, Does.Contain("[Deploy] net request kind=deploy-shield pos=(0.00, 0.00, 0.00) yaw=0.0 sent=False"));
        }

        [Test]
        public void ABadRequestIsRejectedBeforeSending()
        {
            Assert.Throws<ArgumentException>(() => _deployables.Request("", Vector3.Zero, 0f));
            Assert.Throws<ArgumentException>(() => _deployables.Request("deploy-shield", new Vector3(float.NaN, 0f, 0f), 0f));
            Assert.Throws<ArgumentException>(() => _deployables.Request("deploy-shield", Vector3.Zero, float.PositiveInfinity));
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void ListedDeployablesAreShownMovedAndRemoved()
        {
            _deployables.Apply(Snapshot(Row(1, DeployableKind.Turret, "deploy-turret", Team.Alpha, 1, new Vector3(-3f, 0.5f, 0f), 90f, 150),
                Row(2, DeployableKind.Shield, "deploy-shield", Team.Bravo, 2, new Vector3(2f, 0.5f, 0f), 270f, 300)), _localPlayerId);

            Assert.That(_views.Shown, Has.Count.EqualTo(2));
            Assert.That(_views.Shown[0].Kind, Is.EqualTo(DeployableKind.Turret));
            Assert.That(_views.Shown[0].TypeId, Is.EqualTo("deploy-turret"));
            Assert.That(_views.Shown[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(_views.Shown[0].IsOwn, Is.True);
            Assert.That(_views.Shown[0].Position, Is.EqualTo(new Vector3(-3f, 0.5f, 0f)));
            Assert.That(_views.Shown[0].Yaw, Is.EqualTo(90f));
            Assert.That(_views.Shown[0].Health, Is.EqualTo(150));
            Assert.That(_views.Shown[1].IsOwn, Is.False);
            Assert.That(_deployables.Count, Is.EqualTo(2));
            Assert.That(_deployables.TryGet(2, out var shield), Is.True);
            Assert.That(shield.OwnerId, Is.EqualTo(2));
            Assert.That(shield.Health, Is.EqualTo(300));
            Assert.That(_log, Does.Contain("[Deploy] net shown id=1 kind=deploy-turret own=True"));

            // タレットの頭が回り、盾が撃たれた
            _deployables.Apply(Snapshot(Row(1, DeployableKind.Turret, "deploy-turret", Team.Alpha, 1, new Vector3(-3f, 0.5f, 0f), 120f, 150),
                Row(2, DeployableKind.Shield, "deploy-shield", Team.Bravo, 2, new Vector3(2f, 0.5f, 0f), 270f, 275)), _localPlayerId);

            Assert.That(_views.Shown, Has.Count.EqualTo(2), "the same deployables are not shown again");
            Assert.That(_views.Shown[0].Yaw, Is.EqualTo(120f));
            Assert.That(_views.Shown[0].Moves, Is.EqualTo(1));
            Assert.That(_views.Shown[1].Moves, Is.EqualTo(0));
            Assert.That(_views.Shown[1].Health, Is.EqualTo(275));

            // 盾が壊れてスナップショットから消えた
            _deployables.Apply(Snapshot(Row(1, DeployableKind.Turret, "deploy-turret", Team.Alpha, 1, new Vector3(-3f, 0.5f, 0f), 120f, 150)), _localPlayerId);

            Assert.That(_views.Shown[1].Removed, Is.True);
            Assert.That(_views.Shown[0].Removed, Is.False);
            Assert.That(_deployables.Count, Is.EqualTo(1));
            Assert.That(_deployables.TryGet(2, out _), Is.False);
            Assert.That(_log, Does.Contain("[Deploy] net gone id=2 kind=deploy-shield"));
        }

        [Test]
        public void ADifferentDeployableUnderTheSameIdIsShownAgain()
        {
            _deployables.Apply(Snapshot(Row(5, DeployableKind.Shield, "deploy-shield", Team.Alpha, 1, Vector3.Zero, 0f, 300)), _localPlayerId);
            _deployables.Apply(Snapshot(Row(5, DeployableKind.Decoy, "deploy-decoy", Team.Bravo, 2, Vector3.Zero, 0f, 100)), _localPlayerId);

            Assert.That(_views.Shown, Has.Count.EqualTo(2));
            Assert.That(_views.Shown[0].Removed, Is.True);
            Assert.That(_views.Shown[1].Kind, Is.EqualTo(DeployableKind.Decoy));
            Assert.That(_views.Shown[1].Removed, Is.False);
        }

        [Test]
        public void DetachingRemovesEverythingShown()
        {
            _deployables.Apply(Snapshot(Row(1, DeployableKind.Shield, "deploy-shield", Team.Alpha, 1, Vector3.Zero, 0f, 300)), _localPlayerId);

            _deployables.Detach();

            Assert.That(_views.Shown[0].Removed, Is.True);
            Assert.That(_deployables.IsAttached, Is.False);
            Assert.That(_deployables.Count, Is.EqualTo(0));
            _deployables.Apply(Snapshot(Row(1, DeployableKind.Shield, "deploy-shield", Team.Alpha, 1, Vector3.Zero, 0f, 300)), _localPlayerId);
            Assert.That(_views.Shown, Has.Count.EqualTo(1), "a detached list shows nothing");
        }

        private static DeployableSnapshot Row(int id, DeployableKind kind, string typeId, Team team, int ownerId, Vector3 position, float yaw, int health)
        {
            return new DeployableSnapshot
            {
                DeployableId = id,
                Present = DeployableFields.All,
                Kind = kind,
                TypeId = typeId,
                Team = team,
                HasOwner = true,
                OwnerId = ownerId,
                Position = position,
                Yaw = yaw,
                Health = health,
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

        private sealed class FakeViews : IClientDeployableViews
        {
            public List<FakeView> Shown { get; } = new List<FakeView>();

            public IClientDeployableView Show(DeployableKind kind, string typeId, Team team, bool isOwn, Vector3 position, float yaw)
            {
                var view = new FakeView(kind, typeId, team, isOwn, position, yaw);
                Shown.Add(view);
                return view;
            }
        }

        private sealed class FakeView : IClientDeployableView
        {
            public FakeView(DeployableKind kind, string typeId, Team team, bool isOwn, Vector3 position, float yaw)
            {
                Kind = kind;
                TypeId = typeId;
                Team = team;
                IsOwn = isOwn;
                Position = position;
                Yaw = yaw;
            }

            public DeployableKind Kind { get; }

            public string TypeId { get; }

            public Team Team { get; }

            public bool IsOwn { get; }

            public Vector3 Position { get; private set; }

            public float Yaw { get; private set; }

            public int Health { get; private set; }

            public int Moves { get; private set; }

            public bool Removed { get; private set; }

            public void Move(Vector3 position, float yaw)
            {
                Position = position;
                Yaw = yaw;
                Moves++;
            }

            public void SetHealth(int health)
            {
                Health = health;
            }

            public void Remove()
            {
                Removed = true;
            }
        }
    }
}

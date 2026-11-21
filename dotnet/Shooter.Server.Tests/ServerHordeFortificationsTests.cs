using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed class ServerHordeFortificationsTests
    {
        private const float _reach = HordeSpec.DefaultInteractRadius;

        // 建物 0 は地面に接した 0 の上に 1 が載った 2 段の壁 (始めから置かれている)。建物 1 は x = 4 の 1 枚の門 (通貨で置く)
        private static readonly int[] _wallHealth = { 50, 50 };
        private static readonly bool[] _wallGrounded = { true, false };
        private static readonly (int, int)[] _wallAdjacency = { (0, 1) };
        private static readonly int[] _gateHealth = { 10 };
        private static readonly bool[] _gateGrounded = { true };
        private static readonly int[] _gateOnce = { 1 };
        private static readonly int[] _gateTwice = { 1, 1 };

        private StringWriter _log;
        private List<int> _sent;
        private HashSet<int> _alive;
        private ServerBodies _bodies;
        private ServerStructures _structures;
        private ServerHordeFortifications _fortifications;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _sent = new List<int>();
            _alive = new HashSet<int>();
            _bodies = new ServerBodies(NetMotorSettings.Default.Stance);
            _structures = new ServerStructures(Map(), _log) { Enabled = true };
            _fortifications = new ServerHordeFortifications(() => _structures, () => _bodies, _alive.Contains, _sent.Add, () => _log);
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
        }

        [Test]
        public void TheGateThatStartsUnbuiltIsTakenOutAndSentOnTheFirstTick()
        {
            _fortifications.Tick();

            Assert.That(_structures.DestroyedCount(1), Is.EqualTo(1));
            Assert.That(_structures.DestroyedCount(0), Is.EqualTo(0));
            Assert.That(_sent, Is.EqualTo(_gateOnce));
            Assert.That(_fortifications.IsBuilt(0), Is.True);
            Assert.That(_fortifications.IsBuilt(1), Is.False);
            Assert.That(_fortifications.PendingAction(0), Is.EqualTo(HordeFortificationAction.None));
            Assert.That(_fortifications.PendingAction(1), Is.EqualTo(HordeFortificationAction.Build));
            Assert.That(_fortifications.PendingAction(7), Is.EqualTo(HordeFortificationAction.None), "not a fortification");

            _fortifications.Tick();
            Assert.That(_sent, Has.Count.EqualTo(1), "nothing changes on the next tick");
        }

        [Test]
        public void NothingHappensUntilTheStructuresExist()
        {
            ServerStructures structures = null;
            var fortifications = new ServerHordeFortifications(() => structures, () => _bodies, _alive.Contains, _sent.Add, () => _log);

            fortifications.Prepare();
            fortifications.Tick();
            Assert.That(fortifications.FindInReach(new Vector3(4f, 0f, 0f), _reach), Is.Null);
            Assert.That(fortifications.Restore(1, HordeFortificationAction.Build), Is.EqualTo(0));
            Assert.That(_sent, Is.Empty);

            structures = _structures;
            fortifications.Tick();

            Assert.That(_structures.DestroyedCount(1), Is.EqualTo(1));
            Assert.That(_sent, Is.EqualTo(_gateOnce));
        }

        [Test]
        public void TheNearestFortificationWithSomethingToDoIsFound()
        {
            var gate = _fortifications.FindInReach(new Vector3(3f, 5f, 0f), _reach);

            Assert.That(gate.HasValue, Is.True, "the height does not count");
            Assert.That(gate.Value.Id, Is.EqualTo(1));
            Assert.That(gate.Value.NameKey, Is.EqualTo("Gate"));
            Assert.That(gate.Value.Action, Is.EqualTo(HordeFortificationAction.Build));
            Assert.That(gate.Value.RestorablePieces, Is.EqualTo(1));
            Assert.That(_fortifications.FindInReach(new Vector3(0.5f, 0f, 0f), _reach).Value.Id, Is.EqualTo(1), "the intact wall is passed over");
            Assert.That(_fortifications.FindInReach(new Vector3(-1f, 0f, 0f), 2f), Is.Null, "the gate is 5 m away");

            _structures.ApplyHit(10, new Vector3(0f, 0.5f, 0f), 60, 0f, null, _log);
            var wall = _fortifications.FindInReach(new Vector3(1.9f, 0f, 0f), _reach);

            Assert.That(wall.Value.Id, Is.EqualTo(0));
            Assert.That(wall.Value.Action, Is.EqualTo(HordeFortificationAction.Repair));
            Assert.That(wall.Value.RestorablePieces, Is.EqualTo(2));
            Assert.That(_fortifications.FindInReach(new Vector3(2f, 0f, 0f), _reach).Value.Id, Is.EqualTo(0), "the lower number wins a tie");
        }

        [Test]
        public void BuildingTheGatePutsItBackAndSendsIt()
        {
            _fortifications.Tick();

            Assert.That(_fortifications.Restore(1, HordeFortificationAction.Build), Is.EqualTo(1));

            Assert.That(_structures.DestroyedCount(1), Is.EqualTo(0));
            Assert.That(_fortifications.IsBuilt(1), Is.True);
            Assert.That(_fortifications.PendingAction(1), Is.EqualTo(HordeFortificationAction.None));
            Assert.That(_sent, Is.EqualTo(_gateTwice));
            Assert.That(_fortifications.FindInReach(new Vector3(4f, 0f, 0f), _reach), Is.Null, "nothing is left to do");
            Assert.That(_fortifications.Restore(3, HordeFortificationAction.Repair), Is.EqualTo(0), "not a fortification");
            Assert.Throws<ArgumentOutOfRangeException>(() => _fortifications.Restore(0, HordeFortificationAction.None));
        }

        [Test]
        public void ABrokenGateIsRepairedOnceBuilt()
        {
            _fortifications.Restore(1, HordeFortificationAction.Build);
            _structures.ApplyHit(12, new Vector3(4f, 0.5f, 0f), 10, 0f, null, _log);

            Assert.That(_fortifications.PendingAction(1), Is.EqualTo(HordeFortificationAction.Repair));
            Assert.That(_fortifications.Restore(1, HordeFortificationAction.Repair), Is.EqualTo(1));
            Assert.That(_fortifications.PendingAction(1), Is.EqualTo(HordeFortificationAction.None));
        }

        [Test]
        public void APieceALivingBodyStandsInStaysOut()
        {
            _structures.ApplyHit(10, new Vector3(0f, 0.5f, 0f), 60, 0f, null, _log);
            Place(1, new Vector3(0f, 0f, 0f));
            _alive.Add(1);

            var blocked = _fortifications.FindInReach(new Vector3(0f, 0f, 0f), _reach).Value;
            var restored = _fortifications.Restore(0, HordeFortificationAction.Repair);

            Assert.That(blocked.RestorablePieces, Is.LessThan(2));
            Assert.That(restored, Is.EqualTo(blocked.RestorablePieces));
            Assert.That(_structures.IsDestroyed(0, 0), Is.True, "the lower piece is where the body stands");

            _alive.Remove(1);
            Assert.That(_fortifications.Restore(0, HordeFortificationAction.Repair), Is.EqualTo(2 - restored), "a dead body does not block");
            Assert.That(_structures.DestroyedCount(0), Is.EqualTo(0));
        }

        [Test]
        public void ABodyBesideTheWallOrNotYetSpawnedDoesNotBlock()
        {
            _structures.ApplyHit(10, new Vector3(0f, 0.5f, 0f), 60, 0f, null, _log);
            Place(1, new Vector3(1.5f, 0f, 0f));
            _bodies.Add(2);
            _alive.Add(1);
            _alive.Add(2);

            Assert.That(_fortifications.FindInReach(new Vector3(1.5f, 0f, 0f), _reach).Value.RestorablePieces, Is.EqualTo(2));
        }

        [Test]
        public void PreparingAgainTakesTheBuiltGateOutAndKeepsTheWallAsItIs()
        {
            _fortifications.Restore(1, HordeFortificationAction.Build);
            _structures.ApplyHit(10, new Vector3(0f, 0.5f, 0f), 60, 0f, null, _log);

            _fortifications.Prepare();
            _fortifications.Tick();

            Assert.That(_fortifications.IsBuilt(1), Is.False);
            Assert.That(_structures.DestroyedCount(1), Is.EqualTo(1));
            Assert.That(_fortifications.PendingAction(0), Is.EqualTo(HordeFortificationAction.Repair), "structures stay broken between matches");
        }

        [Test]
        public void NewStructuresForANewMapStartOver()
        {
            _fortifications.Restore(1, HordeFortificationAction.Build);
            Assert.That(_fortifications.IsBuilt(1), Is.True);

            _structures = new ServerStructures(Map(), _log) { Enabled = true };

            Assert.That(_fortifications.IsBuilt(1), Is.False);
            Assert.That(_structures.DestroyedCount(1), Is.EqualTo(1));
        }

        private void Place(int playerId, Vector3 position)
        {
            _bodies.Add(playerId);
            _bodies.Teleport(playerId, position, 0f, 1);
        }

        private static ServerMapData Map()
        {
            var colliders = new[]
            {
                WorldCollider.Box(10, 0, MapColliderGroups.Piece(0, 0), new Vector3(0f, 0.5f, 0f), new Vector3(0.25f, 0.5f, 1f), Quaternion.Identity),
                WorldCollider.Box(11, 0, MapColliderGroups.Piece(0, 1), new Vector3(0f, 1.5f, 0f), new Vector3(0.25f, 0.5f, 1f), Quaternion.Identity),
                WorldCollider.Box(12, 0, MapColliderGroups.Piece(1, 0), new Vector3(4f, 0.5f, 0f), new Vector3(0.25f, 0.5f, 1f), Quaternion.Identity),
            };
            var layouts = new NetMapStructures(
                new[]
                {
                    new StructureLayout(_wallHealth, _wallGrounded, _wallAdjacency),
                    new StructureLayout(_gateHealth, _gateGrounded, Array.Empty<(int, int)>()),
                },
                new[]
                {
                    new MapFortification(1, new Vector3(4f, 0f, 0f), false, "Gate"),
                    new MapFortification(0, Vector3.Zero, true, "Wall"),
                });
            return new ServerMapData("Test", new WorldMap(colliders), TestMaps.NavMesh(), "memory", null, layouts);
        }
    }
}

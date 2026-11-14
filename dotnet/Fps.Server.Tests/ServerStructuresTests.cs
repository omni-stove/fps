using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerStructuresTests
    {
        // 地面に接した 0 の上に 1 が載った 2 段の壁
        private static readonly int[] _health = { 50, 50 };
        private static readonly bool[] _grounded = { true, false };
        private static readonly (int, int)[] _adjacency = { (0, 1) };
        private static readonly int[] _onePieceHealth = { 10 };
        private static readonly bool[] _onePieceGrounded = { true };
        private static readonly int[] _firstStructure = { 0 };

        private StringWriter _log;
        private List<int> _changed;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _changed = new List<int>();
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
        }

        [Test]
        public void BreakingTheBottomPieceCollapsesTheOneOnItAndOpensTheWall()
        {
            var map = Map(withLayouts: true);
            var structures = new ServerStructures(map, _log) { Enabled = true };

            Assert.That(structures.ApplyHit(10, new Vector3(0f, 0.5f, 0f), 30, 0f, _changed, _log), Is.EqualTo(1));
            Assert.That(structures.IsDestroyed(0, 0), Is.False);
            structures.ApplyHit(10, new Vector3(0f, 0.5f, 0f), 30, 0f, _changed, _log);

            Assert.That(structures.IsDestroyed(0, 0), Is.True);
            Assert.That(structures.IsDestroyed(0, 1), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Destruction] structure=0 destroyed=0 collapsed=1"));
            Assert.That(_changed, Is.EqualTo(_firstStructure));
            Assert.That(map.World.Raycast(new Vector3(-5f, 1.5f, 0f), Vector3.UnitX, 10f, ~0, out _), Is.False);
        }

        [Test]
        public void TheBlastIsScaledByTheDistanceToEachPiece()
        {
            var structures = new ServerStructures(Map(withLayouts: true), _log) { Enabled = true };

            // 下の部品まで 0.5 m、上の部品まで 1.5 m (半径 2 m)
            Assert.That(structures.ApplyAround(new Vector3(-1f, 0.5f, 0f), 100, 2f, _changed, _log), Is.EqualTo(2));

            Assert.That(structures.IsDestroyed(0, 0), Is.True, "75 damage breaks the 50 health piece");
            Assert.That(_log.ToString(), Does.Contain("destroyed=0 collapsed=1"));
        }

        [TestCase(0f, 100)]
        [TestCase(1f, 50)]
        [TestCase(1.5f, 25)]
        [TestCase(2f, 0)]
        [TestCase(3f, 0)]
        public void ScaleMatchesUnity(float distance, int expected)
        {
            Assert.That(ServerStructures.Scale(100, distance, 2f), Is.EqualTo(expected));
        }

        [Test]
        public void WithoutTheLayoutsThePiecesStayAsWalls()
        {
            var map = Map(withLayouts: false);
            var structures = new ServerStructures(map, _log) { Enabled = true };

            Assert.That(structures.ApplyHit(10, Vector3.Zero, 1000, 0f, _changed, _log), Is.EqualTo(0));
            Assert.That(_log.ToString(), Does.Contain("has 1 destructible structure(s) but no Test.structures.json"));
            Assert.That(map.World.Raycast(new Vector3(-5f, 0.5f, 0f), Vector3.UnitX, 10f, ~0, out _), Is.True);
        }

        [Test]
        public void LayoutsThatDoNotMatchTheMapAreNotUsed()
        {
            var tooSmall = new NetMapStructures(new[] { new StructureLayout(_onePieceHealth, _onePieceGrounded, Array.Empty<(int, int)>()) });
            var structures = new ServerStructures(Map(withLayouts: true, layouts: tooSmall), _log) { Enabled = true };

            Assert.That(structures.Count, Is.EqualTo(0));
            Assert.That(_log.ToString(), Does.Contain("does not match"));
        }

        [Test]
        public void NothingBreaksWhileDisabled()
        {
            var structures = new ServerStructures(Map(withLayouts: true), _log);

            Assert.That(structures.ApplyHit(10, Vector3.Zero, 1000, 0f, _changed, _log), Is.EqualTo(0));
            Assert.That(structures.IsDestroyed(0, 0), Is.False);
        }

        private static ServerMapData Map(bool withLayouts, NetMapStructures layouts = null)
        {
            var colliders = new[]
            {
                WorldCollider.Box(10, 0, MapColliderGroups.Piece(0, 0), new Vector3(0f, 0.5f, 0f), new Vector3(0.25f, 0.5f, 1f), Quaternion.Identity),
                WorldCollider.Box(11, 0, MapColliderGroups.Piece(0, 1), new Vector3(0f, 1.5f, 0f), new Vector3(0.25f, 0.5f, 1f), Quaternion.Identity),
            };
            layouts ??= withLayouts ? new NetMapStructures(new[] { new StructureLayout(_health, _grounded, _adjacency) }) : null;
            return new ServerMapData("Test", new WorldMap(colliders), TestMaps.NavMesh(), "memory", null, layouts);
        }
    }
}

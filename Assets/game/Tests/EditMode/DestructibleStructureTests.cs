using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 壊せる建物 (<see cref="DestructibleStructure"/>・<see cref="DestructiblePiece"/>) と破片 (<see cref="DebrisPool"/>) を確かめる。
    /// 部品は 1m の Cube を 2x2 に並べる。添字は下段が 0 (左)・1 (右)、上段が 2 (左)・3 (右) で、下段だけ地面に接する。
    /// 他のテストの物と重ならないよう、原点から離して置く。
    /// </summary>
    public sealed class DestructibleStructureTests
    {
        private static readonly Vector3 _origin = new Vector3(200f, 0f, 200f);

        private readonly List<GameObject> _created = new List<GameObject>();
        private DebrisPool _pool;

        [SetUp]
        public void SetUp()
        {
            var poolObject = new GameObject("TestDebrisPool");
            _created.Add(poolObject);
            _pool = poolObject.AddComponent<DebrisPool>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void DefaultsAreDisabledWithAuthority()
        {
            var structure = new GameObject("Defaults").AddComponent<DestructibleStructure>();
            _created.Add(structure.gameObject);

            Assert.That(structure.DestructionEnabled, Is.False);
            Assert.That(structure.HasAuthority, Is.True);
        }

        [Test]
        public void BuildMakesAdjacencyFromOverlappingColliders()
        {
            var structure = CreateGrid("Grid");
            DestructiblePiece.Create(structure.transform, "Far", new Vector3(5f, 0.5f, 0f), Vector3.one, 4, grounded: true);
            structure.Build();

            var layout = structure.Integrity.Layout;

            Assert.That(layout.PieceCount, Is.EqualTo(5));
            Assert.That(layout.IsGrounded(0), Is.True);
            Assert.That(layout.IsGrounded(2), Is.False);
            Assert.That(layout.Adjacency, Does.Contain((0, 1)));
            Assert.That(layout.Adjacency, Does.Contain((0, 2)));
            Assert.That(layout.Adjacency, Does.Contain((1, 3)));
            Assert.That(layout.Adjacency, Does.Contain((2, 3)));
            Assert.That(layout.Neighbors(4), Is.Empty, "a piece 4m away is not adjacent");
        }

        [Test]
        public void PiecesAreOrderedByIndex()
        {
            var structure = CreateGrid("Grid");

            for (var i = 0; i < structure.Pieces.Count; i++)
            {
                Assert.That(structure.Pieces[i].Index, Is.EqualTo(i));
            }
        }

        [Test]
        public void HitReducesHealthWithoutBreakingThePiece()
        {
            var structure = CreateGrid("Grid");
            var piece = structure.Pieces[0];
            var results = Record(structure);

            structure.ApplyStructureHit(piece.Collider, piece.transform.position, 30);

            Assert.That(structure.Integrity.Health(0), Is.EqualTo(70));
            Assert.That(structure.Integrity.IsDestroyed(0), Is.False);
            Assert.That(piece.Collider.enabled, Is.True);
            Assert.That(results, Is.Empty);
            Assert.That(_pool.Count, Is.Zero);
        }

        [Test]
        public void APieceWithMoreHealthTakesMoreToBreak()
        {
            var structure = CreateGrid("Grid");
            var sturdy = structure.Pieces[0];
            sturdy.Initialize(sturdy.Index, 250, sturdy.Grounded, sturdy.Collider);
            structure.Build();

            Hit(structure, 0, DestructiblePiece.DefaultMaxHealth);
            Hit(structure, 1, DestructiblePiece.DefaultMaxHealth);

            Assert.That(structure.Integrity.Health(0), Is.EqualTo(250 - DestructiblePiece.DefaultMaxHealth));
            Assert.That(structure.Integrity.IsDestroyed(0), Is.False, "the sturdier piece survives the hit that breaks a default one");
            Assert.That(structure.Integrity.IsDestroyed(1), Is.True);
        }

        [Test]
        public void BreakingAPieceDisablesItsColliderRendererAndObstacleAndSpawnsFragments()
        {
            var structure = CreateGrid("Grid");
            var piece = structure.Pieces[0];
            var results = Record(structure);

            structure.ApplyStructureHit(piece.Collider, piece.transform.position + Vector3.back, 100);

            Assert.That(structure.Integrity.IsDestroyed(0), Is.True);
            Assert.That(piece.IsBroken, Is.True);
            Assert.That(piece.Collider.enabled, Is.False);
            Assert.That(piece.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(piece.GetComponent<NavMeshObstacle>().enabled, Is.False);
            Assert.That(structure.Pieces[1].Collider.enabled, Is.True, "neighbors stay");
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Destroyed, Is.EqualTo(new[] { 0 }));
            Assert.That(results[0].Collapsed, Is.Empty, "the upper left piece still rests on the lower right one");
            Assert.That(_pool.Count, Is.EqualTo(DestructibleStructure.FragmentsPerDestroyedPiece));
        }

        [Test]
        public void FragmentsAreSmallVisualOnlyCubesWithRigidbodies()
        {
            var structure = CreateGrid("Grid");
            var piece = structure.Pieces[0];

            structure.ApplyStructureHit(piece.Collider, piece.transform.position, 100);

            var fragments = _pool.GetComponentsInChildren<Rigidbody>();
            Assert.That(fragments, Has.Length.EqualTo(DestructibleStructure.FragmentsPerDestroyedPiece));
            foreach (var fragment in fragments)
            {
                Assert.That(fragment.gameObject.layer, Is.EqualTo(DebrisPool.DebrisLayer));
                Assert.That(fragment.transform.localScale.x, Is.LessThanOrEqualTo(DestructibleStructure.MaxFragmentSize));
                Assert.That(fragment.GetComponent<DestructiblePiece>(), Is.Null);
            }
        }

        [Test]
        public void BreakingTheLowerRowCollapsesTheUpperRow()
        {
            var structure = CreateGrid("Grid");
            var results = Record(structure);

            Hit(structure, 0, 100);
            Hit(structure, 1, 100);

            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[1].Destroyed, Is.EqualTo(new[] { 1 }));
            Assert.That(results[1].Collapsed, Is.EqualTo(new[] { 2, 3 }));
            for (var i = 0; i < 4; i++)
            {
                Assert.That(structure.Integrity.IsDestroyed(i), Is.True, $"piece {i}");
                Assert.That(structure.Pieces[i].Collider.enabled, Is.False, $"piece {i}");
            }

            // 直接壊れた 2 つは小さな破片を、崩落した 2 つは自分の大きさの破片を 1 つずつ出す。
            Assert.That(_pool.Count, Is.EqualTo(2 * DestructibleStructure.FragmentsPerDestroyedPiece + 2));
            var whole = 0;
            foreach (Transform debris in _pool.transform)
            {
                if (debris.localScale == Vector3.one)
                {
                    whole++;
                }
            }

            Assert.That(whole, Is.EqualTo(2));
        }

        [Test]
        public void DestroyedLogNamesTheStructureAndPieces()
        {
            var structure = CreateGrid("Bunker");
            Hit(structure, 0, 100);

            LogAssert.Expect(LogType.Log, "[Destruction] structure=Bunker destroyed=1 collapsed=2,3");
            Hit(structure, 1, 100);

            Assert.That(
                DestructibleStructure.FormatDestroyedLog("Wall", new[] { 4 }, Array.Empty<int>()),
                Is.EqualTo("[Destruction] structure=Wall destroyed=4 collapsed=-"));
        }

        [Test]
        public void DisabledDestructionIgnoresHits()
        {
            var structure = CreateGrid("Grid");
            structure.DestructionEnabled = false;
            var results = Record(structure);

            Hit(structure, 0, 100);
            Hit(structure, 1, 100);

            AssertUntouched(structure, results);
        }

        [Test]
        public void WithoutAuthorityHitsAreIgnored()
        {
            var structure = CreateGrid("Grid");
            structure.HasAuthority = false;
            var results = Record(structure);

            Hit(structure, 0, 100);
            Hit(structure, 1, 100);

            AssertUntouched(structure, results);
        }

        [Test]
        public void HitsOnCollidersOutsideTheStructureAreIgnored()
        {
            var structure = CreateGrid("Grid");
            var other = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(other);
            var results = Record(structure);

            structure.ApplyStructureHit(other.GetComponent<Collider>(), Vector3.zero, 100);

            AssertUntouched(structure, results);
        }

        [Test]
        public void StructureDamageReachesThePieceThroughTheInterface()
        {
            var structure = CreateGrid("Grid");
            var piece = structure.Pieces[3];

            var count = StructureDamage.Apply(piece.Collider, piece.transform.position, 25, 0f);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(structure.Integrity.Health(3), Is.EqualTo(75));
        }

        [Test]
        public void ApplyDestroyedBitsetMirrorsTheStateWithoutAuthority()
        {
            var structure = CreateGrid("Grid");
            structure.HasAuthority = false;
            structure.DestructionEnabled = false;
            var results = Record(structure);

            structure.ApplyDestroyedBitset(new[] { 0b0101UL });

            Assert.That(structure.DestroyedBitset, Is.EqualTo(new[] { 0b0101UL }));
            Assert.That(structure.Pieces[0].Collider.enabled, Is.False);
            Assert.That(structure.Pieces[1].Collider.enabled, Is.True);
            Assert.That(structure.Pieces[2].Collider.enabled, Is.False, "no collapse is computed, the bits are applied as is");
            Assert.That(structure.Pieces[3].Collider.enabled, Is.True);
            Assert.That(structure.Pieces[2].GetComponent<NavMeshObstacle>().enabled, Is.False);
            Assert.That(results, Is.Empty, "PiecesDestroyed is only raised on the authority");
            Assert.That(_pool.Count, Is.EqualTo(2 * DestructibleStructure.FragmentsPerDestroyedPiece));

            structure.ApplyDestroyedBitset(new[] { 0b0001UL });

            Assert.That(structure.Integrity.IsDestroyed(2), Is.False);
            Assert.That(structure.Integrity.Health(2), Is.EqualTo(DestructiblePiece.DefaultMaxHealth));
            Assert.That(structure.Pieces[2].Collider.enabled, Is.True);
            Assert.That(structure.Pieces[2].GetComponent<Renderer>().enabled, Is.True);
            Assert.That(structure.Pieces[2].GetComponent<NavMeshObstacle>().enabled, Is.True);
            Assert.That(_pool.Count, Is.EqualTo(2 * DestructibleStructure.FragmentsPerDestroyedPiece), "no new debris");
        }

        [Test]
        public void DestroyedBitsetRoundTripsToAnotherStructure()
        {
            var server = CreateGrid("Server");
            var client = CreateGrid("Client", new Vector3(10f, 0f, 0f));
            client.HasAuthority = false;
            Hit(server, 0, 100);
            Hit(server, 1, 100);

            client.ApplyDestroyedBitset(server.DestroyedBitset);

            for (var i = 0; i < 4; i++)
            {
                Assert.That(client.Integrity.IsDestroyed(i), Is.EqualTo(server.Integrity.IsDestroyed(i)), $"piece {i}");
                Assert.That(client.Pieces[i].Collider.enabled, Is.False, $"piece {i}");
            }
        }

        [Test]
        public void ApplyDestroyedBitsetRejectsAMalformedBitsetAndKeepsTheState()
        {
            var structure = CreateGrid("Grid");
            Hit(structure, 0, 100);

            Assert.Throws<ArgumentNullException>(() => structure.ApplyDestroyedBitset(null));
            Assert.Throws<ArgumentException>(() => structure.ApplyDestroyedBitset(new ulong[2]));
            Assert.Throws<ArgumentException>(() => structure.ApplyDestroyedBitset(new[] { 1UL << 4 }));

            Assert.That(structure.DestroyedBitset, Is.EqualTo(new[] { 0b0001UL }));
            Assert.That(structure.Pieces[0].Collider.enabled, Is.False);
        }

        [Test]
        public void DebrisStaysWithinTheSceneWideCapacity()
        {
            _pool.Capacity = 6;
            var structure = CreateGrid("Grid");

            Hit(structure, 0, 100);
            var first = _pool.transform.GetChild(0).gameObject;
            Hit(structure, 1, 100);

            Assert.That(_pool.Count, Is.EqualTo(6));
            Assert.That(_pool.transform.childCount, Is.EqualTo(6));
            Assert.That(first == null, Is.True, "the oldest debris is removed first");
        }

        [Test]
        public void SpawnRemovesTheOldestDebrisWhenFull()
        {
            _pool.Capacity = 2;

            var a = Spawn();
            var b = Spawn();
            var c = Spawn();

            Assert.That(a == null, Is.True);
            Assert.That(b != null && c != null, Is.True);
            Assert.That(_pool.Count, Is.EqualTo(2));
        }

        [Test]
        public void LoweringTheCapacityTrimsTheOldestDebris()
        {
            var a = Spawn();
            var b = Spawn();
            var c = Spawn();

            _pool.Capacity = 1;

            Assert.That(a == null && b == null, Is.True);
            Assert.That(c != null, Is.True);
        }

        [Test]
        public void ZeroCapacitySpawnsNothing()
        {
            _pool.Capacity = 0;

            Assert.That(Spawn(), Is.Null);
            Assert.That(_pool.Count, Is.Zero);
        }

        [Test]
        public void DebrisDisappearsAfterItsLifetime()
        {
            _pool.Lifetime = 2f;
            var debris = Spawn();

            Assert.That(_pool.Tick(1.5f), Is.Zero);
            Assert.That(debris != null, Is.True);
            Assert.That(_pool.Tick(0.5f), Is.EqualTo(1));
            Assert.That(debris == null, Is.True);
            Assert.That(_pool.Count, Is.Zero);
        }

        [Test]
        public void DebrisRemovedFromOutsideIsNotCounted()
        {
            var debris = Spawn();
            Spawn();

            Object.DestroyImmediate(debris);

            Assert.That(_pool.Count, Is.EqualTo(1));
        }

        [Test]
        public void SpawnDebrisFalseBreaksPiecesWithoutDebris()
        {
            var structure = CreateGrid("Grid");
            structure.SpawnDebris = false;

            Hit(structure, 0, 100);

            Assert.That(structure.Pieces[0].Collider.enabled, Is.False);
            Assert.That(_pool.Count, Is.Zero);
        }

        [Test]
        public void PoolRejectsInvalidSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _pool.Capacity = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => _pool.Lifetime = 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _pool.Lifetime = float.PositiveInfinity);
            Assert.Throws<ArgumentOutOfRangeException>(() => _pool.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _pool.Spawn(Vector3.zero, Quaternion.identity, Vector3.zero, null, Vector3.zero));
        }

        [Test]
        public void DuplicateIndicesFailTheBuild()
        {
            var structure = CreateStructureRoot("Broken", _origin);
            DestructiblePiece.Create(structure.transform, "A", new Vector3(0f, 0.5f, 0f), Vector3.one, 0, grounded: true);
            DestructiblePiece.Create(structure.transform, "B", new Vector3(1f, 0.5f, 0f), Vector3.one, 0, grounded: true);

            Assert.Throws<InvalidOperationException>(() => structure.Build());
        }

        [Test]
        public void IndicesWithAGapFailTheBuild()
        {
            var structure = CreateStructureRoot("Broken", _origin);
            DestructiblePiece.Create(structure.transform, "A", new Vector3(0f, 0.5f, 0f), Vector3.one, 0, grounded: true);
            DestructiblePiece.Create(structure.transform, "B", new Vector3(1f, 0.5f, 0f), Vector3.one, 2, grounded: true);

            Assert.Throws<InvalidOperationException>(() => structure.Build());
        }

        [Test]
        public void HitsOnAMisconfiguredStructureLogAnErrorOnceAndDoNothing()
        {
            var structure = CreateStructureRoot("Empty", _origin);
            structure.DestructionEnabled = true;
            var stray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stray.transform.SetParent(structure.transform, false);

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Destruction\] DestructibleStructure 'Empty'"));
            structure.ApplyStructureHit(stray.GetComponent<Collider>(), Vector3.zero, 100);
            structure.ApplyStructureHit(stray.GetComponent<Collider>(), Vector3.zero, 100);

            LogAssert.NoUnexpectedReceived();
            Assert.That(_pool.Count, Is.Zero);
        }

        [Test]
        public void NegativeDamageIsRejected()
        {
            var structure = CreateGrid("Grid");

            Assert.Throws<ArgumentOutOfRangeException>(() => Hit(structure, 0, -1));
        }

        [Test]
        public void PieceRejectsInvalidSettings()
        {
            var root = new GameObject("Root");
            _created.Add(root);

            Assert.Throws<ArgumentNullException>(() => DestructiblePiece.Create(null, "P", Vector3.zero, Vector3.one, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => DestructiblePiece.Create(root.transform, "P", Vector3.zero, Vector3.zero, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => DestructiblePiece.Create(root.transform, "P", Vector3.zero, Vector3.one, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => DestructiblePiece.Create(root.transform, "P", Vector3.zero, Vector3.one, 0, 0));
            Assert.That(root.transform.childCount, Is.Zero, "failed pieces are cleaned up");
        }

        private DestructibleStructure CreateGrid(string name, Vector3 offset = default)
        {
            var structure = CreateStructureRoot(name, _origin + offset);
            DestructiblePiece.Create(structure.transform, "BottomLeft", new Vector3(-0.5f, 0.5f, 0f), Vector3.one, 0, grounded: true);
            DestructiblePiece.Create(structure.transform, "BottomRight", new Vector3(0.5f, 0.5f, 0f), Vector3.one, 1, grounded: true);
            DestructiblePiece.Create(structure.transform, "TopLeft", new Vector3(-0.5f, 1.5f, 0f), Vector3.one, 2);
            DestructiblePiece.Create(structure.transform, "TopRight", new Vector3(0.5f, 1.5f, 0f), Vector3.one, 3);
            structure.DestructionEnabled = true;
            structure.Build();
            return structure;
        }

        private DestructibleStructure CreateStructureRoot(string name, Vector3 position)
        {
            var root = new GameObject(name);
            _created.Add(root);
            root.transform.position = position;
            var structure = root.AddComponent<DestructibleStructure>();
            structure.DebrisPool = _pool;
            return structure;
        }

        private static void Hit(DestructibleStructure structure, int piece, int damage)
        {
            var target = structure.Pieces[piece];
            structure.ApplyStructureHit(target.Collider, target.transform.position, damage);
        }

        private static List<StructureDamageResult> Record(DestructibleStructure structure)
        {
            var results = new List<StructureDamageResult>();
            structure.PiecesDestroyed += results.Add;
            return results;
        }

        private void AssertUntouched(DestructibleStructure structure, List<StructureDamageResult> results)
        {
            for (var i = 0; i < 4; i++)
            {
                Assert.That(structure.Integrity.Health(i), Is.EqualTo(DestructiblePiece.DefaultMaxHealth), $"piece {i}");
                Assert.That(structure.Pieces[i].Collider.enabled, Is.True, $"piece {i}");
            }

            Assert.That(results, Is.Empty);
            Assert.That(_pool.Count, Is.Zero);
        }

        private GameObject Spawn()
        {
            return _pool.Spawn(_origin, Quaternion.identity, Vector3.one * 0.2f, null, Vector3.zero);
        }
    }
}

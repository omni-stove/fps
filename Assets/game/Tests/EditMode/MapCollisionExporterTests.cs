using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class MapCollisionExporterTests
    {
        private const int _propLayer = 9;
        private const int _probeCount = 4500;

        // 他のテストが残した物に光線が当たらないよう、原点から離れた所に組む
        private static readonly Vector3 _origin = new Vector3(700f, 50f, -700f);

        private GameObject _root;
        private Collider _trigger;
        private Collider _disabled;
        private Collider _inactive;
        private Collider _bodyCollider;
        private Collider[] _pieceColliders;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("MapCollisionExporterTests");
            _root.transform.position = _origin;

            Box("Floor", new Vector3(0f, -0.5f, 0f), Quaternion.identity, Vector3.one, new Vector3(24f, 1f, 24f), Vector3.zero);
            Box("TiltedBox", new Vector3(-4f, 1f, 3f), Quaternion.Euler(10f, 35f, 5f), new Vector3(2f, 1f, 3f), new Vector3(1f, 2f, 1f), new Vector3(0.2f, 0f, -0.1f));
            Box("MirroredBox", new Vector3(6f, 1f, -6f), Quaternion.Euler(0f, -20f, 0f), new Vector3(-1.5f, 2f, 1f), Vector3.one, Vector3.zero);

            var sphere = Primitive(PrimitiveType.Sphere, "Sphere", new Vector3(3f, 2f, 4f), Quaternion.Euler(0f, 30f, 0f), new Vector3(1f, 2f, 1.5f));
            var sphereCollider = sphere.GetComponent<SphereCollider>();
            sphereCollider.radius = 0.7f;
            sphereCollider.center = new Vector3(0.1f, 0.2f, 0f);

            for (var axis = 0; axis < 3; axis++)
            {
                var capsule = Primitive(
                    PrimitiveType.Capsule,
                    "Capsule" + axis,
                    new Vector3(-6f + (axis * 3f), 1.5f, -3f),
                    Quaternion.Euler(15f * axis, 40f, 20f),
                    new Vector3(1f + (0.3f * axis), 1.2f, 0.8f));
                var capsuleCollider = capsule.GetComponent<CapsuleCollider>();
                capsuleCollider.direction = axis;
                capsuleCollider.height = 2.5f;
                capsuleCollider.radius = 0.4f;
            }

            var cylinderMesh = MeshOf(PrimitiveType.Cylinder);
            var cylinder = new GameObject("MeshCylinder");
            Place(cylinder, new Vector3(0f, 1f, -6f), Quaternion.Euler(0f, 10f, 70f), new Vector3(1.5f, 1f, 2f));
            cylinder.AddComponent<MeshCollider>().sharedMesh = cylinderMesh;

            var mirroredSphere = new GameObject("MirroredMeshSphere");
            Place(mirroredSphere, new Vector3(-7f, 2f, 6f), Quaternion.Euler(0f, 25f, 0f), new Vector3(-2f, 1.5f, 2f));
            mirroredSphere.AddComponent<MeshCollider>().sharedMesh = MeshOf(PrimitiveType.Sphere);

            var ramp = Primitive(PrimitiveType.Plane, "OneSidedRamp", new Vector3(7f, 2f, 5f), Quaternion.Euler(25f, 0f, 0f), new Vector3(0.4f, 1f, 0.4f));
            Assert.That(ramp.GetComponent<MeshCollider>(), Is.Not.Null);

            var prop = Primitive(PrimitiveType.Cube, "Prop", new Vector3(1f, 0.5f, 7f), Quaternion.identity, Vector3.one);
            prop.layer = _propLayer;

            _trigger = Primitive(PrimitiveType.Cube, "Trigger", new Vector3(0f, 1f, 0f), Quaternion.identity, new Vector3(3f, 2f, 3f)).GetComponent<Collider>();
            _trigger.isTrigger = true;
            _disabled = Primitive(PrimitiveType.Cube, "Disabled", new Vector3(2f, 1f, -2f), Quaternion.identity, Vector3.one).GetComponent<Collider>();
            _disabled.enabled = false;
            var inactive = Primitive(PrimitiveType.Cube, "Inactive", new Vector3(-2f, 1f, 2f), Quaternion.identity, Vector3.one);
            _inactive = inactive.GetComponent<Collider>();
            inactive.SetActive(false);

            var body = Primitive(PrimitiveType.Cube, "Body", new Vector3(9f, 0.5f, 0f), Quaternion.identity, Vector3.one);
            body.AddComponent<Rigidbody>().isKinematic = true;
            _bodyCollider = body.GetComponent<Collider>();

            var building = new GameObject("Building");
            Place(building, new Vector3(-9f, 0f, -8f), Quaternion.identity, Vector3.one);
            building.AddComponent<DestructibleStructure>();
            _pieceColliders = new Collider[2];
            for (var i = 0; i < _pieceColliders.Length; i++)
            {
                var pieceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pieceObject.name = "Piece" + i;
                pieceObject.transform.SetParent(building.transform, false);
                pieceObject.transform.localPosition = new Vector3(i, 0.5f, 0f);
                var collider = pieceObject.GetComponent<Collider>();

                // 添字を並びと逆にして、番号が並び順でなく部品の添字から決まることを確かめる
                pieceObject.AddComponent<DestructiblePiece>().Initialize(_pieceColliders.Length - 1 - i, DestructiblePiece.DefaultMaxHealth, true, collider);
                _pieceColliders[i] = collider;
            }

            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void ReadBackMapRaycastsLikePhysicsForThousandsOfRays()
        {
            var sources = new List<Collider>();
            var map = ReadBack(MapCollisionExporter.Collect(new[] { _root }, sources));
            var world = new BvhWorldCollision(map);
            var probes = MapCollisionCheck.SampleProbes(MapCollisionCheck.BoundsOf(sources), _probeCount, 583);

            var everything = MapCollisionCheck.CompareRaycasts(world, probes, ~0, sources);
            Assert.That(everything, Is.Empty, string.Join("\n", everything.Take(20)));

            var withoutProps = MapCollisionCheck.CompareRaycasts(world, probes, ~(1 << _propLayer), sources);
            Assert.That(withoutProps, Is.Empty, string.Join("\n", withoutProps.Take(20)));

            var hits = probes.Count(p => Physics.Raycast(p.Origin, p.Direction, p.MaxDistance, ~0, QueryTriggerInteraction.Ignore));
            Assert.That(hits, Is.GreaterThan(_probeCount / 3), "the probes should mostly hit the map, not fly past it");
        }

        [Test]
        public void SkipsTriggersDisabledAndInactiveColliders()
        {
            var sources = new List<Collider>();
            var map = MapCollisionExporter.Collect(new[] { _root }, sources);

            Assert.That(sources, Has.No.Member(_trigger));
            Assert.That(sources, Has.No.Member(_disabled));
            Assert.That(sources, Has.No.Member(_inactive));
            Assert.That(map.Colliders.Count, Is.EqualTo(sources.Count));
            Assert.That(map.Colliders.Select(c => c.Id), Is.EqualTo(Enumerable.Range(0, sources.Count)));
        }

        [Test]
        public void PutsRigidbodiesAndDestructiblePiecesInTheirOwnGroups()
        {
            var sources = new List<Collider>();
            var map = MapCollisionExporter.Collect(new[] { _root }, sources);

            for (var i = 0; i < sources.Count; i++)
            {
                var group = map.Colliders[i].Group;
                if (sources[i] == _bodyCollider)
                {
                    Assert.That(group, Is.EqualTo(MapColliderGroups.Body(0)));
                }
                else if (sources[i] == _pieceColliders[0])
                {
                    Assert.That(group, Is.EqualTo(MapColliderGroups.Piece(0, 1)));
                }
                else if (sources[i] == _pieceColliders[1])
                {
                    Assert.That(group, Is.EqualTo(MapColliderGroups.Piece(0, 0)));
                }
                else
                {
                    Assert.That(group, Is.EqualTo(MapColliderGroups.Static), sources[i].name);
                }
            }

            var world = new BvhWorldCollision(map);
            var above = _pieceColliders[0].bounds.center + (Vector3.up * 3f);
            Assert.That(world.Raycast(ToNumerics(above), -System.Numerics.Vector3.UnitY, 5f, ~0, out var hit), Is.True);
            Assert.That(sources[hit.ColliderId], Is.EqualTo(_pieceColliders[0]));

            Assert.That(world.SetGroupEnabled(MapColliderGroups.Piece(0, 1), false), Is.True);
            Assert.That(world.Raycast(ToNumerics(above), -System.Numerics.Vector3.UnitY, 5f, ~0, out hit), Is.True);
            Assert.That(hit.Distance, Is.EqualTo(3.5f).Within(1e-3f), "a broken piece lets the ray through to the floor");
        }

        [Test]
        public void ThrowsOnCollidersItCannotExport()
        {
            var terrain = new GameObject("Terrain");
            terrain.transform.SetParent(_root.transform, false);
            terrain.AddComponent<TerrainCollider>();

            var error = Assert.Throws<InvalidOperationException>(() => MapCollisionExporter.Collect(new[] { _root }));
            Assert.That(error.Message, Does.Contain("MapCollisionExporterTests/Terrain (TerrainCollider)"));
        }

        [Test]
        public void WritesAFileThatReadsBackTheSameMapAndRemovesStaleOnes()
        {
            var directory = Path.Combine(Path.GetTempPath(), "MapCollisionExporterTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var map = MapCollisionExporter.Collect(new[] { _root });
                var path = Path.Combine(directory, "Arena" + MapCollisionExporter.FileExtension);
                var stale = Path.Combine(directory, "Gone" + MapCollisionExporter.FileExtension);
                var other = Path.Combine(directory, "notes.txt");
                File.WriteAllText(stale, "old");
                File.WriteAllText(other, "keep");

                MapCollisionExporter.WriteFile(path, map);
                var removed = MapCollisionExporter.RemoveStaleFiles(directory, new[] { path });

                Assert.That(Bytes(MapCollisionExporter.ReadFile(path)), Is.EqualTo(Bytes(map)));
                Assert.That(removed, Is.EqualTo(new[] { stale }));
                Assert.That(File.Exists(stale), Is.False);
                Assert.That(File.Exists(other), Is.True);
                Assert.That(File.Exists(path + ".tmp"), Is.False);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void ParsesTheScenesArgumentAndNamesMapsAfterTheirScenes()
        {
            Assert.That(MapCollisionExporter.ParseScenesArgument(new[] { "-batchmode" }), Is.Null);
            Assert.That(
                MapCollisionExporter.ParseScenesArgument(new[] { "-mapScenes", "Assets/A.unity, Assets/B.unity,", "-quit" }),
                Is.EqualTo(new[] { "Assets/A.unity", "Assets/B.unity" }));
            Assert.Throws<ArgumentException>(() => MapCollisionExporter.ParseScenesArgument(new[] { "-mapScenes" }));
            Assert.Throws<ArgumentException>(() => MapCollisionExporter.ParseScenesArgument(new[] { "-mapScenes", "-quit" }));

            Assert.That(MapCollisionExporter.MapName("Assets/Scenes/NetArena01.unity"), Is.EqualTo("NetArena01"));
            Assert.Throws<ArgumentException>(() => MapCollisionExporter.MapName("Assets/Scenes/NetArena01.prefab"));
        }

        private GameObject Box(string name, Vector3 position, Quaternion rotation, Vector3 scale, Vector3 size, Vector3 center)
        {
            var box = Primitive(PrimitiveType.Cube, name, position, rotation, scale);
            var collider = box.GetComponent<BoxCollider>();
            collider.size = size;
            collider.center = center;
            return box;
        }

        private GameObject Primitive(PrimitiveType type, string name, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            Place(primitive, position, rotation, scale);
            return primitive;
        }

        private void Place(GameObject gameObject, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            gameObject.transform.SetParent(_root.transform, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localRotation = rotation;
            gameObject.transform.localScale = scale;
        }

        private static Mesh MeshOf(PrimitiveType type)
        {
            var source = GameObject.CreatePrimitive(type);
            var mesh = source.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(source);
            return mesh;
        }

        private static WorldMap ReadBack(WorldMap map)
        {
            using (var stream = new MemoryStream(Bytes(map)))
            {
                return FpsMapFile.Read(stream);
            }
        }

        private static byte[] Bytes(WorldMap map)
        {
            using (var stream = new MemoryStream())
            {
                FpsMapFile.Write(stream, map);
                return stream.ToArray();
            }
        }

        private static System.Numerics.Vector3 ToNumerics(Vector3 v)
        {
            return new System.Numerics.Vector3(v.x, v.y, v.z);
        }
    }
}

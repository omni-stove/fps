using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 建造物への被害の配り方 (<see cref="StructureDamage"/>) と、<see cref="Hitscan.Fire"/> からの呼び出しを確かめる。
    /// Edit Mode でも Physics の問い合わせはコライダーを見つけるので、生成後に SyncTransforms してから呼ぶ。
    /// </summary>
    public sealed class StructureDamageTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);

        private readonly List<GameObject> _created = new List<GameObject>();

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
        public void ZeroRadiusPassesTheFullDamageToTheHitStructureOnce()
        {
            var structure = CreateStructure("Wall", new Vector3(0f, 0f, 10f));
            var collider = structure.GetComponent<Collider>();
            var point = new Vector3(0f, 0f, 9.5f);

            var count = StructureDamage.Apply(collider, point, 40, 0f);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(structure.Calls, Has.Count.EqualTo(1));
            Assert.That(structure.Calls[0].Collider, Is.SameAs(collider));
            Assert.That(structure.Calls[0].Point, Is.EqualTo(point));
            Assert.That(structure.Calls[0].Damage, Is.EqualTo(40));
        }

        [Test]
        public void ZeroRadiusDoesNotReachNearbyStructures()
        {
            var hit = CreateStructure("Hit", new Vector3(0f, 0f, 10f));
            var near = CreateStructure("Near", new Vector3(1.2f, 0f, 10f));

            StructureDamage.Apply(hit.GetComponent<Collider>(), new Vector3(0f, 0f, 9.5f), 40, 0f);

            Assert.That(hit.Calls, Has.Count.EqualTo(1));
            Assert.That(near.Calls, Is.Empty);
        }

        [Test]
        public void ZeroRadiusFindsTheStructureInTheParentsAndPassesThePart()
        {
            var root = new GameObject("Building");
            _created.Add(root);
            var structure = root.AddComponent<StructureHitRecorder>();
            var part = CreateCube("Pillar", new Vector3(0f, 0f, 10f), root.transform);

            var count = StructureDamage.Apply(part, new Vector3(0f, 0f, 9.5f), 40, 0f);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(structure.Calls, Has.Count.EqualTo(1));
            Assert.That(structure.Calls[0].Collider, Is.SameAs(part));
        }

        [Test]
        public void ZeroRadiusOnSomethingThatIsNotAStructureDoesNothing()
        {
            var wall = CreateCube("PlainWall", new Vector3(0f, 0f, 10f), null);

            Assert.That(StructureDamage.Apply(wall, new Vector3(0f, 0f, 9.5f), 40, 0f), Is.Zero);
        }

        [Test]
        public void RadiusReachesStructuresInsideAndScalesTheDamageByDistance()
        {
            // 中心は原点。各立方体 (1m) の原点に最も近い面までの距離は、center が 1m、edge が 2.5m、far が 9.5m。
            var center = CreateStructure("Center", new Vector3(1.5f, 0f, 0f));
            var edge = CreateStructure("Edge", new Vector3(3f, 0f, 0f));
            var far = CreateStructure("Far", new Vector3(10f, 0f, 0f));

            var count = StructureDamage.Apply(center.GetComponent<Collider>(), Vector3.zero, 40, 4f);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(center.Calls, Has.Count.EqualTo(1));
            Assert.That(center.Calls[0].Damage, Is.EqualTo(30), "1m of 4m leaves 75%");
            Assert.That(center.Calls[0].Point, Is.EqualTo(Vector3.zero));
            Assert.That(edge.Calls, Has.Count.EqualTo(1));
            Assert.That(edge.Calls[0].Damage, Is.EqualTo(15), "2.5m of 4m leaves 37.5%");
            Assert.That(far.Calls, Is.Empty, "outside the radius");
        }

        [Test]
        public void RadiusGivesTheFullDamageAtTheCenter()
        {
            var structure = CreateStructure("Wall", new Vector3(0f, 0f, 10f));
            var point = new Vector3(0f, 0f, 9.5f);

            StructureDamage.Apply(structure.GetComponent<Collider>(), point, 40, 3f);

            Assert.That(structure.Calls, Has.Count.EqualTo(1));
            Assert.That(structure.Calls[0].Damage, Is.EqualTo(40));
        }

        [Test]
        public void RadiusCallsTheStructureOncePerPart()
        {
            var root = new GameObject("Building");
            _created.Add(root);
            var structure = root.AddComponent<StructureHitRecorder>();
            var left = CreateCube("Left", new Vector3(-1f, 0f, 0f), root.transform);
            var right = CreateCube("Right", new Vector3(1f, 0f, 0f), root.transform);

            var count = StructureDamage.Apply(left, Vector3.zero, 40, 2f);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(structure.Calls, Has.Count.EqualTo(2));
            var parts = new[] { structure.Calls[0].Collider, structure.Calls[1].Collider };
            Assert.That(parts, Is.EquivalentTo(new Collider[] { left, right }));
        }

        [Test]
        public void RadiusIgnoresTriggersAndNonStructures()
        {
            var structure = CreateStructure("Wall", new Vector3(1f, 0f, 0f));
            var trigger = CreateStructure("Trigger", new Vector3(-1f, 0f, 0f));
            trigger.GetComponent<Collider>().isTrigger = true;
            CreateCube("Plain", new Vector3(0f, 0f, 1f), null);
            Physics.SyncTransforms();

            var count = StructureDamage.Apply(structure.GetComponent<Collider>(), Vector3.zero, 40, 3f);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(structure.Calls, Has.Count.EqualTo(1));
            Assert.That(trigger.Calls, Is.Empty);
        }

        [Test]
        public void ScaleIsFullAtTheCenterAndZeroAtTheEdge()
        {
            Assert.That(StructureDamage.Scale(40, 0f, 4f), Is.EqualTo(40));
            Assert.That(StructureDamage.Scale(40, 2f, 4f), Is.EqualTo(20));
            Assert.That(StructureDamage.Scale(40, 4f, 4f), Is.Zero);
            Assert.That(StructureDamage.Scale(40, 10f, 4f), Is.Zero);
            Assert.That(StructureDamage.Scale(0, 1f, 4f), Is.Zero);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var structure = CreateStructure("Wall", new Vector3(0f, 0f, 10f));
            var collider = structure.GetComponent<Collider>();

            Assert.That(
                Assert.Throws<ArgumentNullException>(() => StructureDamage.Apply(null, Vector3.zero, 1, 0f)).ParamName,
                Is.EqualTo("hitCollider"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => StructureDamage.Apply(collider, Vector3.zero, -1, 0f)).ParamName,
                Is.EqualTo("damage"));
            foreach (var radius in new[] { -0.1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.That(
                    Assert.Throws<ArgumentOutOfRangeException>(() => StructureDamage.Apply(collider, Vector3.zero, 1, radius)).ParamName,
                    Is.EqualTo("radius"),
                    radius.ToString());
            }

            Assert.That(structure.Calls, Is.Empty);
        }

        [Test]
        public void HitscanPassesTheBaseDamageToAStructureItHits()
        {
            var structure = CreateStructure("Wall", new Vector3(0f, 1.6f, 5f));

            var result = Hitscan.Fire(_eye, Vector3.forward, 50f, 10, DamageFalloff.None, null);

            Assert.That(result.Hit, Is.True);
            Assert.That(result.Hitbox, Is.Null);
            Assert.That(result.Damage, Is.Zero, "structure damage is not reported as player damage");
            Assert.That(structure.Calls, Has.Count.EqualTo(1));
            Assert.That(structure.Calls[0].Damage, Is.EqualTo(10));
            Assert.That(structure.Calls[0].Point.z, Is.EqualTo(4.5f).Within(1e-4f));
        }

        [Test]
        public void HitscanPassesTheExplosionRadius()
        {
            var hit = CreateStructure("Hit", new Vector3(0f, 1.6f, 5f));
            var near = CreateStructure("Near", new Vector3(1.5f, 1.6f, 5f));

            Hitscan.Fire(_eye, Vector3.forward, 50f, 10, DamageFalloff.None, null, 2f);

            Assert.That(hit.Calls, Has.Count.EqualTo(1));
            Assert.That(near.Calls, Has.Count.EqualTo(1));
            Assert.That(near.Calls[0].Damage, Is.LessThan(10));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void HitscanRejectsAnInvalidExplosionRadiusEvenOnAMiss(float explosionRadius)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(
                () => Hitscan.Fire(_eye, Vector3.back, 50f, 10, DamageFalloff.None, null, explosionRadius));
            Assert.That(e.ParamName, Is.EqualTo("explosionRadius"));
        }

        private StructureHitRecorder CreateStructure(string name, Vector3 position)
        {
            var collider = CreateCube(name, position, null);
            return collider.gameObject.AddComponent<StructureHitRecorder>();
        }

        private BoxCollider CreateCube(string name, Vector3 position, Transform parent)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (parent == null)
            {
                _created.Add(cube);
            }
            else
            {
                cube.transform.SetParent(parent, false);
            }

            cube.name = name;
            cube.transform.position = position;
            Physics.SyncTransforms();
            return cube.GetComponent<BoxCollider>();
        }
    }
}

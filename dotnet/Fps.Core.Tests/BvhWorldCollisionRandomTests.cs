using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class BvhWorldCollisionRandomTests
    {
        private const int _everything = ~0;
        private const int _shapeCount = 300;

        private static readonly int[] _triangleIndices = { 0, 1, 2 };

        private WorldMap _map;
        private BvhWorldCollision _world;
        private BvhWorldCollision[] _single;

        [SetUp]
        public void SetUp()
        {
            var random = new Random(578);
            var colliders = new List<WorldCollider>();
            for (var i = 0; i < _shapeCount; i++)
            {
                var center = RandomPoint(random, 20f);
                var layer = random.Next(4);
                switch (i % 4)
                {
                    case 0:
                        colliders.Add(WorldCollider.Mesh(
                            i, layer, 0,
                            new[] { center + RandomPoint(random, 1.5f), center + RandomPoint(random, 1.5f), center + RandomPoint(random, 1.5f) },
                            _triangleIndices));
                        break;
                    case 1:
                        colliders.Add(WorldCollider.Box(
                            i, layer, 0, center, new Vector3(0.2f) + Vector3.Abs(RandomPoint(random, 1f)), RandomRotation(random)));
                        break;
                    case 2:
                        colliders.Add(WorldCollider.Capsule(i, layer, 0, center, center + RandomPoint(random, 1.5f), 0.2f + ((float)random.NextDouble() * 0.6f)));
                        break;
                    default:
                        colliders.Add(WorldCollider.Sphere(i, layer, 0, center, 0.2f + ((float)random.NextDouble() * 0.8f)));
                        break;
                }
            }

            _map = new WorldMap(colliders);
            _world = new BvhWorldCollision(_map);
            _single = new BvhWorldCollision[colliders.Count];
            for (var i = 0; i < colliders.Count; i++)
            {
                _single[i] = new BvhWorldCollision(new WorldMap(new[] { colliders[i] }));
            }
        }

        [Test]
        public void RaycastFindsTheSameNearestHitAsTestingEveryColliderAlone()
        {
            var random = new Random(1);
            var hits = 0;
            for (var n = 0; n < 2000; n++)
            {
                var origin = RandomPoint(random, 25f);
                var direction = RandomPoint(random, 20f) - origin;
                var mask = random.Next(2) == 0 ? _everything : 1 << random.Next(4);
                var found = _world.Raycast(origin, direction, 30f, mask, out var hit);

                var expectedFound = false;
                var expected = default(WorldHit);
                foreach (var single in _single)
                {
                    if (single.Raycast(origin, direction, 30f, mask, out var candidate) && (!expectedFound || candidate.Distance < expected.Distance))
                    {
                        expectedFound = true;
                        expected = candidate;
                    }
                }

                Assert.That(found, Is.EqualTo(expectedFound), "ray " + n);
                if (found)
                {
                    hits++;
                    Assert.That(hit.Distance, Is.EqualTo(expected.Distance).Within(1e-4f), "ray " + n);
                    Assert.That(Vector3.Distance(hit.Point, origin + (Vector3.Normalize(direction) * hit.Distance)), Is.LessThan(1e-3f));
                    Assert.That(hit.Normal.Length(), Is.EqualTo(1f).Within(1e-4f));
                    Assert.That(Vector3.Dot(hit.Normal, direction), Is.LessThanOrEqualTo(1e-4f), "a hit surface faces the ray");
                    Assert.That((mask & (1 << hit.Layer)) != 0, Is.True);
                }
            }

            Assert.That(hits, Is.GreaterThan(100), "the scene is dense enough to test hits");
        }

        [Test]
        public void OverlapFindsTheSameCollidersAsTestingEveryColliderAlone()
        {
            var random = new Random(2);
            var results = new int[_shapeCount];
            var single = new int[1];
            for (var n = 0; n < 500; n++)
            {
                var point0 = RandomPoint(random, 22f);
                var point1 = point0 + RandomPoint(random, 2f);
                var radius = 0.1f + ((float)random.NextDouble() * 1.5f);
                var count = _world.OverlapCapsule(point0, point1, radius, _everything, results);

                var expected = new List<int>();
                for (var i = 0; i < _single.Length; i++)
                {
                    if (_single[i].OverlapCapsule(point0, point1, radius, _everything, single) == 1)
                    {
                        expected.Add(single[0]);
                    }
                }

                Assert.That(results.AsSpan(0, count).ToArray(), Is.EquivalentTo(expected), "capsule " + n);
            }
        }

        [Test]
        public void CapsuleCastStopsWhereTheCapsuleFirstTouches()
        {
            var random = new Random(3);
            var results = new int[_shapeCount];
            var hits = 0;
            for (var n = 0; n < 600; n++)
            {
                var point0 = RandomPoint(random, 22f);
                var point1 = point0 + RandomPoint(random, 1.5f);
                var radius = 0.1f + ((float)random.NextDouble() * 0.6f);
                var direction = Vector3.Normalize(RandomPoint(random, 20f) - point0);
                const float maxDistance = 30f;
                if (_world.OverlapCapsule(point0, point1, radius, _everything, results) > 0)
                {
                    continue;
                }

                var found = _world.CapsuleCast(point0, point1, radius, direction, maxDistance, _everything, out var hit);
                var travel = found ? hit.Distance : maxDistance;

                // 止まった所の少し手前までは何にも触れない。三角形は裏から近づくと当たらないので、重なりの確かめから外す
                for (var step = 0; step <= 40; step++)
                {
                    var offset = direction * (Math.Max(0f, travel - 2e-3f) * step / 40f);
                    var count = _world.OverlapCapsule(point0 + offset, point1 + offset, radius, _everything, results);
                    for (var i = 0; i < count; i++)
                    {
                        Assert.That(IsBackfacingTriangle(results[i], direction), Is.True, "capsule " + n + " passed through " + results[i]);
                    }
                }

                if (found)
                {
                    hits++;
                    var offset = direction * hit.Distance;
                    var count = _world.OverlapCapsule(point0 + offset, point1 + offset, radius + 2e-3f, _everything, results);
                    Assert.That(results.AsSpan(0, count).ToArray(), Does.Contain(hit.ColliderId), "capsule " + n + " touches what it hit");
                    Assert.That(hit.Normal.Length(), Is.EqualTo(1f).Within(1e-4f));
                    Assert.That(Vector3.Dot(hit.Normal, direction), Is.LessThanOrEqualTo(1e-4f), "capsule " + n + " normal opposes the motion");
                    var axisPoint = WorldClosestOnSegment(hit.Point, point0 + offset, point1 + offset);
                    Assert.That(Vector3.Distance(axisPoint, hit.Point), Is.EqualTo(radius).Within(2e-3f), "capsule " + n + " point on the surface");
                }
            }

            Assert.That(hits, Is.GreaterThan(100), "the scene is dense enough to test hits");
        }

        private bool IsBackfacingTriangle(int id, Vector3 direction)
        {
            var collider = _map.Colliders[id];
            if (collider.Kind != WorldShapeKind.Mesh)
            {
                return false;
            }

            var a = collider.Vertices[0];
            var normal = Vector3.Cross(collider.Vertices[1] - a, collider.Vertices[2] - a);
            return Vector3.Dot(normal, direction) > 0f;
        }

        private static Vector3 WorldClosestOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            var s = Math.Clamp(Vector3.Dot(point - a, ab) / Math.Max(Vector3.Dot(ab, ab), 1e-12f), 0f, 1f);
            return a + (ab * s);
        }

        private static Vector3 RandomPoint(Random random, float extent)
        {
            return new Vector3(
                (float)((random.NextDouble() * 2.0) - 1.0) * extent,
                (float)((random.NextDouble() * 2.0) - 1.0) * extent,
                (float)((random.NextDouble() * 2.0) - 1.0) * extent);
        }

        private static Quaternion RandomRotation(Random random)
        {
            var axis = Vector3.Normalize(RandomPoint(random, 1f) + new Vector3(1e-3f));
            return Quaternion.CreateFromAxisAngle(axis, (float)(random.NextDouble() * Math.PI * 2.0));
        }
    }
}

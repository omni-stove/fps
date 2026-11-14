using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class BvhWorldCollisionTests
    {
        private const int _everything = ~0;
        private const int _defaultLayer = 0;
        private const int _propLayer = 8;
        private const int _poleLayer = 9;

        private const int _floorId = 1;
        private const int _wallId = 2;
        private const int _stepId = 3;
        private const int _slopeId = 4;
        private const int _propId = 5;
        private const int _poleId = 6;
        private const int _crateId = 7;
        private const int _crateGroup = 1;

        private const float _tolerance = 1e-4f;

        private static readonly int[] _floorIndices = { 0, 1, 2, 0, 2, 3 };
        private static readonly int[] _slopeIndices = { 0, 2, 1, 0, 3, 2 };

        private static readonly Vector3[] _roomVertices =
        {
            new Vector3(-5f, 0f, -5f), new Vector3(-5f, 0f, 5f), new Vector3(5f, 0f, 5f), new Vector3(5f, 0f, -5f),
            new Vector3(3f, 0f, -5f), new Vector3(3f, 0f, 5f), new Vector3(3f, 3f, 5f), new Vector3(3f, 3f, -5f),
        };

        private static readonly int[] _roomIndices = { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };

        private static readonly float _slopeTop = 5f * (float)Math.Tan(Math.PI / 6.0);
        private static readonly Vector3 _slopeNormal = new Vector3(0.5f, (float)Math.Sqrt(3.0) / 2f, 0f);

        private BvhWorldCollision _world;

        [SetUp]
        public void SetUp()
        {
            _world = new BvhWorldCollision(BuildWorld());
        }

        [Test]
        public void RayDownHitsFloorWithUpNormal()
        {
            Assert.That(_world.Raycast(new Vector3(1f, 2f, -1f), new Vector3(0f, -3f, 0f), 10f, _everything, out var hit), Is.True);
            Assert.That(hit.ColliderId, Is.EqualTo(_floorId));
            Assert.That(hit.Layer, Is.EqualTo(_defaultLayer));
            Assert.That(hit.Distance, Is.EqualTo(2f).Within(_tolerance));
            AssertVector(hit.Point, new Vector3(1f, 0f, -1f));
            AssertVector(hit.Normal, Vector3.UnitY);
        }

        [Test]
        public void RaycastColliderOnlyHitsThatCollider()
        {
            var origin = new Vector3(5f, 5f, 0f);
            var down = new Vector3(0f, -1f, 0f);

            Assert.That(_world.RaycastCollider(_floorId, origin, down, 10f, out var floor), Is.True);
            Assert.That(floor.ColliderId, Is.EqualTo(_floorId));
            Assert.That(floor.Distance, Is.EqualTo(5f).Within(_tolerance));

            Assert.That(_world.RaycastCollider(_wallId, origin, down, 10f, out var wall), Is.True);
            Assert.That(wall.ColliderId, Is.EqualTo(_wallId));
            AssertVector(wall.Point, new Vector3(5f, 3f, 0f));
            AssertVector(wall.Normal, Vector3.UnitY);

            Assert.That(_world.RaycastCollider(_propId, origin, down, 10f, out _), Is.False);
            Assert.That(_world.RaycastCollider(99, origin, down, 10f, out _), Is.False);
        }

        [Test]
        public void RaycastColliderFromInsideABoxMissesIt()
        {
            Assert.That(_world.RaycastCollider(_wallId, new Vector3(5f, 1f, 0f), Vector3.UnitY, 10f, out _), Is.False);
        }

        [Test]
        public void RaycastColliderSkipsADisabledGroup()
        {
            var origin = new Vector3(-3f, 5f, -8f);
            Assert.That(_world.RaycastCollider(_crateId, origin, -Vector3.UnitY, 10f, out _), Is.True);

            _world.SetGroupEnabled(_crateGroup, false);

            Assert.That(_world.RaycastCollider(_crateId, origin, -Vector3.UnitY, 10f, out _), Is.False);
        }

        [Test]
        public void RayShorterThanTheFloorMisses()
        {
            Assert.That(_world.Raycast(new Vector3(1f, 2f, -1f), -Vector3.UnitY, 1.9f, _everything, out var hit), Is.False);
            Assert.That(hit, Is.EqualTo(default(WorldHit)));
        }

        [Test]
        public void RayFromBelowPassesThroughTheFloorBackface()
        {
            Assert.That(_world.Raycast(new Vector3(1f, -1f, -1f), Vector3.UnitY, 10f, _everything, out _), Is.False);
        }

        [Test]
        public void RayHitsWallFaceWithNormalTowardTheRay()
        {
            Assert.That(_world.Raycast(new Vector3(0f, 1f, 0f), Vector3.UnitX, 100f, _everything, out var hit), Is.True);
            Assert.That(hit.ColliderId, Is.EqualTo(_wallId));
            Assert.That(hit.Distance, Is.EqualTo(4.75f).Within(_tolerance));
            AssertVector(hit.Normal, -Vector3.UnitX);
        }

        [Test]
        public void RayReturnsTheNearestOfTwoColliders()
        {
            Assert.That(_world.Raycast(new Vector3(6f, 1f, -5f), -Vector3.UnitX, 100f, _everything, out var hit), Is.True);
            Assert.That(hit.ColliderId, Is.EqualTo(_poleId));
            Assert.That(hit.Layer, Is.EqualTo(_poleLayer));
            Assert.That(hit.Distance, Is.EqualTo(2.7f).Within(_tolerance));
            AssertVector(hit.Point, new Vector3(3.3f, 1f, -5f));
            AssertVector(hit.Normal, Vector3.UnitX);
        }

        [Test]
        public void RayStartingInsideTheWallIgnoresIt()
        {
            Assert.That(_world.Raycast(new Vector3(5f, 1f, 0f), Vector3.UnitX, 100f, 1 << _defaultLayer, out _), Is.False);
        }

        [Test]
        public void RayHitsTheStepTopAndSide()
        {
            Assert.That(_world.Raycast(new Vector3(0.5f, 1f, 5f), -Vector3.UnitY, 10f, _everything, out var top), Is.True);
            Assert.That(top.ColliderId, Is.EqualTo(_stepId));
            Assert.That(top.Distance, Is.EqualTo(0.7f).Within(_tolerance));
            AssertVector(top.Normal, Vector3.UnitY);

            Assert.That(_world.Raycast(new Vector3(0.5f, 0.1f, 2f), Vector3.UnitZ, 10f, _everything, out var side), Is.True);
            Assert.That(side.ColliderId, Is.EqualTo(_stepId));
            Assert.That(side.Distance, Is.EqualTo(2f).Within(_tolerance));
            AssertVector(side.Normal, -Vector3.UnitZ);
        }

        [Test]
        public void RayOnTheSlopeReturnsTheSlopeNormal()
        {
            Assert.That(_world.Raycast(new Vector3(-7.5f, 10f, 0f), -Vector3.UnitY, 20f, _everything, out var hit), Is.True);
            Assert.That(hit.ColliderId, Is.EqualTo(_slopeId));
            Assert.That(hit.Point.Y, Is.EqualTo(_slopeTop / 2f).Within(_tolerance));
            AssertVector(hit.Normal, _slopeNormal);
        }

        [Test]
        public void RayExcludesLayersOutsideTheMask()
        {
            var origin = new Vector3(0f, 1f, -2f);
            Assert.That(_world.Raycast(origin, -Vector3.UnitZ, 10f, _everything, out var prop), Is.True);
            Assert.That(prop.ColliderId, Is.EqualTo(_propId));
            Assert.That(prop.Layer, Is.EqualTo(_propLayer));
            Assert.That(prop.Distance, Is.EqualTo(2.5f).Within(_tolerance));
            AssertVector(prop.Normal, Vector3.UnitZ);

            Assert.That(_world.Raycast(origin, -Vector3.UnitZ, 10f, ~(1 << _propLayer), out _), Is.False);
        }

        [Test]
        public void RayHitsTheCapsuleSideWithARadialNormal()
        {
            Assert.That(_world.Raycast(new Vector3(0f, 1f, -5f), Vector3.UnitX, 10f, 1 << _poleLayer, out var hit), Is.True);
            Assert.That(hit.ColliderId, Is.EqualTo(_poleId));
            Assert.That(hit.Distance, Is.EqualTo(2.7f).Within(_tolerance));
            AssertVector(hit.Normal, -Vector3.UnitX);
        }

        [Test]
        public void RayWithZeroDirectionOrDistanceMisses()
        {
            Assert.That(_world.Raycast(new Vector3(0f, 1f, 0f), Vector3.Zero, 10f, _everything, out _), Is.False);
            Assert.That(_world.Raycast(new Vector3(0f, 1f, 0f), -Vector3.UnitY, 0f, _everything, out _), Is.False);
            Assert.That(_world.Raycast(new Vector3(0f, 1f, 0f), -Vector3.UnitY, float.NaN, _everything, out _), Is.False);
            Assert.That(_world.Raycast(new Vector3(0f, 1f, 0f), -Vector3.UnitY, float.PositiveInfinity, _everything, out _), Is.True);
        }

        [Test]
        public void DisabledGroupIsNotHitUntilEnabledAgain()
        {
            var origin = new Vector3(-3f, 1f, -6f);
            Assert.That(_world.Raycast(origin, -Vector3.UnitZ, 10f, _everything, out var before), Is.True);
            Assert.That(before.ColliderId, Is.EqualTo(_crateId));

            Assert.That(_world.SetGroupEnabled(_crateGroup, false), Is.True);
            Assert.That(_world.IsGroupEnabled(_crateGroup), Is.False);
            Assert.That(_world.Raycast(origin, -Vector3.UnitZ, 10f, _everything, out _), Is.False);
            Assert.That(_world.OverlapCapsule(new Vector3(-3f, 1f, -8f), new Vector3(-3f, 1.5f, -8f), 0.1f, _everything, new int[4]), Is.EqualTo(0));

            _world.SetGroupEnabled(_crateGroup, true);
            Assert.That(_world.Raycast(origin, -Vector3.UnitZ, 10f, _everything, out _), Is.True);
            Assert.That(_world.SetGroupEnabled(99, false), Is.False);
        }

        [Test]
        public void CapsuleAboveTheFloorOverlapsNothing()
        {
            var results = new int[8];
            Assert.That(_world.OverlapCapsule(new Vector3(0f, 0.51f, 0f), new Vector3(0f, 1.5f, 0f), 0.5f, _everything, results), Is.EqualTo(0));
        }

        [Test]
        public void CapsuleSunkIntoTheFloorReportsTheFloorOnce()
        {
            // 床の 2 枚の三角形が接する対角線の上に置き、両方に重ねる
            var results = new int[8];
            var count = _world.OverlapCapsule(new Vector3(0f, 0.4f, 0f), new Vector3(0f, 1.5f, 0f), 0.5f, _everything, results);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(results[0], Is.EqualTo(_floorId));
        }

        [Test]
        public void CapsuleAgainstTheWallReportsWallAndFloor()
        {
            var results = new int[8];
            var count = _world.OverlapCapsule(new Vector3(4.4f, 0.4f, 0f), new Vector3(4.4f, 1.5f, 0f), 0.5f, _everything, results);
            Assert.That(results.AsSpan(0, count).ToArray(), Is.EquivalentTo(new[] { _floorId, _wallId }));

            count = _world.OverlapCapsule(new Vector3(4.4f, 0.4f, 0f), new Vector3(4.4f, 1.5f, 0f), 0.5f, _everything, new int[1]);
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void CapsuleOverlapExcludesLayersOutsideTheMask()
        {
            var results = new int[8];
            var point0 = new Vector3(0f, 1f, -4.2f);
            var point1 = new Vector3(0f, 2f, -4.2f);
            Assert.That(_world.OverlapCapsule(point0, point1, 0.4f, _everything, results), Is.EqualTo(1));
            Assert.That(results[0], Is.EqualTo(_propId));
            Assert.That(_world.OverlapCapsule(point0, point1, 0.4f, ~(1 << _propLayer), results), Is.EqualTo(0));
        }

        [Test]
        public void CapsuleOverlapRejectsANullBuffer()
        {
            Assert.Throws<ArgumentNullException>(() => _world.OverlapCapsule(Vector3.Zero, Vector3.UnitY, 1f, _everything, null));
        }

        [Test]
        public void CapsuleFallingLandsOnTheFloor()
        {
            Assert.That(_world.CapsuleCast(
                new Vector3(1f, 2.5f, -1f), new Vector3(1f, 3.5f, -1f), 0.5f, -Vector3.UnitY, 10f, _everything, out var hit), Is.True);
            Assert.That(hit.ColliderId, Is.EqualTo(_floorId));
            Assert.That(hit.Distance, Is.EqualTo(2f).Within(_tolerance));
            AssertVector(hit.Normal, Vector3.UnitY);
            AssertVector(hit.Point, new Vector3(1f, 0f, -1f));
        }

        [Test]
        public void CapsuleWalkingIntoTheWallStopsAtItsFace()
        {
            Assert.That(_world.CapsuleCast(
                new Vector3(0f, 0.6f, 0f), new Vector3(0f, 1.5f, 0f), 0.5f, Vector3.UnitX, 10f, _everything, out var hit), Is.True);
            Assert.That(hit.ColliderId, Is.EqualTo(_wallId));
            Assert.That(hit.Distance, Is.EqualTo(4.25f).Within(_tolerance));
            AssertVector(hit.Normal, -Vector3.UnitX);
            Assert.That(hit.Point.X, Is.EqualTo(4.75f).Within(_tolerance));
        }

        [Test]
        public void CapsuleWalkingIntoTheStepTouchesItsTopEdge()
        {
            // 足の球の下端 (0.05) は段の上面 (0.3) より低いので、段の手前の上の角に球が触れる
            Assert.That(_world.CapsuleCast(
                new Vector3(0.5f, 0.55f, 2f), new Vector3(0.5f, 1.5f, 2f), 0.5f, Vector3.UnitZ, 10f, _everything, out var hit), Is.True);
            Assert.That(hit.ColliderId, Is.EqualTo(_stepId));
            var dy = 0.55f - 0.3f;
            var expected = 4f - 2f - (float)Math.Sqrt((0.5f * 0.5f) - (dy * dy));
            Assert.That(hit.Distance, Is.EqualTo(expected).Within(_tolerance));
            AssertVector(hit.Point, new Vector3(0.5f, 0.3f, 4f));
            var toCenter = Vector3.Normalize(new Vector3(0f, dy, -(float)Math.Sqrt((0.5f * 0.5f) - (dy * dy))));
            AssertVector(hit.Normal, toCenter);
        }

        [Test]
        public void CapsuleLowerThanTheStepHitsItsSide()
        {
            Assert.That(_world.CapsuleCast(
                new Vector3(0.5f, 0.15f, 2f), new Vector3(0.5f, 0.15f, 2f), 0.1f, Vector3.UnitZ, 10f, _everything, out var hit), Is.True);
            Assert.That(hit.ColliderId, Is.EqualTo(_stepId));
            Assert.That(hit.Distance, Is.EqualTo(1.9f).Within(_tolerance));
            AssertVector(hit.Normal, -Vector3.UnitZ);
        }

        [Test]
        public void CapsuleDroppedOnTheSlopeGetsTheSlopeNormal()
        {
            var bottom = new Vector3(-7.5f, 5f, 0f);
            Assert.That(_world.CapsuleCast(bottom, bottom + Vector3.UnitY, 0.5f, -Vector3.UnitY, 20f, _everything, out var hit), Is.True);
            Assert.That(hit.ColliderId, Is.EqualTo(_slopeId));
            AssertVector(hit.Normal, _slopeNormal);
            var center = bottom - (Vector3.UnitY * hit.Distance);
            var surfaceUnderCenter = new Vector3(-5f, 0f, 0f);
            Assert.That(Vector3.Dot(center - surfaceUnderCenter, _slopeNormal), Is.EqualTo(0.5f).Within(_tolerance));
            AssertVector(hit.Point, center - (_slopeNormal * 0.5f));
        }

        [Test]
        public void CapsuleCastIgnoresWhatItAlreadyOverlaps()
        {
            // 床にめり込んだまま横へ動かすと、床ではなく壁に当たる
            Assert.That(_world.CapsuleCast(
                new Vector3(0f, 0.3f, 0f), new Vector3(0f, 1.5f, 0f), 0.5f, Vector3.UnitX, 10f, _everything, out var hit), Is.True);
            Assert.That(hit.ColliderId, Is.EqualTo(_wallId));
        }

        [Test]
        public void CapsuleCastIgnoresEveryTriangleOfAMeshItAlreadyOverlaps()
        {
            var room = new BvhWorldCollision(new WorldMap(new[]
            {
                WorldCollider.Mesh(1, _defaultLayer, WorldCollider.StaticGroup, _roomVertices, _roomIndices),
            }));
            var top = new Vector3(0f, 1.5f, 0f);

            Assert.That(room.CapsuleCast(new Vector3(0f, 0.3f, 0f), top, 0.5f, Vector3.UnitX, 10f, _everything, out _), Is.False);

            Assert.That(room.CapsuleCast(new Vector3(0f, 0.6f, 0f), top, 0.5f, Vector3.UnitX, 10f, _everything, out var hit), Is.True);
            Assert.That(hit.Distance, Is.EqualTo(2.5f).Within(_tolerance));
            AssertVector(hit.Normal, -Vector3.UnitX);
        }

        [Test]
        public void CapsuleCastIgnoresManyCollidersItAlreadyOverlaps()
        {
            var colliders = new List<WorldCollider>();
            for (var i = 0; i < 40; i++)
            {
                colliders.Add(WorldCollider.Sphere(i, _defaultLayer, WorldCollider.StaticGroup, new Vector3(0f, 1f, i * 0.001f), 0.1f));
            }

            colliders.Add(WorldCollider.Box(100, _defaultLayer, WorldCollider.StaticGroup, new Vector3(3f, 1f, 0f), new Vector3(0.5f), Quaternion.Identity));
            var world = new BvhWorldCollision(new WorldMap(colliders));

            Assert.That(world.CapsuleCast(new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 0f), 0.3f, Vector3.UnitX, 10f, _everything, out var hit), Is.True);
            Assert.That(hit.ColliderId, Is.EqualTo(100));
            Assert.That(hit.Distance, Is.EqualTo(2.2f).Within(_tolerance));
        }

        [Test]
        public void CapsuleCastExcludesLayersOutsideTheMask()
        {
            var point0 = new Vector3(0f, 1f, 0f);
            var point1 = new Vector3(0f, 1.2f, 0f);
            Assert.That(_world.CapsuleCast(point0, point1, 0.3f, -Vector3.UnitZ, 10f, _everything, out var prop), Is.True);
            Assert.That(prop.ColliderId, Is.EqualTo(_propId));
            Assert.That(prop.Distance, Is.EqualTo(5f - 0.5f - 0.3f).Within(_tolerance));
            AssertVector(prop.Normal, Vector3.UnitZ);

            Assert.That(_world.CapsuleCast(point0, point1, 0.3f, -Vector3.UnitZ, 10f, ~(1 << _propLayer), out _), Is.False);
        }

        [Test]
        public void CapsuleCastAgainstACapsuleStopsAtTheSumOfRadii()
        {
            Assert.That(_world.CapsuleCast(
                new Vector3(0f, 1f, -5f), new Vector3(0f, 1.2f, -5f), 0.2f, Vector3.UnitX, 10f, 1 << _poleLayer, out var hit), Is.True);
            Assert.That(hit.ColliderId, Is.EqualTo(_poleId));
            Assert.That(hit.Distance, Is.EqualTo(3f - 0.3f - 0.2f).Within(_tolerance));
            AssertVector(hit.Normal, -Vector3.UnitX);
            AssertVector(hit.Point, new Vector3(2.7f, 1f, -5f));
        }

        [Test]
        public void CapsuleCastWithZeroRadiusDirectionOrDistanceMisses()
        {
            var point0 = new Vector3(0f, 2f, 0f);
            var point1 = new Vector3(0f, 3f, 0f);
            Assert.That(_world.CapsuleCast(point0, point1, 0f, -Vector3.UnitY, 10f, _everything, out _), Is.False);
            Assert.That(_world.CapsuleCast(point0, point1, 0.5f, Vector3.Zero, 10f, _everything, out _), Is.False);
            Assert.That(_world.CapsuleCast(point0, point1, 0.5f, -Vector3.UnitY, 0f, _everything, out _), Is.False);
        }

        [Test]
        public void EmptyWorldHitsNothing()
        {
            var world = new BvhWorldCollision(new WorldMap(Array.Empty<WorldCollider>()));
            Assert.That(world.Raycast(Vector3.Zero, Vector3.UnitX, 10f, _everything, out _), Is.False);
            Assert.That(world.OverlapCapsule(Vector3.Zero, Vector3.UnitY, 1f, _everything, new int[2]), Is.EqualTo(0));
            Assert.That(world.CapsuleCast(Vector3.Zero, Vector3.UnitY, 1f, Vector3.UnitX, 10f, _everything, out _), Is.False);
        }

        [Test]
        public void QueriesDoNotAllocate()
        {
            var results = new int[8];
            RunQueries(results);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                RunQueries(results);
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        private void RunQueries(int[] results)
        {
            _world.Raycast(new Vector3(0f, 1f, 0f), Vector3.UnitX, 100f, _everything, out _);
            _world.OverlapCapsule(new Vector3(4.4f, 0.4f, 0f), new Vector3(4.4f, 1.5f, 0f), 0.5f, _everything, results);
            _world.CapsuleCast(new Vector3(0.5f, 0.55f, 2f), new Vector3(0.5f, 1.5f, 2f), 0.5f, Vector3.UnitZ, 10f, _everything, out _);
            _world.SetGroupEnabled(_crateGroup, true);
        }

        internal static WorldMap BuildWorld()
        {
            var floor = WorldCollider.Mesh(
                _floorId,
                _defaultLayer,
                WorldCollider.StaticGroup,
                new[]
                {
                    new Vector3(-20f, 0f, -20f), new Vector3(-20f, 0f, 20f), new Vector3(20f, 0f, 20f), new Vector3(20f, 0f, -20f),
                },
                _floorIndices);
            var slope = WorldCollider.Mesh(
                _slopeId,
                _defaultLayer,
                WorldCollider.StaticGroup,
                new[]
                {
                    new Vector3(-5f, 0f, -2f), new Vector3(-5f, 0f, 2f), new Vector3(-10f, _slopeTop, 2f), new Vector3(-10f, _slopeTop, -2f),
                },
                _slopeIndices);
            return new WorldMap(new[]
            {
                floor,
                WorldCollider.Box(_wallId, _defaultLayer, WorldCollider.StaticGroup, new Vector3(5f, 1.5f, 0f), new Vector3(0.25f, 1.5f, 3f), Quaternion.Identity),
                WorldCollider.Box(_stepId, _defaultLayer, WorldCollider.StaticGroup, new Vector3(0f, 0.15f, 5f), new Vector3(1f, 0.15f, 1f), Quaternion.Identity),
                slope,
                WorldCollider.Sphere(_propId, _propLayer, WorldCollider.StaticGroup, new Vector3(0f, 1f, -5f), 0.5f),
                WorldCollider.Capsule(_poleId, _poleLayer, WorldCollider.StaticGroup, new Vector3(3f, 0.5f, -5f), new Vector3(3f, 1.5f, -5f), 0.3f),
                WorldCollider.Box(_crateId, _defaultLayer, _crateGroup, new Vector3(-3f, 1f, -8f), new Vector3(0.5f, 0.5f, 0.5f), Quaternion.Identity),
            });
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(_tolerance), "expected " + expected + " but was " + actual);
        }
    }
}

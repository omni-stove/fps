using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    // 目は (0, 2.1, 0) にあり、+Z の先 z = 7.5 に上の縁が y = 3 の壁がある。
    public sealed class GrappleTargetingTests
    {
        private const float _tolerance = 1e-3f;
        private const int _blocking = ~(1 << 2);

        private static readonly Vector3 _eye = new Vector3(0f, 2.1f, 0f);
        private static readonly Vector3 _forward = new Vector3(0f, 0f, 1f);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
        }

        [Test]
        public void TheDirectionFollowsYawAndPitch()
        {
            AssertNear(GrappleTargeting.Direction(0f, 0f), new Vector3(0f, 0f, 1f));
            AssertNear(GrappleTargeting.Direction(MathF.PI / 2f, 0f), new Vector3(1f, 0f, 0f));
            AssertNear(GrappleTargeting.Direction(0f, MathF.PI / 2f), new Vector3(0f, 1f, 0f));
            AssertNear(GrappleTargeting.Direction(0f, -MathF.PI / 4f), new Vector3(0f, -MathF.Sqrt(0.5f), MathF.Sqrt(0.5f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => GrappleTargeting.Direction(float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => GrappleTargeting.Direction(0f, float.PositiveInfinity));
        }

        [Test]
        public void AWallNearItsTopEdgeIsGrabbedAtTheEdge()
        {
            var wall = AddWall(0);

            var miss = Aim(Grapple(), _eye, _forward, out var target);

            Assert.That(miss, Is.EqualTo(GrappleMiss.None));
            Assert.That(target.ColliderId, Is.EqualTo(wall));
            Assert.That(target.SnappedToEdge, Is.True);
            AssertNear(target.Point, new Vector3(0f, 3f, 7.5f));
            AssertNear(target.Normal, new Vector3(0f, 0f, -1f));
            Assert.That(target.Distance, Is.EqualTo(7.5f).Within(_tolerance));
            Assert.That(target.IsWall, Is.True);
            Assert.That(target.IsFloor, Is.False);
        }

        [Test]
        public void WithoutEdgeSnappingTheHitPointIsKept()
        {
            AddWall(0);
            var spec = new GrappleSpec(edgeSnapDistance: 0f);

            var miss = Aim(new GrappleType("grapple", spec, AbilitySpec.Default), _eye, _forward, out var target);

            Assert.That(miss, Is.EqualTo(GrappleMiss.None));
            Assert.That(target.SnappedToEdge, Is.False);
            AssertNear(target.Point, new Vector3(0f, 2.1f, 7.5f));
        }

        [Test]
        public void AnEdgeOutOfReachIsNotSnapped()
        {
            AddWall(0);
            var spec = new GrappleSpec(edgeSnapDistance: 0.5f);

            Aim(new GrappleType("grapple", spec, AbilitySpec.Default), _eye, _forward, out var target);

            Assert.That(target.SnappedToEdge, Is.False);
            Assert.That(target.Point.Y, Is.EqualTo(2.1f).Within(_tolerance));
        }

        [Test]
        public void NothingWithinTheMaxRangeIsOutOfRange()
        {
            AddWall(0);

            Assert.That(Aim(Grapple(), _eye, -_forward, out _), Is.EqualTo(GrappleMiss.OutOfRange));
            Assert.That(
                Aim(new GrappleType("grapple", new GrappleSpec(maxRange: 5f), AbilitySpec.Default), _eye, _forward, out _),
                Is.EqualTo(GrappleMiss.OutOfRange));
            Assert.That(Aim(Grapple(), _eye, Vector3.Zero, out _), Is.EqualTo(GrappleMiss.OutOfRange));
            Assert.That(Aim(Grapple(), _eye, new Vector3(float.NaN, 0f, 1f), out _), Is.EqualTo(GrappleMiss.OutOfRange));
        }

        [Test]
        public void ASurfaceCloserThanTheMinRangeIsTooClose()
        {
            AddWall(0);

            var miss = Aim(Grapple(), new Vector3(0f, 2.1f, 6f), _forward, out var target);

            Assert.That(miss, Is.EqualTo(GrappleMiss.TooClose));
            Assert.That(target.Distance, Is.EqualTo(1.5f).Within(_tolerance));
        }

        [Test]
        public void ASurfaceOnAnotherLayerIsNotGrabbable()
        {
            AddWall(3);

            var miss = Aim(new GrappleType("grapple", GrappleSpec.Default, AbilitySpec.Default, 1 << 0), _eye, _forward, out var target);

            Assert.That(miss, Is.EqualTo(GrappleMiss.NotGrabbable));
            Assert.That(target.Layer, Is.EqualTo(3));
        }

        [Test]
        public void TheRopeDoesNotPassANotGrabbableSurfaceInFront()
        {
            AddWall(0);
            AddBox(3, new Vector3(0f, 2f, 4f), new Vector3(1f, 2f, 0.1f));

            var miss = Aim(new GrappleType("grapple", GrappleSpec.Default, AbilitySpec.Default, 1 << 0), _eye, _forward, out _);

            Assert.That(miss, Is.EqualTo(GrappleMiss.NotGrabbable));
        }

        [Test]
        public void SurfacesOutsideTheBlockingLayersAreIgnored()
        {
            var wall = AddWall(0);
            AddBox(2, new Vector3(0f, 2f, 4f), new Vector3(1f, 2f, 0.1f));

            var miss = Aim(Grapple(), _eye, _forward, out var target);

            Assert.That(miss, Is.EqualTo(GrappleMiss.None));
            Assert.That(target.ColliderId, Is.EqualTo(wall));
        }

        [Test]
        public void AFloorIsGrabbedWhereTheRopeHits()
        {
            AddBox(0, new Vector3(0f, 0.5f, 5f), new Vector3(2f, 0.5f, 2f));

            var direction = Vector3.Normalize(new Vector3(0f, 1f - 2.1f, 5f));
            var miss = Aim(Grapple(), _eye, direction, out var target);

            Assert.That(miss, Is.EqualTo(GrappleMiss.None));
            Assert.That(target.IsFloor, Is.True);
            Assert.That(target.IsWall, Is.False);
            Assert.That(target.SnappedToEdge, Is.False);
            AssertNear(target.Point, new Vector3(0f, 1f, 5f));
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            var world = World();

            Assert.Throws<ArgumentNullException>(() => GrappleTargeting.Aim(null, Grapple(), _blocking, _eye, _forward, out _));
            Assert.Throws<ArgumentNullException>(() => GrappleTargeting.Aim(world, null, _blocking, _eye, _forward, out _));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GrappleTargeting.Aim(world, Grapple(), _blocking, new Vector3(float.NaN, 0f, 0f), _forward, out _));
        }

        private static GrappleType Grapple()
        {
            return new GrappleType("grapple", GrappleSpec.Default, AbilitySpec.Default);
        }

        private GrappleMiss Aim(GrappleType grapple, Vector3 origin, Vector3 direction, out GrappleTarget target)
        {
            return GrappleTargeting.Aim(World(), grapple, _blocking, origin, direction, out target);
        }

        private BvhWorldCollision World()
        {
            return new BvhWorldCollision(new WorldMap(_colliders));
        }

        // 面が z = 7.5、上の縁が y = 3 の壁
        private int AddWall(int layer)
        {
            return AddBox(layer, new Vector3(0f, 1.5f, 8f), new Vector3(10f, 1.5f, 0.5f));
        }

        private int AddBox(int layer, Vector3 center, Vector3 halfExtents)
        {
            var id = _colliders.Count + 1;
            _colliders.Add(WorldCollider.Box(id, layer, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
            return id;
        }

        private static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(_tolerance));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(_tolerance));
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(_tolerance));
        }
    }
}

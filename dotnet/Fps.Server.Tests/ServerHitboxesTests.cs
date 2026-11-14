using System;
using System.Numerics;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerHitboxesTests
    {
        private static readonly StanceGeometry _geometry = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private ServerBodies _bodies;

        [SetUp]
        public void SetUp()
        {
            _bodies = new ServerBodies(_geometry);
        }

        [Test]
        public void TheTargetIsRewoundToTheTickTheShooterSaw()
        {
            var hitboxes = Hitboxes();
            var target = Spawn(2, new Vector3(10f, 1f, 0f));
            hitboxes.Record(1);
            target.SetPose(new Vector3(10f, 1f, 3f), 0f, PlayerStance.Standing);
            hitboxes.Record(2);

            Assert.That(hitboxes.Raycast(Vector3.Zero + Vector3.UnitY, Vector3.UnitX, 100f, 1f, 1, _ => true, out var old), Is.True);
            Assert.That(old.HitTarget, Is.True);
            Assert.That(old.TargetId, Is.EqualTo(2));
            Assert.That(old.Distance, Is.EqualTo(9.5f).Within(1e-4f));
            Assert.That(old.RewindTicks, Is.EqualTo(1f));
            Assert.That(hitboxes.Raycast(Vector3.UnitY, Vector3.UnitX, 100f, 2f, 1, _ => true, out _), Is.False);
        }

        [Test]
        public void BetweenTicksThePositionIsInterpolated()
        {
            var hitboxes = Hitboxes();
            var target = Spawn(2, new Vector3(10f, 1f, 0f));
            hitboxes.Record(1);
            target.SetPose(new Vector3(10f, 1f, 2f), 0f, PlayerStance.Standing);
            hitboxes.Record(2);

            Assert.That(hitboxes.Raycast(new Vector3(0f, 1f, 1f), Vector3.UnitX, 100f, 1.5f, 1, _ => true, out var hit), Is.True);
            Assert.That(hit.Distance, Is.EqualTo(9.5f).Within(1e-4f));
        }

        [Test]
        public void TheShooterTheUnhittableAndTheUnrecordedAreNotHit()
        {
            var hitboxes = Hitboxes();
            Spawn(1, new Vector3(0f, 1f, 0f));
            Spawn(2, new Vector3(5f, 1f, 0f));
            hitboxes.Record(1);
            Spawn(3, new Vector3(8f, 1f, 0f));

            Assert.That(hitboxes.Raycast(new Vector3(0f, 1f, 0f), Vector3.UnitX, 100f, 1f, 1, id => id != 2, out _), Is.False);
        }

        [Test]
        public void AWallInFrontHidesTheTarget()
        {
            var wall = WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(4f, 1f, 0f), new Vector3(0.1f, 2f, 2f), Quaternion.Identity);
            var hitboxes = new ServerHitboxes(_bodies, new BvhWorldCollision(new WorldMap(new[] { wall })));
            Spawn(2, new Vector3(10f, 1f, 0f));
            hitboxes.Record(1);

            Assert.That(hitboxes.Raycast(new Vector3(0f, 1f, 0f), Vector3.UnitX, 100f, 1f, 1, _ => true, out var hit), Is.True);
            Assert.That(hit.HitTarget, Is.False);
            Assert.That(hit.ColliderId, Is.EqualTo(1));
            Assert.That(hit.Distance, Is.EqualTo(3.9f).Within(1e-4f));
        }

        [Test]
        public void AWallBehindTheTargetDoesNotHideIt()
        {
            var wall = WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(12f, 1f, 0f), new Vector3(0.1f, 2f, 2f), Quaternion.Identity);
            var hitboxes = new ServerHitboxes(_bodies, new BvhWorldCollision(new WorldMap(new[] { wall })));
            Spawn(2, new Vector3(10f, 1f, 0f));
            hitboxes.Record(1);

            Assert.That(hitboxes.Raycast(new Vector3(0f, 1f, 0f), Vector3.UnitX, 100f, 1f, 1, _ => true, out var hit), Is.True);
            Assert.That(hit.HitTarget, Is.True);
        }

        [Test]
        public void TheZoneComesFromTheRewoundStance()
        {
            var hitboxes = Hitboxes();
            var target = Spawn(2, new Vector3(10f, 1f, 0f));
            hitboxes.Record(1);
            target.SetPose(new Vector3(10f, 1f, 0f), 0f, PlayerStance.Crouching);
            hitboxes.Record(2);

            // 立った頭の高さ (足元 0 から 1.9)。立っていた tick では頭、しゃがんだ tick では頭の上を通る
            Assert.That(hitboxes.Raycast(new Vector3(0f, 1.9f, 0f), Vector3.UnitX, 100f, 1f, 1, _ => true, out var standing), Is.True);
            Assert.That(standing.Zone, Is.EqualTo(HitZone.Head));
            Assert.That(standing.TargetCrouched, Is.False);
            Assert.That(hitboxes.Raycast(new Vector3(0f, 1.9f, 0f), Vector3.UnitX, 100f, 2f, 1, _ => true, out _), Is.False);
            Assert.That(hitboxes.Raycast(new Vector3(0f, 0.6f, 0f), Vector3.UnitX, 100f, 2f, 1, _ => true, out var crouched), Is.True);
            Assert.That(crouched.TargetCrouched, Is.True);
        }

        [Test]
        public void ForgettingAPlayerDropsItsHistory()
        {
            var hitboxes = Hitboxes();
            Spawn(2, new Vector3(10f, 1f, 0f));
            hitboxes.Record(1);
            hitboxes.Forget(2);

            Assert.That(hitboxes.Raycast(new Vector3(0f, 1f, 0f), Vector3.UnitX, 100f, 1f, 1, _ => true, out _), Is.False);
        }

        [TestCase(0f, 1f, 9.5f)]
        [TestCase(0f, 1.9f, 9.7f)]
        [TestCase(0f, 2.4f, -1f)]
        [TestCase(0.45f, 1f, 9.782f)]
        public void RayCapsuleFindsTheNearestSurface(float z, float y, float expected)
        {
            var found = ServerHitboxes.RayCapsule(
                new Vector3(0f, y, z), Vector3.UnitX, new Vector3(10f, 0.5f, 0f), new Vector3(10f, 1.5f, 0f), 0.5f, 100f, out var distance, out var normal);

            Assert.That(found, Is.EqualTo(expected >= 0f));
            if (found)
            {
                Assert.That(distance, Is.EqualTo(expected).Within(1e-3f));
                Assert.That(normal.X, Is.LessThan(0f));
            }
        }

        [Test]
        public void RayCapsuleRespectsTheMaximumDistance()
        {
            Assert.That(
                ServerHitboxes.RayCapsule(Vector3.Zero, Vector3.UnitX, new Vector3(10f, -1f, 0f), new Vector3(10f, 1f, 0f), 0.5f, 9f, out _, out _),
                Is.False);
        }

        private ServerHitboxes Hitboxes()
        {
            return new ServerHitboxes(_bodies, new BvhWorldCollision(new WorldMap(Array.Empty<WorldCollider>())));
        }

        private ServerBody Spawn(int id, Vector3 position)
        {
            _bodies.Add(id);
            _bodies.Teleport(id, position, 0f, 0);
            Assert.That(_bodies.TryGet(id, out var body), Is.True);
            return body;
        }
    }
}

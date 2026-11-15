using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerShotTraceRicochetTests
    {
        private static readonly StanceGeometry _geometry = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _muzzle = new Vector3(0f, 0.5f, 0f);

        private List<ShotTraceSegment> _segments;

        [SetUp]
        public void SetUp()
        {
            _segments = new List<ShotTraceSegment>();
        }

        [Test]
        public void AShallowShotIntoTheFloorBouncesIntoTheTarget()
        {
            var trace = Build(withTarget: true, withCeiling: false);

            trace.Trace(Round(1), new PlannedRound(_muzzle, Down(10f), 0), 1f, 1, _ => true, _segments);

            Assert.That(_segments, Has.Count.EqualTo(2));
            var floor = _segments[0];
            Assert.That(floor.Bounced, Is.True);
            Assert.That(floor.HasHit, Is.True);
            Assert.That(floor.Hit.HitTarget, Is.False);
            Assert.That(floor.Hit.Point.Y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(floor.DamageMultiplier, Is.EqualTo(1f));

            var bounced = _segments[1];
            Assert.That(bounced.Bounced, Is.False);
            Assert.That(bounced.Origin, Is.EqualTo(floor.Hit.Point));
            Assert.That(bounced.Direction.Y, Is.GreaterThan(0f));
            Assert.That(bounced.HasHit, Is.True);
            Assert.That(bounced.Hit.HitTarget, Is.True);
            Assert.That(bounced.Hit.TargetId, Is.EqualTo(2));
            Assert.That(bounced.DamageMultiplier, Is.EqualTo(0.5f));

            var path = floor.Hit.Distance + Vector3.Distance(floor.Hit.Point, bounced.Hit.Point);
            Assert.That(bounced.Hit.Distance, Is.EqualTo(path).Within(ServerShotTrace.RicochetSurfaceOffset), "falloff uses the whole path");
        }

        [Test]
        public void ADeepShotStopsAtTheFloor()
        {
            var trace = Build(withTarget: true, withCeiling: false);

            trace.Trace(Round(1), new PlannedRound(_muzzle, Down(45f), 0), 1f, 1, _ => true, _segments);

            Assert.That(_segments, Has.Count.EqualTo(1));
            Assert.That(_segments[0].Bounced, Is.False);
            Assert.That(_segments[0].HasHit, Is.True);
            Assert.That(_segments[0].Hit.HitTarget, Is.False);
        }

        [TestCase(1, 2)]
        [TestCase(2, 3)]
        public void TheRoundStopsAfterItsBounces(int maxBounces, int segments)
        {
            var trace = Build(withTarget: false, withCeiling: true);

            trace.Trace(Round(maxBounces), new PlannedRound(_muzzle, Down(10f), 0), 1f, 1, _ => true, _segments);

            Assert.That(_segments, Has.Count.EqualTo(segments));
            for (var i = 0; i < segments - 1; i++)
            {
                Assert.That(_segments[i].Bounced, Is.True, "segment " + i);
            }

            var last = _segments[segments - 1];
            Assert.That(last.Bounced, Is.False);
            Assert.That(last.HasHit, Is.True);
            Assert.That(last.DamageMultiplier, Is.EqualTo(MathF.Pow(0.5f, maxBounces)).Within(1e-6f));
        }

        [Test]
        public void TheBouncedRoundOnlyGoesAsFarAsTheRangeLeft()
        {
            var trace = Build(withTarget: true, withCeiling: false);

            trace.Trace(Round(1, range: 5f), new PlannedRound(_muzzle, Down(10f), 0), 1f, 1, _ => true, _segments);

            Assert.That(_segments, Has.Count.EqualTo(2));
            Assert.That(_segments[0].Bounced, Is.True);
            Assert.That(_segments[1].HasHit, Is.False);
            Assert.That(_segments[1].DamageMultiplier, Is.EqualTo(0.5f));
        }

        [Test]
        public void ARoundWithoutRicochetStopsAtTheFloor()
        {
            var trace = Build(withTarget: true, withCeiling: false);

            trace.Trace(RoundSpec.Default, new PlannedRound(_muzzle, Down(10f), 0), 1f, 1, _ => true, _segments);

            Assert.That(_segments, Has.Count.EqualTo(1));
            Assert.That(_segments[0].Bounced, Is.False);
            Assert.That(_segments[0].DamageMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void ARoundThatHitsAPlayerDoesNotBounce()
        {
            var trace = Build(withTarget: true, withCeiling: false);

            trace.Trace(Round(1), new PlannedRound(new Vector3(0f, 1.5f, 0f), Vector3.UnitX, 0), 1f, 1, _ => true, _segments);

            Assert.That(_segments, Has.Count.EqualTo(1));
            Assert.That(_segments[0].Hit.HitTarget, Is.True);
            Assert.That(_segments[0].Bounced, Is.False);
        }

        private static ServerShotTrace Build(bool withTarget, bool withCeiling)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            if (withCeiling)
            {
                colliders.Add(WorldCollider.Box(
                    2, 0, WorldCollider.StaticGroup, new Vector3(0f, 1.5f, 0f), new Vector3(60f, 0.5f, 60f), Quaternion.Identity));
            }

            var bodies = new ServerBodies(_geometry);
            var hitboxes = new ServerHitboxes(bodies, new BvhWorldCollision(new WorldMap(colliders)));
            if (withTarget)
            {
                bodies.Add(2);
                bodies.Teleport(2, new Vector3(10f, 1f, 0f), 0f, 0);
            }

            hitboxes.Record(1);
            return new ServerShotTrace(hitboxes);
        }

        private static RoundSpec Round(int maxBounces, float range = RoundSpec.DefaultRange)
        {
            return new RoundSpec(range: range, ricochet: new RicochetSpec(maxBounces, 20f, 0.5f));
        }

        private static Vector3 Down(float degrees)
        {
            var radians = degrees * (MathF.PI / 180f);
            return new Vector3(MathF.Cos(radians), -MathF.Sin(radians), 0f);
        }
    }
}

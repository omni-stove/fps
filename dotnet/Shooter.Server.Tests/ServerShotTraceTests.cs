using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed class ServerShotTraceTests
    {
        private static readonly StanceGeometry _geometry = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private ServerBodies _bodies;
        private ServerHitboxes _hitboxes;
        private ServerShotTrace _trace;
        private List<ShotTraceSegment> _segments;

        [SetUp]
        public void SetUp()
        {
            _bodies = new ServerBodies(_geometry);
            _hitboxes = new ServerHitboxes(_bodies, new BvhWorldCollision(new WorldMap(Array.Empty<WorldCollider>())));
            _trace = new ServerShotTrace(_hitboxes);
            _segments = new List<ShotTraceSegment>();
            _bodies.Add(2);
            _bodies.Teleport(2, new Vector3(10f, 1f, 0f), 0f, 0);
            _hitboxes.Record(1);
        }

        [Test]
        public void AHitscanRoundThatHitsIsOneSegmentEndingAtTheHit()
        {
            _trace.Trace(RoundSpec.Default, new PlannedRound(Vector3.UnitY, Vector3.UnitX, 0), 1f, 1, _ => true, _segments);

            Assert.That(_segments, Has.Count.EqualTo(1));
            Assert.That(_segments[0].Origin, Is.EqualTo(Vector3.UnitY));
            Assert.That(_segments[0].Direction, Is.EqualTo(Vector3.UnitX));
            Assert.That(_segments[0].HasHit, Is.True);
            Assert.That(_segments[0].Hit.TargetId, Is.EqualTo(2));
            Assert.That(_segments[0].Hit.Distance, Is.EqualTo(9.5f).Within(1e-4f));
        }

        [Test]
        public void AMissIsStillOneSegment()
        {
            _trace.Trace(RoundSpec.Default, new PlannedRound(Vector3.UnitY, -Vector3.UnitX, 0), 1f, 1, _ => true, _segments);

            Assert.That(_segments, Has.Count.EqualTo(1));
            Assert.That(_segments[0].HasHit, Is.False);
            Assert.That(_segments[0].Direction, Is.EqualTo(-Vector3.UnitX));
        }

        [Test]
        public void ARoundStopsAtItsRange()
        {
            _trace.Trace(new RoundSpec(range: 5f), new PlannedRound(Vector3.UnitY, Vector3.UnitX, 0), 1f, 1, _ => true, _segments);

            Assert.That(_segments, Has.Count.EqualTo(1));
            Assert.That(_segments[0].HasHit, Is.False);
        }

        [Test]
        public void SegmentsAreAddedAfterTheOnesAlreadyThere()
        {
            _trace.Trace(RoundSpec.Default, new PlannedRound(Vector3.UnitY, -Vector3.UnitX, 0), 1f, 1, _ => true, _segments);
            _trace.Trace(RoundSpec.Default, new PlannedRound(Vector3.UnitY, Vector3.UnitX, 1), 1f, 1, _ => true, _segments);

            Assert.That(_segments, Has.Count.EqualTo(2));
            Assert.That(_segments[0].HasHit, Is.False);
            Assert.That(_segments[1].HasHit, Is.True);
        }
    }
}

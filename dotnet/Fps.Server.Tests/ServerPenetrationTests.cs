using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerPenetrationTests
    {
        private const int _thinWallId = 10;
        private const int _thickWallId = 11;
        private const int _shooterId = 1;
        private const int _targetId = 2;
        private const int _farTargetId = 3;

        private static readonly StanceGeometry _geometry = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _eye = Vector3.UnitY;

        private ServerBodies _bodies;
        private ServerHitboxes _hitboxes;
        private ServerShotTrace _trace;
        private List<ShotTraceSegment> _segments;

        [SetUp]
        public void SetUp()
        {
            // +X には厚さ 0.2 の壁の先に人が 1 人、-X には厚さ 1 の壁の先に人が 1 人
            var world = new BvhWorldCollision(new WorldMap(new[]
            {
                WorldCollider.Box(_thinWallId, 0, WorldCollider.StaticGroup, new Vector3(5f, 1f, 0f), new Vector3(0.1f, 2f, 2f), Quaternion.Identity),
                WorldCollider.Box(_thickWallId, 0, WorldCollider.StaticGroup, new Vector3(-5f, 1f, 0f), new Vector3(0.5f, 2f, 2f), Quaternion.Identity),
            }));
            _bodies = new ServerBodies(_geometry);
            _hitboxes = new ServerHitboxes(_bodies, world);
            _trace = new ServerShotTrace(_hitboxes);
            _segments = new List<ShotTraceSegment>();
            Spawn(_targetId, new Vector3(10f, 1f, 0f));
            Spawn(_farTargetId, new Vector3(-10f, 1f, 0f));
            _hitboxes.Record(1);
        }

        [Test]
        public void ARoundWithoutPenetrationStopsAtTheWall()
        {
            Trace(RoundSpec.Default, Vector3.UnitX);

            Assert.That(_segments, Has.Count.EqualTo(1));
            Assert.That(_segments[0].Hit.HitTarget, Is.False);
            Assert.That(_segments[0].Hit.ColliderId, Is.EqualTo(_thinWallId));
            Assert.That(_segments[0].DamageMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void ARoundGoesThroughAThinWallAndHitsThePersonBehindWithLessDamage()
        {
            Trace(Penetrating(new PenetrationSpec(1, 0.5f, 0.3f)), Vector3.UnitX);

            Assert.That(_segments, Has.Count.EqualTo(2));
            Assert.That(_segments[0].Hit.ColliderId, Is.EqualTo(_thinWallId));
            Assert.That(_segments[0].DamageMultiplier, Is.EqualTo(1f));
            Assert.That(_segments[1].Origin, Is.EqualTo(_segments[0].Hit.Point));
            Assert.That(_segments[1].Hit.TargetId, Is.EqualTo(_targetId));
            Assert.That(_segments[1].Hit.Distance, Is.EqualTo(9.5f).Within(1e-4f));
            Assert.That(_segments[1].DamageMultiplier, Is.EqualTo(0.5f));
        }

        [Test]
        public void AWallThickerThanTheRoundCanGoThroughStopsIt()
        {
            Trace(Penetrating(new PenetrationSpec(3, 0.5f, 0.3f)), -Vector3.UnitX);

            Assert.That(_segments, Has.Count.EqualTo(1));
            Assert.That(_segments[0].Hit.ColliderId, Is.EqualTo(_thickWallId));
        }

        [Test]
        public void AMaterialThatCannotBePenetratedStopsTheRound()
        {
            var unbreakable = new PenetrationSpec(3, 0.5f, 0.3f, new[] { new PenetrationMaterialSpec(ServerCombat.DefaultSurfaceMaterialId, 0f) });

            Trace(Penetrating(unbreakable), Vector3.UnitX);

            Assert.That(_segments, Has.Count.EqualTo(1));
            Assert.That(_segments[0].Hit.ColliderId, Is.EqualTo(_thinWallId));
        }

        [Test]
        public void TheMaterialScalesTheThicknessTheRoundGoesThrough()
        {
            var stronger = new PenetrationSpec(1, 0.5f, 0.3f, new[] { new PenetrationMaterialSpec(ServerCombat.DefaultSurfaceMaterialId, 4f) });

            Trace(Penetrating(stronger), -Vector3.UnitX);

            Assert.That(_segments, Has.Count.EqualTo(2));
            Assert.That(_segments[1].Hit.TargetId, Is.EqualTo(_farTargetId));
        }

        [Test]
        public void EachPassMultipliesTheDamageAndARoundLeftOverFliesOnPastTheLastHit()
        {
            Trace(Penetrating(new PenetrationSpec(3, 0.5f, 0.3f)), Vector3.UnitX);

            Assert.That(_segments, Has.Count.EqualTo(3));
            Assert.That(_segments[1].Hit.TargetId, Is.EqualTo(_targetId));
            Assert.That(_segments[1].DamageMultiplier, Is.EqualTo(0.5f));
            Assert.That(_segments[2].HasHit, Is.False);
            Assert.That(_segments[2].Origin, Is.EqualTo(_segments[1].Hit.Point));
            Assert.That(_segments[2].Direction, Is.EqualTo(Vector3.UnitX));
        }

        [Test]
        public void AThickRoundDoesNotPenetrate()
        {
            Trace(new RoundSpec(radius: 0.1f, penetration: new PenetrationSpec(3, 0.5f, 0.3f)), Vector3.UnitX);

            Assert.That(_segments, Has.Count.EqualTo(1));
            Assert.That(_segments[0].Hit.ColliderId, Is.EqualTo(_thinWallId));
        }

        [Test]
        public void APenetratingRoundDoesNotBounceOffTheWallThatStopsIt()
        {
            var round = new RoundSpec(penetration: new PenetrationSpec(3, 0.5f, 0.3f), ricochet: new RicochetSpec(3, 90f, 0.5f));

            Trace(round, -Vector3.UnitX);

            Assert.That(_segments, Has.Count.EqualTo(1));
            Assert.That(_segments[0].Hit.ColliderId, Is.EqualTo(_thickWallId));
            Assert.That(_segments[0].Bounced, Is.False);
        }

        [Test]
        public void TheRoundStopsAfterItsMaxPenetrations()
        {
            Spawn(4, new Vector3(20f, 1f, 0f));
            _hitboxes.Record(2);

            _trace.Trace(Penetrating(new PenetrationSpec(1, 0.5f, 0.3f)), new PlannedRound(_eye, Vector3.UnitX, 0), 2f, _shooterId, _ => true, _segments);

            Assert.That(_segments, Has.Count.EqualTo(2));
            Assert.That(_segments[1].Hit.TargetId, Is.EqualTo(_targetId));
        }

        [Test]
        public void PeopleInARowAreAllHitWithTheDamageFallingEachTime()
        {
            Spawn(4, new Vector3(20f, 1f, 0f));
            _hitboxes.Record(2);

            _trace.Trace(Penetrating(new PenetrationSpec(2, 0.5f, 0.3f)), new PlannedRound(_eye, Vector3.UnitX, 0), 2f, _shooterId, _ => true, _segments);

            Assert.That(_segments, Has.Count.EqualTo(3));
            Assert.That(_segments[2].Hit.TargetId, Is.EqualTo(4));
            Assert.That(_segments[2].DamageMultiplier, Is.EqualTo(0.25f));
        }

        [Test]
        public void CastingForManyHitsReturnsTheWorldAndPeopleNearestFirst()
        {
            var hits = new List<ShotHit>();

            Assert.That(_hitboxes.Cast(_eye, Vector3.UnitX, 0f, 100f, 5, 1f, _shooterId, _ => true, hits), Is.True);

            Assert.That(hits, Has.Count.EqualTo(2), "the far face of the wall is not a second hit");
            Assert.That(hits[0].ColliderId, Is.EqualTo(_thinWallId));
            Assert.That(hits[0].Distance, Is.EqualTo(4.9f).Within(1e-4f));
            Assert.That(hits[1].TargetId, Is.EqualTo(_targetId));
        }

        [Test]
        public void CastingForManyHitsKeepsTheNearestOnes()
        {
            var hits = new List<ShotHit>();

            Assert.That(_hitboxes.Cast(_eye, Vector3.UnitX, 0f, 100f, 1, 1f, _shooterId, _ => true, hits), Is.True);
            Assert.That(_hitboxes.Cast(_eye, Vector3.UnitX, 0f, 100f, 2, 1f, _shooterId, id => id != _targetId, hits), Is.True);

            Assert.That(hits, Has.Count.EqualTo(2));
            Assert.That(hits[0].ColliderId, Is.EqualTo(_thinWallId));
            Assert.That(hits[1].ColliderId, Is.EqualTo(_thinWallId), "people who cannot be hit are passed by");
        }

        [Test]
        public void TheThicknessIsMeasuredUpToTheMax()
        {
            var hits = new List<ShotHit>();
            _hitboxes.Cast(_eye, Vector3.UnitX, 0f, 100f, 2, 1f, _shooterId, _ => true, hits);
            _hitboxes.Cast(_eye, -Vector3.UnitX, 0f, 100f, 1, 1f, _shooterId, _ => true, hits);

            Assert.That(_hitboxes.Thickness(hits[0], Vector3.UnitX, 0.3f), Is.EqualTo(0.2f).Within(1e-3f));
            Assert.That(_hitboxes.Thickness(hits[0], Vector3.UnitX, 0.1f), Is.EqualTo(float.PositiveInfinity));
            Assert.That(_hitboxes.Thickness(hits[1], Vector3.UnitX, 0f), Is.Zero, "a person counts as no thickness");
            Assert.That(_hitboxes.Thickness(hits[2], -Vector3.UnitX, 0.3f), Is.EqualTo(float.PositiveInfinity));
            Assert.That(_hitboxes.Thickness(hits[2], -Vector3.UnitX, 2f), Is.EqualTo(1f).Within(1e-3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _hitboxes.Thickness(hits[0], Vector3.UnitX, -1f));
        }

        private static RoundSpec Penetrating(PenetrationSpec penetration)
        {
            return new RoundSpec(penetration: penetration);
        }

        private void Trace(RoundSpec round, Vector3 direction)
        {
            _trace.Trace(round, new PlannedRound(_eye, direction, 0), 1f, _shooterId, _ => true, _segments);
        }

        private void Spawn(int playerId, Vector3 position)
        {
            _bodies.Add(playerId);
            _bodies.Teleport(playerId, position, 0f, 0);
        }
    }
}

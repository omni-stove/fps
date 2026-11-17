using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class PenetrationDeliveryTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1f, 0f);

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
        public void WithoutPenetrationAThinWallStopsTheRound()
        {
            CreateWall(5f, 0.2f);
            var target = CreateTarget("dummy", 10f);

            var outcome = HitscanDelivery.Instance.Deliver(Request(40, null), Straight());

            Assert.That(outcome.Owner, Is.Null);
            Assert.That(target.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void TheRoundGoesThroughAThinWallAndHurtsThePersonBehindLess()
        {
            CreateWall(5f, 0.2f);
            var target = CreateTarget("dummy", 10f);

            var outcome = HitscanDelivery.Instance.Deliver(Request(40, new PenetrationSpec(1, 0.5f, 0.3f)), Straight());

            Assert.That(outcome.Owner, Is.SameAs(target));
            Assert.That(outcome.Damage, Is.EqualTo(20));
            Assert.That(target.Health.Current, Is.EqualTo(80));
        }

        [Test]
        public void AWallThickerThanTheMaxStopsTheRound()
        {
            CreateWall(5f, 0.5f);
            var target = CreateTarget("dummy", 10f);

            var outcome = HitscanDelivery.Instance.Deliver(Request(40, new PenetrationSpec(1, 0.5f, 0.3f)), Straight());

            Assert.That(outcome.Hit, Is.True);
            Assert.That(outcome.Owner, Is.Null);
            Assert.That(target.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void AMaterialThatCannotBePenetratedStopsTheRound()
        {
            CreateWall(5f, 0.05f, "metal");
            var target = CreateTarget("dummy", 10f);
            var penetration = new PenetrationSpec(1, 0.5f, 0.3f, new[] { new PenetrationMaterialSpec("metal", 0f) });

            HitscanDelivery.Instance.Deliver(Request(40, penetration), Straight());

            Assert.That(target.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void TheMaterialScalesTheThicknessTheRoundGoesThrough()
        {
            CreateWall(5f, 0.5f, "wood");
            var target = CreateTarget("dummy", 10f);
            var penetration = new PenetrationSpec(1, 0.5f, 0.3f, new[] { new PenetrationMaterialSpec("wood", 2f) });

            HitscanDelivery.Instance.Deliver(Request(40, penetration), Straight());

            Assert.That(target.Health.Current, Is.EqualTo(80));
        }

        [Test]
        public void TheRoundStopsAfterItsMaxPenetrations()
        {
            CreateWall(4f, 0.1f);
            CreateWall(6f, 0.1f);
            var target = CreateTarget("dummy", 10f);

            HitscanDelivery.Instance.Deliver(Request(40, new PenetrationSpec(1, 0.5f, 0.3f)), Straight());
            Assert.That(target.Health.Current, Is.EqualTo(100));

            HitscanDelivery.Instance.Deliver(Request(40, new PenetrationSpec(2, 0.5f, 0.3f)), Straight());
            Assert.That(target.Health.Current, Is.EqualTo(90), "two passes leave a quarter of the damage");
        }

        [Test]
        public void PeopleInARowAreEachHitOnceAndTheFirstIsReported()
        {
            var first = CreateTarget("first", 5f);
            AddLimb(first);
            var second = CreateTarget("second", 10f);

            var outcome = HitscanDelivery.Instance.Deliver(Request(40, new PenetrationSpec(2, 0.5f, 0.3f)), Straight());

            Assert.That(outcome.Owner, Is.SameAs(first));
            Assert.That(first.Health.Current, Is.EqualTo(60), "the limb behind the body is the same person");
            Assert.That(second.Health.Current, Is.EqualTo(80));
        }

        [Test]
        public void TheThicknessIsMeasuredUpToTheMax()
        {
            var wall = CreateWall(5f, 0.2f).GetComponent<Collider>();
            var entry = new Vector3(0f, 1f, 4.9f);

            Assert.That(ShotCast.Thickness(wall, entry, Vector3.forward, 0.3f), Is.EqualTo(0.2f).Within(1e-3f));
            Assert.That(ShotCast.Thickness(wall, entry, Vector3.forward, 0.1f), Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void CastAllListsEveryHitNearestFirst()
        {
            CreateWall(6f, 0.1f);
            CreateWall(3f, 0.1f);
            var hits = new List<RaycastHit>();

            Assert.That(ShotCast.CastAll(_eye, Vector3.forward, 50f, Physics.DefaultRaycastLayers, null, hits), Is.EqualTo(2));
            Assert.That(hits[0].distance, Is.LessThan(hits[1].distance));
            Assert.That(hits[0].point.z, Is.EqualTo(2.95f).Within(1e-3f));
        }

        private static ShotRequest Request(int damage, PenetrationSpec penetration)
        {
            return new ShotRequest(
                null, null, null, new RoundSpec(penetration: penetration), new ImpactSpec(damage), "test-rifle", Physics.DefaultRaycastLayers);
        }

        private static PlannedRound Straight()
        {
            return new PlannedRound(_eye.ToNumerics(), Vector3.forward.ToNumerics(), 0);
        }

        private GameObject CreateWall(float z, float thickness, string material = null)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.transform.position = new Vector3(0f, 1f, z);
            wall.transform.localScale = new Vector3(4f, 4f, thickness);
            if (material != null)
            {
                wall.AddComponent<SurfaceMaterial>().Id = material;
            }

            Physics.SyncTransforms();
            return wall;
        }

        private Damageable CreateTarget(string id, float z)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(target);
            target.name = id;
            target.transform.position = new Vector3(0f, 1f, z);
            var damageable = target.AddComponent<Damageable>();
            damageable.Initialize(id, 100);
            target.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            Physics.SyncTransforms();
            return damageable;
        }

        // 体の後ろに重なる同じ人の部位
        private static void AddLimb(Damageable owner)
        {
            var limb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            limb.transform.SetParent(owner.transform, false);
            limb.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            limb.transform.localScale = Vector3.one * 0.5f;
            limb.AddComponent<Hitbox>().Initialize(HitZone.Body, owner);
            Physics.SyncTransforms();
        }
    }
}

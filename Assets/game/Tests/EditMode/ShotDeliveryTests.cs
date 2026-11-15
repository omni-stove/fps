using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class ShotDeliveryTests
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

        [TestCase(RoundDelivery.Hitscan, typeof(HitscanDelivery))]
        [TestCase(RoundDelivery.Projectile, typeof(ProjectileDelivery))]
        [TestCase(RoundDelivery.Beam, typeof(BeamDelivery))]
        [TestCase(RoundDelivery.Spray, typeof(SprayDelivery))]
        public void EachDeliveryHasItsOwnHook(RoundDelivery delivery, System.Type expected)
        {
            Assert.That(ShotDeliveries.For(delivery), Is.InstanceOf(expected));
        }

        [TestCase(RoundDelivery.Hitscan)]
        [TestCase(RoundDelivery.Beam)]
        [TestCase(RoundDelivery.Spray)]
        public void EveryDeliveryHitsLikeTheHitscanUntilItIsFilledIn(RoundDelivery delivery)
        {
            var target = CreateTarget("dummy", new Vector3(0f, 1f, 10f), HitZone.Head);

            var outcome = ShotDeliveries.For(delivery).Deliver(Request(10), Straight());

            Assert.That(outcome.Hit, Is.True);
            Assert.That(outcome.Owner, Is.SameAs(target));
            Assert.That(outcome.Damage, Is.EqualTo(20), "the head multiplier is applied once");
            Assert.That(outcome.WasAlive, Is.True);
            Assert.That(outcome.Killed, Is.False);
            Assert.That(target.Health.Current, Is.EqualTo(80));
        }

        [Test]
        public void TheImpactUsesTheTargetsZoneMultipliersAndReportsTheKill()
        {
            var target = CreateTarget("boss", new Vector3(0f, 1f, 10f), HitZone.Head);
            target.ZoneMultipliers = new ZoneDamageMultipliers(10d, 1d, 1d, 1d);

            var outcome = HitscanDelivery.Instance.Deliver(Request(10), Straight());

            Assert.That(outcome.Damage, Is.EqualTo(100));
            Assert.That(outcome.Killed, Is.True);
            Assert.That(target.IsDead, Is.True);
        }

        [Test]
        public void ADeadTargetIsHitWithoutTakingDamage()
        {
            var target = CreateTarget("dead", new Vector3(0f, 1f, 10f), HitZone.Body);
            target.Kill();

            var outcome = HitscanDelivery.Instance.Deliver(Request(10), Straight());

            Assert.That(outcome.Hit, Is.True);
            Assert.That(outcome.WasAlive, Is.False);
            Assert.That(outcome.Killed, Is.False);
            Assert.That(target.Health.Current, Is.Zero);
        }

        [Test]
        public void AMissRaisesTheShotButReportsNoHit()
        {
            var shots = 0;
            void Count(ShotFiredInfo _) => shots++;
            CombatEvents.ShotFired += Count;
            try
            {
                var outcome = HitscanDelivery.Instance.Deliver(Request(10), Straight());
                Assert.That(outcome.Hit, Is.False);
                Assert.That(shots, Is.EqualTo(1));
            }
            finally
            {
                CombatEvents.ShotFired -= Count;
            }
        }

        [Test]
        public void TheLayerMaskOfTheRequestDecidesWhatTheShotCanHit()
        {
            var target = CreateTarget("ignored", new Vector3(0f, 1f, 10f), HitZone.Body);
            target.gameObject.layer = 2;

            var outcome = HitscanDelivery.Instance.Deliver(Request(10, ~(1 << 2)), Straight());

            Assert.That(outcome.Hit, Is.False);
            Assert.That(target.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void ShotCastSkipsTheShooter()
        {
            var shooter = CreateTarget("shooter", new Vector3(0f, 1f, 2f), HitZone.Body);
            var target = CreateTarget("target", new Vector3(0f, 1f, 10f), HitZone.Body);

            Assert.That(ShotCast.TryFirst(_eye, Vector3.forward, 50f, Physics.DefaultRaycastLayers, shooter.transform, out var hit), Is.True);
            Assert.That(hit.collider.gameObject, Is.SameAs(target.gameObject));
            Assert.Throws<System.ArgumentException>(() => ShotCast.TryFirst(_eye, Vector3.zero, 50f, Physics.DefaultRaycastLayers, null, out _));
        }

        [Test]
        public void AThickRoundHitsATargetTheThinLineMisses()
        {
            // 既定のカプセルは半径 0.5。線は横に 0.7 ずれて外れ、半径 0.3 の弾なら端をかすめて当たる
            var target = CreateTarget("dummy", new Vector3(0.7f, 1f, 10f), HitZone.Body);

            Assert.That(HitscanDelivery.Instance.Deliver(Request(10), Straight()).Hit, Is.False);
            var outcome = HitscanDelivery.Instance.Deliver(Request(10, radius: 0.3f), Straight());

            Assert.That(outcome.Hit, Is.True);
            Assert.That(outcome.Owner, Is.SameAs(target));
            Assert.That(target.Health.Current, Is.EqualTo(90));
        }

        [Test]
        public void AThickShotCastSkipsTheShooterAndWhatItStartsAgainst()
        {
            var shooter = CreateTarget("shooter", new Vector3(0f, 1f, 0.3f), HitZone.Body);
            var beside = CreateTarget("beside", new Vector3(0.9f, 1f, 0f), HitZone.Body);
            var target = CreateTarget("target", new Vector3(0f, 1f, 10f), HitZone.Body);

            Assert.That(ShotCast.TryFirst(_eye, Vector3.forward, 0.5f, 50f, Physics.DefaultRaycastLayers, shooter.transform, out var hit), Is.True);
            Assert.That(hit.collider.gameObject, Is.SameAs(target.gameObject));
            Assert.That(hit.collider.gameObject, Is.Not.SameAs(beside.gameObject));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => ShotCast.TryFirst(_eye, Vector3.forward, -1f, 50f, Physics.DefaultRaycastLayers, null, out _));
        }

        [Test]
        public void AThickShotCastStopsAtAWallStraightAheadItStartsInside()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.transform.position = new Vector3(0f, 1f, 0.3f);
            wall.transform.localScale = new Vector3(4f, 4f, 0.1f);
            CreateTarget("target", new Vector3(0f, 1f, 10f), HitZone.Body);
            Physics.SyncTransforms();

            Assert.That(ShotCast.TryFirst(_eye, Vector3.forward, 0.5f, 50f, Physics.DefaultRaycastLayers, null, out var hit), Is.True);
            Assert.That(hit.collider.gameObject, Is.SameAs(wall));
        }

        [Test]
        public void TheEnemyPathLogsTheHitWithTheTargetsHealth()
        {
            CreateTarget("dummy", new Vector3(0f, 1f, 10f), HitZone.Body);

            LogAssert.Expect(LogType.Log, "[Combat] hit zone=Body dmg=10 id=dummy hp=90");
            var result = Hitscan.Fire(Request(10), Straight());

            Assert.That(result.Damage, Is.EqualTo(10));
        }

        [Test]
        public void ResolvedDamageIsTakenAsIsAndAStatusReceiverIsAddedOnlyOnRequest()
        {
            var target = CreateTarget("dummy", new Vector3(0f, 1f, 10f), HitZone.Head);

            Assert.That(target.ApplyResolvedHit(7, HitZone.Head, null, null, DamageKind.Bullet), Is.EqualTo(7));
            Assert.That(target.Health.Current, Is.EqualTo(93), "no zone multiplier is applied again");

            Assert.That(target.StatusEffects, Is.Null);
            var receiver = target.EnsureStatusEffects();
            Assert.That(receiver, Is.Not.Null);
            Assert.That(target.EnsureStatusEffects(), Is.SameAs(receiver));
            Assert.That(target.StatusEffects, Is.SameAs(receiver));
        }

        private static ShotRequest Request(int damage, int layerMask = Physics.DefaultRaycastLayers, float radius = 0f)
        {
            return new ShotRequest(null, null, null, new RoundSpec(radius: radius), new ImpactSpec(damage), "test-rifle", layerMask);
        }

        private static PlannedRound Straight()
        {
            return new PlannedRound(_eye.ToNumerics(), Vector3.forward.ToNumerics(), 0);
        }

        private Damageable CreateTarget(string id, Vector3 position, HitZone zone)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(target);
            target.name = id;
            target.transform.position = position;
            var damageable = target.AddComponent<Damageable>();
            damageable.Initialize(id, 100);
            target.AddComponent<Hitbox>().Initialize(zone, damageable);
            Physics.SyncTransforms();
            return damageable;
        }
    }
}

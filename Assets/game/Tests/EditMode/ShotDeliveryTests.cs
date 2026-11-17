using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ShotDeliveryTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _lowMuzzle = new Vector3(0f, 0.5f, 0f);

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

        [Test]
        public void TheHitscanDeliveryHitsWhatTheLineTouches()
        {
            var target = CreateTarget("dummy", new Vector3(0f, 1f, 10f), HitZone.Head);

            var outcome = ShotDeliveries.For(RoundDelivery.Hitscan).Deliver(Request(10), Straight());

            Assert.That(outcome.Hit, Is.True);
            Assert.That(outcome.Owner, Is.SameAs(target));
            Assert.That(outcome.Damage, Is.EqualTo(20), "the head multiplier is applied once");
            Assert.That(outcome.WasAlive, Is.True);
            Assert.That(outcome.Killed, Is.False);
            Assert.That(target.Health.Current, Is.EqualTo(80));
        }

        [Test]
        public void TheBeamDamagesWhatItTouchesAndDrawsTheLineFromTheMuzzleToTheHit()
        {
            var shooter = new GameObject("beam shooter");
            _created.Add(shooter);
            var target = CreateTarget("dummy", new Vector3(0f, 1f, 10f), HitZone.Body);
            var shots = 0;
            void Count(ShotFiredInfo _) => shots++;
            CombatEvents.ShotFired += Count;
            try
            {
                var outcome = BeamDelivery.Instance.Deliver(BeamRequest(shooter, 4), Straight());

                Assert.That(outcome.Owner, Is.SameAs(target));
                Assert.That(target.Health.Current, Is.EqualTo(96));
                Assert.That(shots, Is.Zero, "a beam tick is not a gunshot");
            }
            finally
            {
                CombatEvents.ShotFired -= Count;
            }

            var line = shooter.GetComponent<BeamLine>();
            _created.Add(line.Visual);
            Assert.That(line.IsVisible, Is.True);
            Assert.That(line.Line.GetPosition(0), Is.EqualTo(WeaponSockets.None.MuzzlePose(_eye, Vector3.forward).position));
            Assert.That(line.Line.GetPosition(1).z, Is.EqualTo(10f - 0.5f).Within(1e-3f), "the near side of the capsule");
            Assert.That(line.Line.sharedMaterial, Is.Not.Null, "an empty visual builds the line in code");
        }

        [Test]
        public void TheBeamReachesItsRangeOnAMissAndTheLineGoesOutAfterTheTicksStop()
        {
            var shooter = new GameObject("beam shooter");
            _created.Add(shooter);

            var outcome = BeamDelivery.Instance.Deliver(BeamRequest(shooter, 4), Straight());

            Assert.That(outcome.Hit, Is.False);
            var line = shooter.GetComponent<BeamLine>();
            _created.Add(line.Visual);
            Assert.That(line.Line.GetPosition(1), Is.EqualTo(_eye + (Vector3.forward * 30f)));

            var shownAt = Time.time;
            line.HideIfExpired(shownAt + (0.1f * BeamLine.HoldTicks) - 0.01f);
            Assert.That(line.IsVisible, Is.True, "the next tick may still come");
            line.HideIfExpired(shownAt + (0.1f * BeamLine.HoldTicks));
            Assert.That(line.IsVisible, Is.False);
        }

        [Test]
        public void TheSprayBurnsEveryoneInsideTheConeAndNobodyOutsideIt()
        {
            var shooter = new GameObject("spray shooter");
            _created.Add(shooter);
            var near = CreateTarget("near", new Vector3(0.2f, 1f, 3f), HitZone.Body);
            var far = CreateTarget("far", new Vector3(-1.5f, 1f, 6f), HitZone.Body);
            var beside = CreateTarget("beside", new Vector3(4f, 1f, 3f), HitZone.Body);
            var beyond = CreateTarget("beyond", new Vector3(0f, 1f, 12f), HitZone.Body);
            var behind = CreateTarget("behind", new Vector3(0f, 1f, -3f), HitZone.Body);
            var landed = new List<ShotOutcome>();
            var shots = 0;
            void Count(ShotFiredInfo _) => shots++;
            CombatEvents.ShotFired += Count;
            try
            {
                var outcome = SprayDelivery.Instance.Deliver(SprayRequest(shooter, 3, landed.Add), Straight());

                Assert.That(outcome.Owner, Is.SameAs(near), "the nearest one is the outcome");
                Assert.That(landed.Select(l => l.Owner), Is.EqualTo(new[] { far }), "the rest are told like landed rounds");
                Assert.That(shots, Is.Zero, "a spray tick is not a gunshot");
            }
            finally
            {
                CombatEvents.ShotFired -= Count;
            }

            foreach (var burnt in new[] { near, far })
            {
                Assert.That(burnt.Health.Current, Is.EqualTo(97), burnt.name);
                Assert.That(burnt.StatusEffects, Is.Not.Null, burnt.name);
                Assert.That(burnt.StatusEffects.Has(StatusEffectKinds.Burn), Is.True, burnt.name);
            }

            foreach (var spared in new[] { beside, beyond, behind })
            {
                Assert.That(spared.Health.Current, Is.EqualTo(100), spared.name);
                Assert.That(spared.StatusEffects == null || !spared.StatusEffects.Has(StatusEffectKinds.Burn), Is.True, spared.name);
            }
        }

        [Test]
        public void TheSprayCatchesABodyWhoseEdgeIsInsideTheCone()
        {
            var shooter = new GameObject("spray shooter");
            _created.Add(shooter);

            // 中心は 15 度の円錐の外 (約 17 度) だが、カプセルの半径 0.5 の端は中に入る
            var target = CreateTarget("edge", new Vector3(1.2f, 1f, 4f), HitZone.Body);

            var outcome = SprayDelivery.Instance.Deliver(SprayRequest(shooter, 3), Straight());

            Assert.That(outcome.Owner, Is.SameAs(target));
        }

        [Test]
        public void TheSprayDoesNotBurnATargetBehindAWall()
        {
            var shooter = new GameObject("spray shooter");
            _created.Add(shooter);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.transform.position = new Vector3(0f, 1f, 2f);
            wall.transform.localScale = new Vector3(4f, 4f, 0.1f);
            var hidden = CreateTarget("hidden", new Vector3(0f, 1f, 4f), HitZone.Body);

            var outcome = SprayDelivery.Instance.Deliver(SprayRequest(shooter, 3), Straight());

            Assert.That(outcome.Hit, Is.False);
            Assert.That(hidden.Health.Current, Is.EqualTo(100));
            Assert.That(hidden.StatusEffects, Is.Null);

            var flame = shooter.GetComponent<SprayFlame>();
            _created.Add(flame.Visual);
            Assert.That(flame.CodeParticles.main.startLifetime.constant * SprayFlame.CodeFlameSpeed, Is.EqualTo(2f - 0.05f).Within(1e-3f), "the flame stops at the wall");
        }

        [Test]
        public void TheSprayShowsACodeBuiltFlameFromTheMuzzleThatStopsAfterTheTicksEnd()
        {
            var shooter = new GameObject("spray shooter");
            _created.Add(shooter);

            SprayDelivery.Instance.Deliver(SprayRequest(shooter, 3), Straight());

            var flame = shooter.GetComponent<SprayFlame>();
            _created.Add(flame.Visual);
            Assert.That(flame.IsEmitting, Is.True);
            Assert.That(flame.Visual.transform.position, Is.EqualTo(WeaponSockets.None.MuzzlePose(_eye, Vector3.forward).position));
            Assert.That(flame.Visual.transform.forward.z, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(flame.CodeParticles, Is.Not.Null, "an empty visual builds the flame in code");
            Assert.That(flame.CodeParticles.shape.angle, Is.EqualTo(15f));
            Assert.That(flame.CodeParticles.main.startLifetime.constant * SprayFlame.CodeFlameSpeed, Is.EqualTo(8f).Within(1e-3f), "the whole range");

            var shownAt = Time.time;
            flame.StopIfExpired(shownAt + (0.1f * SprayFlame.HoldTicks) - 0.01f);
            Assert.That(flame.IsEmitting, Is.True, "the next tick may still come");
            flame.StopIfExpired(shownAt + (0.1f * SprayFlame.HoldTicks));
            Assert.That(flame.IsEmitting, Is.False);
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

        [Test]
        public void AShallowShotBouncesOffTheFloorIntoTheTargetWithLessDamage()
        {
            CreateFloor();
            var target = CreateTarget("dummy", new Vector3(0f, 1f, 10f), HitZone.Body);
            var fired = 0;
            var continued = new List<ShotFiredInfo>();
            void CountFired(ShotFiredInfo _) => fired++;
            void CountContinued(ShotFiredInfo shot) => continued.Add(shot);
            CombatEvents.ShotFired += CountFired;
            CombatEvents.ShotContinued += CountContinued;
            try
            {
                var outcome = HitscanDelivery.Instance.Deliver(RicochetRequest(10, 1), Shallow(10f));

                Assert.That(outcome.Owner, Is.SameAs(target));
                Assert.That(outcome.Damage, Is.EqualTo(5));
                Assert.That(target.Health.Current, Is.EqualTo(95));
                Assert.That(fired, Is.EqualTo(1), "the gunshot is not played again at the bounce");
                Assert.That(continued, Has.Count.EqualTo(1));
                Assert.That(continued[0].Origin.y, Is.EqualTo(0f).Within(1e-3f));
                Assert.That(continued[0].Direction.y, Is.GreaterThan(0f));
                Assert.That(outcome.RaycastHit.distance, Is.GreaterThan(Vector3.Distance(_lowMuzzle, outcome.RaycastHit.point)), "falloff uses the whole path");
            }
            finally
            {
                CombatEvents.ShotFired -= CountFired;
                CombatEvents.ShotContinued -= CountContinued;
            }
        }

        [Test]
        public void ADeepShotStopsAtTheFloor()
        {
            var floor = CreateFloor();
            var target = CreateTarget("dummy", new Vector3(0f, 1f, 10f), HitZone.Body);

            var outcome = HitscanDelivery.Instance.Deliver(RicochetRequest(10, 1), Shallow(45f));

            Assert.That(outcome.Hit, Is.True);
            Assert.That(outcome.RaycastHit.collider.gameObject, Is.SameAs(floor));
            Assert.That(target.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void TheBouncesRunOutAtTheCeiling()
        {
            CreateFloor();
            var ceiling = CreateFloor();
            ceiling.transform.position = new Vector3(0f, 1.5f, 0f);
            Physics.SyncTransforms();
            var continued = 0;
            void Count(ShotFiredInfo _) => continued++;
            CombatEvents.ShotContinued += Count;
            try
            {
                var outcome = HitscanDelivery.Instance.Deliver(RicochetRequest(10, 1), Shallow(10f));

                Assert.That(continued, Is.EqualTo(1));
                Assert.That(outcome.RaycastHit.collider.gameObject, Is.SameAs(ceiling));
            }
            finally
            {
                CombatEvents.ShotContinued -= Count;
            }
        }

        private static ShotRequest RicochetRequest(int damage, int maxBounces)
        {
            var round = new RoundSpec(ricochet: new RicochetSpec(maxBounces, 20f, 0.5f));
            return new ShotRequest(null, null, null, round, new ImpactSpec(damage), "test-ricochet", Physics.DefaultRaycastLayers);
        }

        private static PlannedRound Shallow(float degrees)
        {
            var direction = Quaternion.Euler(degrees, 0f, 0f) * Vector3.forward;
            return new PlannedRound(_lowMuzzle.ToNumerics(), direction.ToNumerics(), 0);
        }

        private GameObject CreateFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(floor);
            floor.name = "floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(20f, 1f, 60f);
            Physics.SyncTransforms();
            return floor;
        }

        private static ShotRequest Request(int damage, int layerMask = Physics.DefaultRaycastLayers, float radius = 0f)
        {
            return new ShotRequest(null, null, null, new RoundSpec(radius: radius), new ImpactSpec(damage), "test-rifle", layerMask);
        }

        private static ShotRequest BeamRequest(GameObject shooter, int damage)
        {
            var round = new RoundSpec(RoundDelivery.Beam, 30f, beam: new BeamSpec(0.1f));
            return new ShotRequest(shooter, shooter.transform, _eye, round, new ImpactSpec(damage), "laser", Physics.DefaultRaycastLayers);
        }

        private static ShotRequest SprayRequest(GameObject shooter, int damage, System.Action<ShotOutcome> landed = null)
        {
            var round = new RoundSpec(RoundDelivery.Spray, 8f, spray: new SpraySpec(0.1f, 15f));
            var impact = new ImpactSpec(damage, statusEffect: new StatusEffectSpec(3f, 0.5f, 2));
            return new ShotRequest(
                shooter, shooter.transform, _eye, round, impact, "flamethrower", Physics.DefaultRaycastLayers, landed: landed);
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

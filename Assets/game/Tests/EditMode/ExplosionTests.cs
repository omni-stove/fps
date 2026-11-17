using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ExplosionTests
    {
        private const string WeaponId = "launcher";

        private static readonly ExplosionSpec _spec = new ExplosionSpec(GrenadeSpec.Default.Explosion, 50);

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            CombatEvents.ClearSubscribers();
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
        public void DetonateDamagesAndKillsTargetsInTheRadius()
        {
            var near = CreateTarget("Near", new Vector3(0f, 1f, 1.5f));
            var far = CreateTarget("Far", new Vector3(0f, 1f, GrenadeSpec.Default.DamageRadius + 2f));

            var result = Explosion.Detonate(new Vector3(0f, 0.1f, 0f), _spec, null, WeaponId);

            Assert.That(near.IsDead, Is.True);
            Assert.That(far.Health.Current, Is.EqualTo(far.Health.Max));
            Assert.That(result.Damaged, Is.EqualTo(1));
            Assert.That(result.Killed, Is.EqualTo(1));
        }

        [Test]
        public void DetonateDoesNotHitATargetBehindAWall()
        {
            var target = CreateTarget("Hidden", new Vector3(0f, 1f, 2.5f));
            CreateBox("Wall", new Vector3(0f, 1.5f, 1.2f), new Vector3(4f, 3f, 0.3f));

            var result = Explosion.Detonate(new Vector3(0f, 0.5f, 0f), _spec, null, WeaponId);

            Assert.That(target.Health.Current, Is.EqualTo(target.Health.Max));
            Assert.That(result.Damaged, Is.Zero);
        }

        [Test]
        public void APersonInBetweenDoesNotShieldTheOneBehind()
        {
            var front = CreateTarget("Front", new Vector3(0f, 1f, 1.2f));
            var back = CreateTarget("Back", new Vector3(0f, 1f, 2.5f));

            var result = Explosion.Detonate(new Vector3(0f, 0.5f, 0f), _spec, null, WeaponId);

            Assert.That(front.Health.Current, Is.LessThan(front.Health.Max));
            Assert.That(back.Health.Current, Is.LessThan(back.Health.Max));
            Assert.That(result.Damaged, Is.EqualTo(2));
        }

        [Test]
        public void TheBlastReachesFeetSeenUnderARaisedBoard()
        {
            CreateFloor();
            CreateBox("Board", new Vector3(0f, 1.15f, 1.5f), new Vector3(4f, 1.3f, 0.2f));
            var target = CreateTarget("Behind", new Vector3(0f, 1f, 2f));

            var result = Explosion.Detonate(new Vector3(0f, 0.1f, -1f), _spec, null, WeaponId);

            Assert.That(target.Health.Current, Is.LessThan(target.Health.Max), "the line to the feet passes under the board");
            Assert.That(result.Damaged, Is.EqualTo(1));
        }

        [Test]
        public void AGapUnderALowWallLetsTheBlastReachACrouchingTargetItHides()
        {
            CreateFloor();
            CreateBox("Wall", new Vector3(0f, 0.675f, 1.2f), new Vector3(4f, 1.05f, 0.3f));
            var target = CreateTarget("Crouching", new Vector3(0f, 0.45f, 2f), 0.9f);

            var result = Explosion.Detonate(new Vector3(0f, 0.1f, -1f), _spec, null, WeaponId);

            Assert.That(target.Health.Current, Is.LessThan(target.Health.Max), "a 15 cm gap at the floor is enough for the line to the feet");
            Assert.That(result.Damaged, Is.EqualTo(1));
        }

        [Test]
        public void ALowWallDownToTheFloorHidesACrouchingTarget()
        {
            CreateFloor();
            CreateBox("Wall", new Vector3(0f, 0.6f, 1.2f), new Vector3(4f, 1.2f, 0.3f));
            var target = CreateTarget("Crouching", new Vector3(0f, 0.45f, 2f), 0.9f);

            var result = Explosion.Detonate(new Vector3(0f, 0.1f, -1f), _spec, null, WeaponId);

            Assert.That(target.Health.Current, Is.EqualTo(target.Health.Max));
            Assert.That(result.Damaged, Is.Zero);
        }

        [Test]
        public void DetonateHitsStructuresWithTheSpecDamage()
        {
            var structure = CreateBox("Hut", new Vector3(0f, 0.5f, 2f), Vector3.one).AddComponent<StructureHitRecorder>();

            var result = Explosion.Detonate(new Vector3(0f, 0.5f, 0f), _spec, null, WeaponId);

            Assert.That(structure.Calls.Count(), Is.EqualTo(1));
            Assert.That(structure.Calls[0].Damage, Is.GreaterThan(0).And.AtMost(_spec.StructureDamageAtCenter));
            Assert.That(result.StructureHits, Is.EqualTo(1));
        }

        [Test]
        public void DetonateRaisesExplodedWithTheWeaponId()
        {
            var explosions = new List<ExplosionInfo>();
            CombatEvents.Exploded += explosions.Add;
            var center = new Vector3(0f, 50f, 0f);

            var result = Explosion.Detonate(center, _spec, null, WeaponId);

            Assert.That(explosions.Count(), Is.EqualTo(1));
            Assert.That(explosions[0].Center, Is.EqualTo(center));
            Assert.That(explosions[0].WeaponId, Is.EqualTo(WeaponId));
            Assert.That(result.Center, Is.EqualTo(center));
        }

        [Test]
        public void DetonateRejectsAMissingSpec()
        {
            Assert.Throws<ArgumentNullException>(() => Explosion.Detonate(Vector3.zero, null, null, WeaponId));
        }

        [Test]
        public void AnExplodingRoundHurtsTheTargetsNearWhereItHitLessWithDistance()
        {
            CreateBox("Floor", new Vector3(0f, -0.5f, 10f), new Vector3(20f, 1f, 40f));
            var near = CreateTarget("Near", new Vector3(0f, 1f, 11.5f));
            var far = CreateTarget("Far", new Vector3(0f, 1f, 14f));
            var explosions = new List<ExplosionInfo>();
            CombatEvents.Exploded += explosions.Add;

            FireExplodingRoundAt(new Vector3(0f, 0f, 10f), new ExplosionSpec(new ExplosionDamage(80, 6f)));

            var nearDamage = near.Health.Max - near.Health.Current;
            var farDamage = far.Health.Max - far.Health.Current;
            Assert.That(farDamage, Is.GreaterThan(0));
            Assert.That(nearDamage, Is.GreaterThan(farDamage));
            Assert.That(explosions.Count(), Is.EqualTo(1));
            Assert.That(explosions[0].WeaponId, Is.EqualTo(WeaponId));
            Assert.That(explosions[0].Center.y, Is.EqualTo(ImpactResolver.DetonationStandoff).Within(1e-4f), "the center stands off the floor");
        }

        [Test]
        public void AnExplodingRoundDoesNotHitATargetBehindTheWallItHit()
        {
            var wall = CreateBox("Wall", new Vector3(0f, 1.5f, 10f), new Vector3(4f, 3f, 0.3f));
            var hidden = CreateTarget("Hidden", new Vector3(0f, 1f, 11f));
            var explosions = new List<ExplosionInfo>();
            CombatEvents.Exploded += explosions.Add;

            FireExplodingRoundAt(new Vector3(0f, 1.6f, 9.85f), new ExplosionSpec(new ExplosionDamage(80, 6f)));

            Assert.That(explosions.Count(), Is.EqualTo(1), "the round hit the wall and exploded");
            Assert.That(explosions[0].Center.z, Is.LessThan(wall.transform.position.z));
            Assert.That(hidden.Health.Current, Is.EqualTo(hidden.Health.Max));
        }

        private static void FireExplodingRoundAt(Vector3 point, ExplosionSpec explosion)
        {
            var origin = new Vector3(0f, 1.6f, 0f);
            var request = new ShotRequest(
                null, null, origin, new RoundSpec(range: 50f), new ImpactSpec(0, explosion: explosion), WeaponId, Physics.DefaultRaycastLayers);
            Hitscan.Fire(request, new PlannedRound(origin.ToNumerics(), (point - origin).ToNumerics(), 0));
        }

        private Damageable CreateTarget(string name, Vector3 position, float height = 2f)
        {
            var body = CreateBox(name, position, new Vector3(0.6f, height, 0.6f));
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(name, 100);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            return damageable;
        }

        private void CreateFloor()
        {
            CreateBox("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(20f, 0.1f, 20f));
        }

        private GameObject CreateBox(string name, Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(box);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = size;
            Physics.SyncTransforms();
            return box;
        }
    }
}

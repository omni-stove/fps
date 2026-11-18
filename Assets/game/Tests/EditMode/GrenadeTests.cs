using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode でも Physics の問い合わせはコライダーを見つけるので、生成後に SyncTransforms してから呼ぶ。
    public sealed class GrenadeTests
    {
        private static readonly GameRules _twoGrenades =
            GameRules.Default.WithActions(ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(2));

        private static readonly GrenadeSpec _smoke = new GrenadeSpec(
            "ecaf6e57-cb77-4707-9321-6ea5b659408f",
            "grenade.smoke",
            new ImpactSpec(damage: 0, areaEffect: new AreaEffectSpec(5f, 3f, 0.5f, 0, visualId: "smoke")),
            fuseSeconds: 1f);

        private static readonly GrenadeSpec _sticky = new GrenadeSpec(
            "5eb3292d-6300-4ec2-b325-6ad793017e49",
            "grenade.sticky",
            GrenadeSpec.Default.Impact,
            stickiness: GrenadeStickiness.SurfacesAndCharacters);

        private static readonly GrenadeSpec _stickyToSurfaces = new GrenadeSpec(
            "5eb3292d-6300-4ec2-b325-6ad793017e49",
            "grenade.sticky",
            GrenadeSpec.Default.Impact,
            stickiness: GrenadeStickiness.Surfaces);

        private static readonly GrenadeCatalog _fragAndSmoke =
            new GrenadeCatalog(new[] { GrenadeSpec.Default, _smoke }, new[] { GrenadeSpec.DefaultId, _smoke.Id });

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

            GameSignals.ResetAll();
        }

        [Test]
        public void ExplosionKillsANearbyTarget()
        {
            var target = CreateTarget("Near", new Vector3(0f, 1f, 1.5f));
            var grenade = SpawnGrenade(new Vector3(0f, 0.1f, 0f));

            var result = grenade.Explode();

            Assert.That(target.IsDead, Is.True);
            Assert.That(result.Damaged, Is.EqualTo(1));
            Assert.That(result.Killed, Is.EqualTo(1));
        }

        [Test]
        public void ExplosionDamagesLessFartherAway()
        {
            var target = CreateTarget("Mid", new Vector3(0f, 1f, 4.5f));
            var grenade = SpawnGrenade(new Vector3(0f, 1f, 0f));

            grenade.Explode();

            Assert.That(target.IsDead, Is.False);
            Assert.That(target.Health.Current, Is.LessThan(target.Health.Max));
        }

        [Test]
        public void ExplosionDoesNotReachBeyondTheRadius()
        {
            var target = CreateTarget("Far", new Vector3(0f, 1f, GrenadeSpec.Default.DamageRadius + 2f));
            var grenade = SpawnGrenade(new Vector3(0f, 1f, 0f));

            var result = grenade.Explode();

            Assert.That(target.Health.Current, Is.EqualTo(target.Health.Max));
            Assert.That(result.Damaged, Is.Zero);
        }

        [Test]
        public void ExplosionDoesNotHitATargetBehindAWall()
        {
            var target = CreateTarget("Hidden", new Vector3(0f, 1f, 2.5f));
            CreateBox("Wall", new Vector3(0f, 1.5f, 1.2f), new Vector3(4f, 3f, 0.3f));
            var grenade = SpawnGrenade(new Vector3(0f, 0.5f, 0f));

            var result = grenade.Explode();

            Assert.That(target.Health.Current, Is.EqualTo(target.Health.Max));
            Assert.That(result.Damaged, Is.Zero);
        }

        [Test]
        public void AnotherTargetInFrontDoesNotShield()
        {
            var front = CreateTarget("Front", new Vector3(0f, 1f, 1f));
            var back = CreateTarget("Back", new Vector3(0f, 1f, 2.5f));
            var grenade = SpawnGrenade(new Vector3(0f, 1f, 0f));

            grenade.Explode();

            Assert.That(front.Health.Current, Is.LessThan(front.Health.Max));
            Assert.That(back.Health.Current, Is.LessThan(back.Health.Max));
        }

        [Test]
        public void ExplosionHitsStructuresInTheRadius()
        {
            var structure = CreateBox("Hut", new Vector3(0f, 0.5f, 2f), Vector3.one).AddComponent<StructureHitRecorder>();
            var grenade = SpawnGrenade(new Vector3(0f, 0.5f, 0f));

            var result = grenade.Explode();

            Assert.That(structure.Calls.Count(), Is.EqualTo(1));
            Assert.That(structure.Calls[0].Damage, Is.GreaterThan(0));
            Assert.That(result.StructureHits, Is.EqualTo(1));
        }

        [Test]
        public void ExplodesWhenTheFuseRunsOutAndOnlyOnce()
        {
            var target = CreateTarget("Near", new Vector3(0f, 1f, 1f));
            var grenade = SpawnGrenade(new Vector3(0f, 1f, 0f));
            var explosions = 0;
            grenade.Exploded += (exploded, result) => explosions++;

            Assert.That(grenade.Tick(GrenadeSpec.Default.FuseSeconds - 0.1f), Is.False);
            Assert.That(target.IsDead, Is.False);
            Assert.That(grenade.Tick(0.2f), Is.True);

            Assert.That(explosions, Is.EqualTo(1));
            Assert.That(target.IsDead, Is.True);
            Assert.That(grenade == null, Is.True, "the grenade is removed after exploding");
        }

        [Test]
        public void ThrowerThrowsOnlyAsManyAsTheRulesAllow()
        {
            var thrower = CreateThrower(_twoGrenades);

            Assert.That(thrower.TryThrow(), Is.True);
            thrower.Tick(GrenadeSpec.Default.ThrowSeconds);
            Assert.That(thrower.TryThrow(), Is.True);
            thrower.Tick(GrenadeSpec.Default.ThrowSeconds);

            Assert.That(thrower.TryThrow(), Is.False, "a third grenade cannot be thrown");
            Assert.That(thrower.Remaining, Is.Zero);
        }

        [Test]
        public void ThrowerCannotThrowAgainWhileThrowing()
        {
            var thrower = CreateThrower(_twoGrenades);

            thrower.TryThrow();

            Assert.That(thrower.IsThrowing, Is.True);
            Assert.That(thrower.TryThrow(), Is.False);
            thrower.Tick(GrenadeSpec.Default.ThrowSeconds);
            Assert.That(thrower.IsThrowing, Is.False);
        }

        [Test]
        public void ThrowerThrowsNothingWhenGrenadesAreOff()
        {
            var thrower = CreateThrower(GameRules.Default.WithActions(ActionRules.Default.WithGrenadeCount(3)));

            Assert.That(thrower.UsesGrenades, Is.False);
            Assert.That(thrower.TryThrow(), Is.False);
        }

        [Test]
        public void ThrowerThrowsNothingBeforeItIsConfigured()
        {
            var body = CreateBox("Unconfigured", new Vector3(0f, 1f, 0f), Vector3.one);
            var thrower = body.AddComponent<GrenadeThrower>();

            Assert.That(thrower.TryThrow(), Is.False);
        }

        [Test]
        public void ThrowerSendsTheGrenadeForwardFromInFrontOfTheBody()
        {
            var thrower = CreateThrower(_twoGrenades);
            ThrownGrenade thrown = null;
            thrower.Thrown += grenade => thrown = grenade;

            thrower.TryThrow();

            Assert.That(thrown, Is.Not.Null);
            Assert.That(thrown.Thrower, Is.SameAs(thrower.gameObject));
            Assert.That(thrown.transform.position.z, Is.GreaterThan(thrower.transform.position.z));
            Assert.That(thrown.GetComponent<Rigidbody>().linearVelocity.z, Is.GreaterThan(0f));
            Assert.That(thrown.RemainingSeconds, Is.EqualTo(GrenadeSpec.Default.FuseSeconds));
        }

        [Test]
        public void ThrowerDefaultsToTheDefaultSpec()
        {
            var body = CreateBox("Defaults", new Vector3(0f, 1f, 0f), Vector3.one);
            var spec = body.AddComponent<GrenadeThrower>().Spec;

            Assert.That(spec.FuseSeconds, Is.EqualTo(GrenadeSpec.DefaultFuseSeconds));
            Assert.That(spec.ThrowSeconds, Is.EqualTo(GrenadeSpec.DefaultThrowSeconds));
            Assert.That(spec.ThrowSpeed, Is.EqualTo(GrenadeSpec.DefaultThrowSpeed));
            Assert.That(spec.ThrowLiftSpeed, Is.EqualTo(GrenadeSpec.DefaultThrowLiftSpeed));
            Assert.That(spec.MaxDamage, Is.EqualTo(GrenadeSpec.DefaultMaxDamage));
            Assert.That(spec.DamageRadius, Is.EqualTo(GrenadeSpec.DefaultDamageRadius));
            Assert.That(spec.FullDamageRadius, Is.EqualTo(GrenadeSpec.DefaultFullDamageRadius));
            Assert.That(spec.StructureDamageAtCenter, Is.EqualTo(GrenadeSpec.DefaultStructureDamageAtCenter));
        }

        [Test]
        public void ThrowerThrowsWithTheAppliedSpec()
        {
            var thrower = CreateThrower(_twoGrenades);
            thrower.ApplySpec(new GrenadeSpec(fuseSeconds: 1f, throwSeconds: 0.2f, maxDamage: 40, damageRadius: 3f, fullDamageRadius: 1f));
            ThrownGrenade thrown = null;
            thrower.Thrown += grenade => thrown = grenade;

            thrower.TryThrow();

            Assert.That(thrown.RemainingSeconds, Is.EqualTo(1f));
            Assert.That(thrown.Explosion.MaxDamage, Is.EqualTo(40));
            Assert.That(thrown.Explosion.Radius, Is.EqualTo(3f));
            Assert.That(thrown.Explosion.FullDamageRadius, Is.EqualTo(1f));
            thrower.Tick(0.2f);
            Assert.That(thrower.IsThrowing, Is.False);
            Assert.That(thrower.CreateSpec().MaxDamage, Is.EqualTo(40), "the applied values are the serialized ones");
        }

        [Test]
        public void LowerMaxDamageHurtsTheTargetLess()
        {
            var target = CreateTarget("Near", new Vector3(0f, 1f, 1f));
            var spec = new GrenadeSpec(maxDamage: 30);
            var grenade = SpawnGrenade(new Vector3(0f, 1f, 0f), spec);

            grenade.Explode();

            Assert.That(target.Health.Current, Is.EqualTo(target.Health.Max - 30));
        }

        [Test]
        public void ThrowerRejectsInvalidValuesBeforeChangingAnything()
        {
            var body = CreateBox("Invalid", new Vector3(0f, 1f, 0f), Vector3.one);
            var thrower = body.AddComponent<GrenadeThrower>();
            var serialized = new UnityEditor.SerializedObject(thrower);
            serialized.FindProperty("_fullDamageRadius").floatValue = GrenadeSpec.DefaultDamageRadius;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<System.ArgumentOutOfRangeException>(() => thrower.Configure(_twoGrenades));
            Assert.That(thrower.UsesGrenades, Is.False);
            Assert.That(thrower.Remaining, Is.Zero);
        }

        [Test]
        public void ThrowerRefillsWhenTheBodyRespawns()
        {
            var thrower = CreateThrower(_twoGrenades);
            thrower.TryThrow();

            thrower.GetComponent<Damageable>().ResetHealth();

            Assert.That(thrower.Remaining, Is.EqualTo(2));
            Assert.That(thrower.IsThrowing, Is.False);
        }

        [Test]
        public void ThrowerDoesNotThrowWhileDeadOrBlocked()
        {
            var thrower = CreateThrower(_twoGrenades);
            var blocked = true;
            thrower.ThrowBlocker = () => blocked;

            Assert.That(thrower.TryThrow(), Is.False);
            blocked = false;
            thrower.GetComponent<Damageable>().Kill();
            Assert.That(thrower.TryThrow(), Is.False);
            Assert.That(thrower.Remaining, Is.EqualTo(2));
        }

        [Test]
        public void ThrowerReportsChangesToTheCount()
        {
            var thrower = CreateThrower(_twoGrenades);
            var changes = 0;
            thrower.Changed += _ => changes++;

            thrower.TryThrow();
            thrower.Refill();

            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void ThrowerSwitchesBetweenTheCarriedTypes()
        {
            var thrower = CreateThrower(_twoGrenades);
            thrower.ApplyCatalog(_fragAndSmoke);
            var changes = 0;
            thrower.Changed += _ => changes++;

            Assert.That(thrower.Spec, Is.SameAs(GrenadeSpec.Default));
            Assert.That(thrower.SelectNext(), Is.True);

            Assert.That(thrower.Spec, Is.SameAs(_smoke));
            Assert.That(thrower.Remaining, Is.EqualTo(2));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingOneTypeLeavesTheOthers()
        {
            var thrower = CreateThrower(_twoGrenades);
            thrower.ApplyCatalog(_fragAndSmoke);
            thrower.SelectNext();
            ThrownGrenade thrown = null;
            thrower.Thrown += grenade => thrown = grenade;

            Assert.That(thrower.TryThrow(), Is.True);

            Assert.That(thrown.TypeId, Is.EqualTo(_smoke.Id));
            Assert.That(thrown.RemainingSeconds, Is.EqualTo(_smoke.FuseSeconds));
            Assert.That(thrower.Remaining, Is.EqualTo(1));
            Assert.That(thrower.Inventory.RemainingAt(0), Is.EqualTo(2), "the frags are untouched");
        }

        [Test]
        public void OneTypeCannotBeSwitched()
        {
            var thrower = CreateThrower(_twoGrenades);

            Assert.That(thrower.SelectNext(), Is.False);
            Assert.That(thrower.Spec.Id, Is.EqualTo(GrenadeSpec.DefaultId));
        }

        [Test]
        public void ASmokeGrenadeLeavesAnAreaThatEndsAfterItsDuration()
        {
            var target = CreateTarget("Near", new Vector3(0f, 1f, 1f));
            var grenade = ThrownGrenade.Spawn(null, new Vector3(0f, 0.1f, 0f), Vector3.zero, _smoke, null);
            _created.Add(grenade.gameObject);
            Physics.SyncTransforms();

            var result = grenade.Explode();

            var zone = Object.FindObjectsByType<AreaEffectZone>().Single();
            _created.Add(zone.gameObject);
            Assert.That(result.Damaged, Is.Zero, "smoke does not explode");
            Assert.That(target.Health.Current, Is.EqualTo(target.Health.Max));
            Assert.That(zone.Radius, Is.EqualTo(_smoke.Impact.AreaEffect.Radius));
            Assert.That(zone.KindId, Is.EqualTo("smoke"));
            Assert.That(zone.Spec, Is.SameAs(_smoke.Impact.AreaEffect));
            Assert.That(zone.transform.Find(AreaEffectZone.VisualName), Is.Not.Null, "a stand-in shape is built without a visual");

            var ended = 0;
            zone.Ended += _ => ended++;
            Assert.That(zone.Tick(_smoke.Impact.AreaEffect.DurationSeconds - 0.5f), Is.False);
            Assert.That(zone.Tick(0.5f), Is.True);
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(zone == null, Is.True, "the area is removed when it ends");
        }

        [Test]
        public void GrenadeCountStatusComparesByValue()
        {
            Assert.That(new GrenadeCountStatus(true, 2, "a"), Is.EqualTo(new GrenadeCountStatus(true, 2, "a")));
            Assert.That(new GrenadeCountStatus(true, 2, "a"), Is.Not.EqualTo(new GrenadeCountStatus(true, 2, "b")));
            Assert.That(new GrenadeCountStatus(true, 2, null), Is.EqualTo(new GrenadeCountStatus(true, 2, string.Empty)));
            Assert.That(new GrenadeCountStatus(true, 2, null).HasType, Is.False);
            Assert.That(new GrenadeCountStatus(true, -4, null).Remaining, Is.Zero);
            Assert.That(default(GrenadeCountStatus).TypeId, Is.Empty);
        }

        [Test]
        public void AStickyGrenadeStaysWhereItTouchesAWall()
        {
            var wall = CreateBox("Wall", new Vector3(0f, 1.5f, 3f), new Vector3(4f, 3f, 0.2f)).GetComponent<Collider>();
            var grenade = SpawnSticky(_sticky, new Vector3(0f, 1f, 2f));

            Assert.That(grenade.StickTo(wall, new Vector3(0f, 1f, 2.9f), Vector3.back), Is.True);

            Assert.That(grenade.IsStuck, Is.True);
            Assert.That(grenade.StuckTo, Is.SameAs(wall.transform));
            Assert.That(grenade.GetComponent<Rigidbody>().isKinematic, Is.True, "it does not bounce or fall");
            Assert.That(grenade.transform.position, Is.EqualTo(new Vector3(0f, 1f, 2.9f - _sticky.BodyRadius)).Using(Vector3EqualityComparer.Instance));
            Assert.That(grenade.StickTo(wall, Vector3.zero, Vector3.up), Is.False, "it sticks only to the first thing");
        }

        [Test]
        public void AStickyGrenadeMovesWithTheTargetItHitsAndGoesOffOnIt()
        {
            var target = CreateTarget("Moving", new Vector3(0f, 1f, 3f));
            var grenade = SpawnSticky(_sticky, new Vector3(0f, 1f, 2f));
            Assert.That(grenade.StickTo(target.GetComponent<Collider>(), new Vector3(0f, 1f, 2.7f), Vector3.back), Is.True);
            var offset = grenade.transform.position - target.transform.position;

            target.transform.position += new Vector3(5f, 0f, 0f);
            grenade.FollowCarrier();

            Assert.That(grenade.transform.position - target.transform.position, Is.EqualTo(offset).Using(Vector3EqualityComparer.Instance));
            Physics.SyncTransforms();
            Assert.That(grenade.Tick(_sticky.FuseSeconds), Is.True);
            Assert.That(target.IsDead, Is.True);
        }

        [Test]
        public void AGrenadeThatSticksOnlyToSurfacesDoesNotStickToATarget()
        {
            var target = CreateTarget("Target", new Vector3(0f, 1f, 3f));
            var grenade = SpawnSticky(_stickyToSurfaces, new Vector3(0f, 1f, 2f));

            Assert.That(grenade.StickTo(target.GetComponent<Collider>(), new Vector3(0f, 1f, 2.7f), Vector3.back), Is.False);
            Assert.That(grenade.IsStuck, Is.False);
        }

        [Test]
        public void AFragGrenadeDoesNotStick()
        {
            var wall = CreateBox("Wall", new Vector3(0f, 1.5f, 3f), new Vector3(4f, 3f, 0.2f)).GetComponent<Collider>();
            var grenade = SpawnGrenade(new Vector3(0f, 1f, 2f));

            Assert.That(grenade.StickTo(wall, new Vector3(0f, 1f, 2.9f), Vector3.back), Is.False);
        }

        [Test]
        public void AGrenadeFallsOffWhatItWasStuckToWhenThatGoesAway()
        {
            var crate = CreateBox("Crate", new Vector3(0f, 0.5f, 3f), Vector3.one);
            var grenade = SpawnSticky(_sticky, new Vector3(0f, 1.5f, 3f));
            grenade.StickTo(crate.GetComponent<Collider>(), new Vector3(0f, 1f, 3f), Vector3.up);

            crate.SetActive(false);
            grenade.FollowCarrier();

            Assert.That(grenade.IsStuck, Is.False);
            Assert.That(grenade.GetComponent<Rigidbody>().isKinematic, Is.False);
            Assert.That(grenade.GetComponent<Collider>().enabled, Is.True);
        }

        private ThrownGrenade SpawnSticky(GrenadeSpec spec, Vector3 position)
        {
            var grenade = ThrownGrenade.Spawn(null, position, Vector3.zero, spec, null);
            _created.Add(grenade.gameObject);
            Physics.SyncTransforms();
            return grenade;
        }

        private GrenadeThrower CreateThrower(GameRules rules)
        {
            var body = CreateBox("Thrower", new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 1f));
            body.AddComponent<Damageable>().Initialize("Thrower", 100);
            var thrower = body.AddComponent<GrenadeThrower>();
            thrower.Configure(rules);
            thrower.Thrown += grenade => _created.Add(grenade.gameObject);
            return thrower;
        }

        private ThrownGrenade SpawnGrenade(Vector3 position, GrenadeSpec spec = null)
        {
            spec = spec ?? GrenadeSpec.Default;
            var grenade = ThrownGrenade.Spawn(
                null, position, Vector3.zero, spec.FuseSeconds, spec.Explosion, spec.StructureDamageAtCenter);
            _created.Add(grenade.gameObject);
            Physics.SyncTransforms();
            return grenade;
        }

        private Damageable CreateTarget(string name, Vector3 position)
        {
            var body = CreateBox(name, position, new Vector3(0.6f, 2f, 0.6f));
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(name, 100);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            return damageable;
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

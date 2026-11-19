using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 窓の開いた乗り物を、車体の下半分と屋根の 2 つの箱で作る。間 (高さ 0.8〜1.6 m) が窓で、乗っている人の体がそこから見える。
    public sealed class VehicleDamageTests
    {
        private readonly List<Object> _created = new List<Object>();
        private Vehicle _vehicle;
        private BoxCollider _lower;
        private VehicleTestRider _rider;
        private Damageable _riderBody;

        [SetUp]
        public void SetUp()
        {
            var root = Track(new GameObject("Car"));
            _lower = AddBox(root.transform, "Lower", new Vector3(0f, 0.4f, 0f), new Vector3(2f, 0.8f, 4f));
            AddBox(root.transform, "Roof", new Vector3(0f, 1.7f, 0f), new Vector3(2f, 0.2f, 4f));
            var engine = AddBox(root.transform, "Engine", new Vector3(0f, 0.4f, 2.1f), new Vector3(1.6f, 0.6f, 0.4f));
            engine.gameObject.AddComponent<VehiclePart>().PartId = "engine";

            var seat = new GameObject("Driver").AddComponent<VehicleSeat>();
            seat.transform.SetParent(root.transform, false);
            seat.transform.localPosition = new Vector3(0f, 0.1f, 0f);

            _vehicle = root.AddComponent<Vehicle>();
            _vehicle.Configure(new VehicleSpec(
                "car",
                1000,
                new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) },
                parts: new[] { new VehiclePartSpec("engine", 1.5f) }));

            _rider = NewRider("Crew", new Vector3(0f, 1f, 0f), 200, out _riderBody);
            _vehicle.TryEnter(_rider, 0, requireInRange: false);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheVehicleGetsItsOwnDamageableAndHitboxThatReportNoKills()
        {
            var damageable = _vehicle.Damageable;

            Assert.That(damageable, Is.Not.Null);
            Assert.That(damageable, Is.SameAs(_vehicle.GetComponent<Damageable>()));
            Assert.That(damageable.ReportsKills, Is.False);
            Assert.That(damageable.Health.Max, Is.EqualTo(1000));
            Assert.That(damageable.Health.Current, Is.EqualTo(1000));
            Assert.That(_vehicle.GetComponent<Hitbox>().Owner, Is.SameAs(damageable));
            Assert.That(Hitbox.FindFor(_lower).Owner, Is.SameAs(damageable));
            Assert.That(_vehicle.DamageStage, Is.EqualTo(VehicleDamageStage.Intact));
        }

        [Test]
        public void AnUnconfiguredVehicleTakesItsHealthFromTheInspector()
        {
            var root = Track(new GameObject("Turret"));
            new GameObject("Gun").AddComponent<VehicleSeat>().transform.SetParent(root.transform, false);

            var turret = root.AddComponent<Vehicle>();

            Assert.That(turret.Damageable.Health.Max, Is.EqualTo(VehicleSpec.DefaultMaxHealth));
        }

        [Test]
        public void APartTakesItsMultiplierAndAnUnknownPartWarnsAndTakesDamageUnscaled()
        {
            var engine = _vehicle.transform.Find("Engine").GetComponent<Hitbox>();
            Assert.That(engine.DamageMultiplier, Is.EqualTo(1.5f));
            Assert.That(engine.Owner, Is.SameAs(_vehicle.Damageable));

            var wheel = AddBox(_vehicle.transform, "Wheel", new Vector3(1.1f, 0.3f, 1.5f), new Vector3(0.2f, 0.6f, 0.6f));
            wheel.gameObject.AddComponent<VehiclePart>().PartId = "wheel";

            LogAssert.Expect(LogType.Warning, "[Vehicle] 'Car' part 'wheel' is not in the spec; it takes damage unscaled");
            _vehicle.Configure(_vehicle.Spec);

            Assert.That(wheel.GetComponent<Hitbox>().DamageMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void ABulletThroughTheWindowHitsTheRider()
        {
            var result = Hitscan.Fire(new Vector3(-5f, 1.2f, 0f), Vector3.right, 50f, 40, DamageFalloff.None, null);

            Assert.That(result.Hit, Is.True);
            Assert.That(result.Hitbox.Owner, Is.SameAs(_riderBody));
            Assert.That(_riderBody.Health.Current, Is.EqualTo(160));
            Assert.That(_vehicle.Damageable.Health.Current, Is.EqualTo(1000));
        }

        [Test]
        public void ABulletOnTheHullHitsTheVehicleNotTheRider()
        {
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' damaged dmg=40 hp=960/1000");
            var result = Hitscan.Fire(new Vector3(-5f, 0.4f, 0f), Vector3.right, 50f, 40, DamageFalloff.None, null);

            Assert.That(result.Hitbox.Owner, Is.SameAs(_vehicle.Damageable));
            Assert.That(result.Damage, Is.EqualTo(40));
            Assert.That(_vehicle.Damageable.Health.Current, Is.EqualTo(960));
            Assert.That(_riderBody.Health.Current, Is.EqualTo(200));
        }

        [Test]
        public void ABulletOnTheEngineTakesThePartMultiplier()
        {
            var result = Hitscan.Fire(new Vector3(0f, 0.4f, 6f), Vector3.back, 50f, 40, DamageFalloff.None, null);

            Assert.That(result.Damage, Is.EqualTo(60));
            Assert.That(_vehicle.Damageable.Health.Current, Is.EqualTo(940));
        }

        [Test]
        public void ABulletOnAPartReportsThePartOnceEvenAfterReconfiguring()
        {
            var parts = new List<string>();
            var amounts = new List<int>();
            _vehicle.PartDamaged += (vehicle, part, damage) =>
            {
                parts.Add(part.PartId);
                amounts.Add(damage);
            };
            _vehicle.Configure(_vehicle.Spec);

            Hitscan.Fire(new Vector3(0f, 0.4f, 6f), Vector3.back, 50f, 40, DamageFalloff.None, null);
            Hitscan.Fire(new Vector3(-5f, 0.4f, 0f), Vector3.right, 50f, 40, DamageFalloff.None, null);
            _vehicle.Damageable.TakeDamage(100);

            Assert.That(parts, Is.EqualTo(new[] { "engine" }), "the hull and damage that skips the parts are not reported");
            Assert.That(amounts, Is.EqualTo(new[] { 60 }), "after the part multiplier");
        }

        [Test]
        public void AClosedHullShieldsTheRiderFromABlastOutside()
        {
            var root = Track(new GameObject("Tank"));
            AddBox(root.transform, "Hull", new Vector3(0f, 1.5f, 10f), new Vector3(3f, 3f, 3f));
            new GameObject("Seat").AddComponent<VehicleSeat>().transform.SetParent(root.transform, false);
            var tank = root.AddComponent<Vehicle>();
            tank.Configure(new VehicleSpec("tank", 1000, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver, VehicleSeatPose.Hidden) }));
            var crew = NewRider("TankCrew", new Vector3(0f, 1f, 10f), 200, out var crewBody);
            tank.TryEnter(crew, 0, requireInRange: false);
            Physics.SyncTransforms();

            Explosion.Detonate(new Vector3(-4f, 1f, 10f), new ExplosionSpec(new ExplosionDamage(100, 6f, 1f)), null, "grenade");

            Assert.That(crewBody.Health.Current, Is.EqualTo(200), "the hull is in the way");
            Assert.That(tank.Damageable.Health.Current, Is.EqualTo(930), "2.5 m from the hull: 100 * (1 - 1.5 / 5)");
        }

        [Test]
        public void ShootingItDownSmokesThenBurnsThenExplodesHurtingTheRiderAndLeavesAWreck()
        {
            var stages = new List<VehicleDamageStage>();
            var blasts = new List<GrenadeExplosion>();
            _vehicle.DamageStageChanged += (_, stage) => stages.Add(stage);
            _vehicle.Exploded += (_, blast) => blasts.Add(blast);
            var body = _vehicle.Damageable;

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' damaged dmg=500 hp=500/1000");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' stage=Smoking hp=500/1000");
            body.TakeDamage(500);
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' damaged dmg=300 hp=200/1000");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' stage=Burning hp=200/1000");
            body.TakeDamage(300);

            Assert.That(_vehicle.TickDurability(1f), Is.EqualTo(25), "burns 25 a second by default");
            Assert.That(body.Health.Current, Is.EqualTo(175));
            Assert.That(_vehicle.HasExploded, Is.False);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=Car");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Vehicle\] 'Car' exploded at=\(.+\) riders=1 damaged=1 killed=0$"));
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' wrecked riders=1");
            Assert.That(_vehicle.TickDurability(7f), Is.EqualTo(175));

            Assert.That(stages, Is.EqualTo(new[] { VehicleDamageStage.Smoking, VehicleDamageStage.Burning, VehicleDamageStage.Destroyed }));
            Assert.That(_vehicle.HasExploded, Is.True);
            Assert.That(_vehicle.IsWrecked, Is.True);
            Assert.That(_vehicle.DamageStage, Is.EqualTo(VehicleDamageStage.Destroyed));
            Assert.That(blasts.Count, Is.EqualTo(1));
            Assert.That(blasts[0].Damaged, Is.EqualTo(1));
            Assert.That(_riderBody.Health.Current, Is.EqualTo(50), "the rider sits in the full-damage radius: 200 - 150");
            Assert.That(_rider.Seat, Is.Null, "the survivor is thrown out");
            Assert.That(_vehicle != null, Is.True, "the wreck stays");
            Assert.That(_lower.enabled, Is.True, "the wreck keeps its colliders");
            Assert.That(_vehicle.TickDurability(1f), Is.Zero, "a wreck no longer burns");
        }

        [Test]
        public void AFireKeepsTheAttackerAsTheKillerOfTheRiders()
        {
            var attacker = Track(new GameObject("Attacker"));
            _riderBody.MaxHealth = 100;
            _riderBody.ResetHealth();
            var kills = new List<KillFeedEntry>();
            void OnKilled(KillFeedEntry entry) => kills.Add(entry);
            CombatEvents.Killed += OnKilled;
            try
            {
                using (CombatEvents.Attribute(attacker, "rifle"))
                {
                    _vehicle.Damageable.TakeDamage(800);
                }

                _vehicle.TickDurability(10f);
            }
            finally
            {
                CombatEvents.Killed -= OnKilled;
            }

            Assert.That(kills.Count, Is.EqualTo(1), "only the rider; the vehicle is not a kill");
            Assert.That(kills[0].VictimId, Is.EqualTo(CombatEvents.OfflineId(_riderBody.gameObject)));
            Assert.That(kills[0].KillerId, Is.EqualTo(CombatEvents.OfflineId(attacker)));
            Assert.That(kills[0].WeaponId, Is.EqualTo(Vehicle.ExplosionWeaponId));
        }

        [Test]
        public void TickingRejectsBadTimeAndDoesNothingBeforeTheFire()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _vehicle.TickDurability(-1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _vehicle.TickDurability(float.NaN));
            Assert.That(_vehicle.TickDurability(10f), Is.Zero);
            Assert.That(_vehicle.Damageable.Health.Current, Is.EqualTo(1000));
        }

        [Test]
        public void RepairingPutsTheStageBack()
        {
            var stages = new List<VehicleDamageStage>();
            _vehicle.DamageStageChanged += (_, stage) => stages.Add(stage);
            _vehicle.Damageable.TakeDamage(600);

            _vehicle.Damageable.ResetHealth();

            Assert.That(stages, Is.EqualTo(new[] { VehicleDamageStage.Smoking, VehicleDamageStage.Intact }));
            Assert.That(_vehicle.DamageStage, Is.EqualTo(VehicleDamageStage.Intact));
        }

        [Test]
        public void TheDefinitionsBurnRateDrivesTheFire()
        {
            var definition = ScriptableObject.CreateInstance<VehicleDefinition>();
            _created.Add(definition);
            definition.Id = "car";
            definition.MaxHealth = 100;
            definition.Parts = new[] { new VehicleDefinition.Part("engine", 1.5f) };
            definition.BurnDamagePerSecond = 10f;
            _vehicle.Configure(definition);

            _vehicle.Damageable.TakeDamage(80);

            Assert.That(_vehicle.DamageStage, Is.EqualTo(VehicleDamageStage.Burning));
            Assert.That(_vehicle.TickDurability(0.5f), Is.EqualTo(5));
        }

        [Test]
        public void TheEffectsFollowTheStagesAndTintTheWreckWithoutAWreckModel()
        {
            var effects = _vehicle.gameObject.AddComponent<VehicleDamageEffects>();
            effects.SmokePrefab = Track(new GameObject("SmokeTemplate"));
            effects.FirePrefab = Track(new GameObject("FireTemplate"));
            var renderer = Track(GameObject.CreatePrimitive(PrimitiveType.Cube)).GetComponent<Renderer>();
            renderer.transform.SetParent(_vehicle.transform, false);
            renderer.GetComponent<Collider>().enabled = false;
            effects.Attach(_vehicle);

            Assert.That(effects.SmokeInstance, Is.Null);

            _vehicle.Damageable.TakeDamage(500);
            Assert.That(effects.SmokeInstance, Is.Not.Null);
            Assert.That(effects.SmokeInstance.transform.parent, Is.SameAs(_vehicle.transform));
            Assert.That(effects.FireInstance, Is.Null);

            _vehicle.Damageable.TakeDamage(300);
            Assert.That(effects.FireInstance, Is.Not.Null);

            _vehicle.Damageable.TakeDamage(200);
            Assert.That(effects.FireInstance == null, Is.True, "the fire goes out in the blast");
            Assert.That(effects.SmokeInstance, Is.Not.Null, "the wreck keeps smoking");
            Assert.That(effects.IsWreckShown, Is.True);
            Assert.That(effects.WreckInstance, Is.Null);
            Assert.That(renderer.HasPropertyBlock(), Is.True, "tinted without touching the shared material");
            Assert.That(renderer.enabled, Is.True);
        }

        [Test]
        public void AWreckModelReplacesTheVehiclesLook()
        {
            var effects = _vehicle.gameObject.AddComponent<VehicleDamageEffects>();
            var wreckTemplate = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            wreckTemplate.GetComponent<Collider>().enabled = false;
            effects.WreckPrefab = wreckTemplate;
            var look = Track(GameObject.CreatePrimitive(PrimitiveType.Cube)).GetComponent<Renderer>();
            look.transform.SetParent(_vehicle.transform, false);
            look.GetComponent<Collider>().enabled = false;
            effects.Attach(_vehicle);

            _vehicle.Damageable.TakeDamage(1000);

            Assert.That(effects.WreckInstance, Is.Not.Null);
            Assert.That(effects.WreckInstance.transform.parent, Is.SameAs(_vehicle.transform));
            Assert.That(effects.WreckInstance.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(look.enabled, Is.False);
            Assert.That(effects.SmokeInstance, Is.Null, "no smoke model, no smoke");
        }

        [Test]
        public void TheEffectsShowTheCurrentStageWhenAttachedLateAndRejectNull()
        {
            _vehicle.Damageable.TakeDamage(500);
            var effects = _vehicle.gameObject.AddComponent<VehicleDamageEffects>();
            effects.SmokePrefab = Track(new GameObject("SmokeTemplate"));

            effects.Attach(_vehicle);

            Assert.That(effects.SmokeInstance, Is.Not.Null);
            Assert.That(effects.Vehicle, Is.SameAs(_vehicle));
            Assert.Throws<System.ArgumentNullException>(() => effects.Attach(null));
        }

        private VehicleTestRider NewRider(string name, Vector3 position, int health, out Damageable body)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            body = go.AddComponent<Damageable>();
            body.Initialize(name.ToLowerInvariant(), health);
            go.AddComponent<Hitbox>();
            return go.AddComponent<VehicleTestRider>();
        }

        private static BoxCollider AddBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var box = new GameObject(name).AddComponent<BoxCollider>();
            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            box.size = size;
            return box;
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }
    }
}

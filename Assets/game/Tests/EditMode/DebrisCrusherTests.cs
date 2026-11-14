using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 崩れた破片の当たり判定 (<see cref="DestructibleStructure.DebrisCrushEnabled"/>・<see cref="DebrisCrusher"/>) を確かめる。
    /// 物理は進めないので、破片が当たったことは <see cref="DebrisCrusher.TryCrush"/> を直接呼んで表す。
    /// 他のテストの物と重ならないよう、原点から離して置く。
    /// </summary>
    public sealed class DebrisCrusherTests
    {
        private const string WeaponId = "rifle";
        private static readonly Vector3 _origin = new Vector3(260f, 0f, 200f);

        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly List<KillFeedEntry> _kills = new List<KillFeedEntry>();
        private DebrisPool _pool;

        [SetUp]
        public void SetUp()
        {
            var poolObject = new GameObject("TestDebrisPool");
            _created.Add(poolObject);
            _pool = poolObject.AddComponent<DebrisPool>();
            CombatEvents.Killed += _kills.Add;
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.Killed -= _kills.Add;
            _kills.Clear();
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
        public void StructuresLeaveCrushingOffByDefault()
        {
            var structure = new GameObject("Defaults").AddComponent<DestructibleStructure>();
            _created.Add(structure.gameObject);

            Assert.That(structure.DebrisCrushEnabled, Is.False);
            Assert.That(structure.CrushDamage.MinSpeed, Is.EqualTo(DebrisCrushDamage.DefaultMinSpeed));
            Assert.That(structure.CrushDamage.DamagePerMomentum, Is.EqualTo(DebrisCrushDamage.DefaultDamagePerMomentum));
            Assert.That(structure.CrushDamage.MaxDamage, Is.EqualTo(DebrisCrushDamage.DefaultMaxDamage));
            Assert.Throws<ArgumentNullException>(() => structure.CrushDamage = null);
        }

        [Test]
        public void DebrisOfAStructureWithCrushingOffIsHarmless()
        {
            var structure = CreateColumn("Off", crush: false);
            var attacker = CreateObject("Attacker");

            using (CombatEvents.Attribute(attacker, WeaponId))
            {
                Hit(structure, 0);
            }

            Assert.That(_pool.Count, Is.GreaterThan(0));
            Assert.That(_pool.GetComponentsInChildren<DebrisCrusher>(), Is.Empty);
        }

        [Test]
        public void DebrisOfAStructureWithCrushingOnCarriesWhoBrokeIt()
        {
            var structure = CreateColumn("On", crush: true);
            structure.CrushDamage = new DebrisCrushDamage(1f, 0.5f, 80);
            var attacker = CreateObject("Attacker");

            using (CombatEvents.Attribute(attacker, WeaponId))
            {
                Hit(structure, 0);
            }

            var crushers = _pool.GetComponentsInChildren<DebrisCrusher>();
            Assert.That(crushers, Has.Length.EqualTo(_pool.Count), "every piece of debris crushes");
            Assert.That(crushers.Length, Is.EqualTo(DestructibleStructure.FragmentsPerDestroyedPiece + 2), "fragments of the base and the two collapsed pieces");
            foreach (var crusher in crushers)
            {
                Assert.That(crusher.Attribution.HasValue, Is.True);
                Assert.That(crusher.Attribution.Value.KillerId, Is.EqualTo(CombatEvents.OfflineId(attacker)));
                Assert.That(crusher.Attribution.Value.WeaponId, Is.EqualTo(WeaponId));
                Assert.That(crusher.Source, Is.EqualTo("On"));
                Assert.That(crusher.Damage.MinSpeed, Is.EqualTo(1f));
                Assert.That(crusher.Damage.DamagePerMomentum, Is.EqualTo(0.5f));
                Assert.That(crusher.Damage.MaxDamage, Is.EqualTo(80));
            }
        }

        [Test]
        public void DebrisWithoutAnAttributionHasNone()
        {
            var structure = CreateColumn("Unattributed", crush: true);

            Hit(structure, 0);

            foreach (var crusher in _pool.GetComponentsInChildren<DebrisCrusher>())
            {
                Assert.That(crusher.Attribution.HasValue, Is.False);
            }
        }

        [Test]
        public void SyncedDebrisNeverCrushes()
        {
            var structure = CreateColumn("Synced", crush: true);
            structure.HasAuthority = false;

            structure.ApplyDestroyedBitset(new[] { 0b111UL });

            Assert.That(_pool.Count, Is.GreaterThan(0));
            Assert.That(_pool.GetComponentsInChildren<DebrisCrusher>(), Is.Empty);
        }

        [Test]
        public void HitscanAtAStructureCarriesTheShootersAttribution()
        {
            var structure = CreateColumn("Shot", crush: true);
            var shooter = CreateObject("Shooter");
            shooter.transform.position = _origin + new Vector3(0f, 0.5f, -5f);
            Physics.SyncTransforms();

            Hitscan.Fire(shooter.transform.position, Vector3.forward, 20f, DestructiblePiece.DefaultMaxHealth, DamageFalloff.None, shooter.transform, weaponId: WeaponId);

            Assert.That(structure.Integrity.IsDestroyed(0), Is.True);
            var crushers = _pool.GetComponentsInChildren<DebrisCrusher>();
            Assert.That(crushers, Is.Not.Empty);
            foreach (var crusher in crushers)
            {
                Assert.That(crusher.Attribution.HasValue, Is.True);
                Assert.That(crusher.Attribution.Value.KillerId, Is.EqualTo(CombatEvents.OfflineId(shooter)));
                Assert.That(crusher.Attribution.Value.WeaponId, Is.EqualTo(WeaponId));
            }

            Assert.That(CombatEvents.CurrentAttribution.HasValue, Is.False, "the attribution is put back after the shot");
        }

        [Test]
        public void CrushingDealsEnvironmentDamageAndCreditsTheBreaker()
        {
            var attacker = CreateObject("Breaker");
            var attribution = new ShotAttribution(CombatEvents.OfflineId(attacker), WeaponId);
            var crusher = SpawnCrusher(DebrisCrushDamage.Default, attribution);
            var target = CreateTarget("Victim", 100);
            var reports = new List<DamageReport>();
            target.Damaged += (_, report) => reports.Add(report);

            LogAssert.Expect(
                LogType.Log,
                new Regex(@"^\[Destruction\] debris hit structure=Tower id=Victim dmg=150 speed=8\.0 mass=100\.0 by=\d+ hp=0$"));
            var dealt = crusher.TryCrush(target.GetComponent<Collider>(), 8f);

            // (8 - 2) m/s × 100 kg × 0.25
            Assert.That(dealt, Is.EqualTo(150));
            Assert.That(target.IsDead, Is.True);
            Assert.That(reports, Has.Count.EqualTo(1));
            Assert.That(reports[0].Kind, Is.EqualTo(DamageKind.Environment));
            Assert.That(reports[0].Amount, Is.EqualTo(150));
            Assert.That(reports[0].Zone.HasValue, Is.False);
            Assert.That(_kills, Has.Count.EqualTo(1));
            Assert.That(_kills[0].KillerId, Is.EqualTo(CombatEvents.OfflineId(attacker)));
            Assert.That(_kills[0].VictimId, Is.EqualTo(CombatEvents.OfflineId(target.gameObject)));
            Assert.That(_kills[0].WeaponId, Is.EqualTo(WeaponId));
            Assert.That(CombatEvents.CurrentAttribution.HasValue, Is.False, "the attribution is put back after the hit");
        }

        [Test]
        public void CrushingWithoutAnAttributionIsASuicide()
        {
            var crusher = SpawnCrusher(DebrisCrushDamage.Default, null);
            var target = CreateTarget("Alone", 10);

            crusher.TryCrush(target.GetComponent<Collider>(), 8f);

            Assert.That(_kills, Has.Count.EqualTo(1));
            Assert.That(_kills[0].IsSuicide, Is.True);
        }

        [Test]
        public void SlowHitsDoNothingAndEachTargetIsHurtOnce()
        {
            var crusher = SpawnCrusher(DebrisCrushDamage.Default, null);
            var target = CreateTarget("Sturdy", 1000);
            var collider = target.GetComponent<Collider>();

            Assert.That(crusher.TryCrush(collider, 1f), Is.EqualTo(0), "below the minimum speed");
            Assert.That(target.Health.Current, Is.EqualTo(1000));

            Assert.That(crusher.TryCrush(collider, 4f), Is.EqualTo(50));
            Assert.That(crusher.TryCrush(collider, 9f), Is.EqualTo(0), "the same debris does not hurt twice");
            Assert.That(target.Health.Current, Is.EqualTo(950));

            var other = SpawnCrusher(DebrisCrushDamage.Default, null);
            Assert.That(other.TryCrush(collider, 4f), Is.EqualTo(50), "another piece of debris still hurts");
        }

        [Test]
        public void IgnoresNonCharactersTheDeadAndInvalidSpeeds()
        {
            var crusher = SpawnCrusher(DebrisCrushDamage.Default, null);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            var dead = CreateTarget("Dead", 10);
            dead.Kill();

            Assert.That(crusher.TryCrush(null, 8f), Is.EqualTo(0));
            Assert.That(crusher.TryCrush(wall.GetComponent<Collider>(), 8f), Is.EqualTo(0));
            Assert.That(crusher.TryCrush(dead.GetComponent<Collider>(), 8f), Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => crusher.TryCrush(wall.GetComponent<Collider>(), -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => crusher.TryCrush(wall.GetComponent<Collider>(), float.NaN));
        }

        [Test]
        public void AnUninitializedCrusherDoesNothing()
        {
            var debris = CreateObject("Loose");
            debris.AddComponent<Rigidbody>();
            var crusher = debris.AddComponent<DebrisCrusher>();
            var target = CreateTarget("Bystander", 10);

            Assert.That(crusher.TryCrush(target.GetComponent<Collider>(), 8f), Is.EqualTo(0));
            Assert.Throws<ArgumentNullException>(() => crusher.Initialize(null, null, "x"));
        }

        [Test]
        public void PoolSpawnsCrushingDebrisOnlyWhenAsked()
        {
            var plain = _pool.Spawn(_origin, Quaternion.identity, Vector3.one, null, Vector3.zero);
            var crushing = _pool.Spawn(_origin, Quaternion.identity, Vector3.one, null, Vector3.zero, DebrisCrushDamage.Default, null, "S");

            Assert.That(plain.GetComponent<DebrisCrusher>(), Is.Null);
            Assert.That(crushing.GetComponent<DebrisCrusher>(), Is.Not.Null);
            Assert.That(crushing.GetComponent<DebrisCrusher>().Source, Is.EqualTo("S"));
        }

        [Test]
        public void AStoredAttributionCanBeRestoredAndPutBack()
        {
            var outer = CreateObject("Outer");
            var stored = new ShotAttribution(42, "stored");
            using (CombatEvents.Attribute(outer, "outer"))
            {
                using (CombatEvents.Attribute(stored))
                {
                    Assert.That(CombatEvents.CurrentAttribution.Value.KillerId, Is.EqualTo(42));
                    Assert.That(CombatEvents.CurrentAttribution.Value.WeaponId, Is.EqualTo("stored"));
                }

                Assert.That(CombatEvents.CurrentAttribution.Value.KillerId, Is.EqualTo(CombatEvents.OfflineId(outer)));

                using (CombatEvents.Attribute((ShotAttribution?)null))
                {
                    Assert.That(CombatEvents.CurrentAttribution.HasValue, Is.False);
                }
            }

            Assert.That(CombatEvents.CurrentAttribution.HasValue, Is.False);
        }

        [Test]
        public void CrushLogShowsADashWithoutAnAttribution()
        {
            Assert.That(
                DebrisCrusher.FormatCrushLog("Tower", "Enemy", 12, 3.25f, 2.7f, null, 88),
                Is.EqualTo("[Destruction] debris hit structure=Tower id=Enemy dmg=12 speed=3.3 mass=2.7 by=- hp=88"));
        }

        // 下から 0 (地面に接する)・1・2 と積んだ 1m の部品の柱。0 を壊すと 1 と 2 が崩れる。
        private DestructibleStructure CreateColumn(string name, bool crush)
        {
            var root = CreateObject(name);
            root.transform.position = _origin;
            for (var i = 0; i < 3; i++)
            {
                DestructiblePiece.Create(root.transform, "Piece" + i, new Vector3(0f, i + 0.5f, 0f), Vector3.one, i, grounded: i == 0, carveObstacle: false);
            }

            var structure = root.AddComponent<DestructibleStructure>();
            structure.DebrisPool = _pool;
            structure.DestructionEnabled = true;
            structure.DebrisCrushEnabled = crush;
            structure.Build();
            return structure;
        }

        private static void Hit(DestructibleStructure structure, int piece)
        {
            var target = structure.Pieces[piece];
            structure.ApplyStructureHit(target.Collider, target.transform.position, target.MaxHealth);
        }

        // 1m の破片 (質量 100kg) を出す。
        private DebrisCrusher SpawnCrusher(DebrisCrushDamage damage, ShotAttribution? attribution)
        {
            var debris = _pool.Spawn(_origin + Vector3.up * 10f, Quaternion.identity, Vector3.one, null, Vector3.zero, damage, attribution, "Tower");
            return debris.GetComponent<DebrisCrusher>();
        }

        private Damageable CreateTarget(string id, int maxHealth)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(target);
            target.name = id;
            target.transform.position = _origin + new Vector3(3f, 1f, 0f);
            var damageable = target.AddComponent<Damageable>();
            damageable.Initialize(id, maxHealth);
            target.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            return damageable;
        }

        private GameObject CreateObject(string name)
        {
            var gameObject = new GameObject(name);
            _created.Add(gameObject);
            return gameObject;
        }
    }
}

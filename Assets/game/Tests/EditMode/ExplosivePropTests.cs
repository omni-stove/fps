using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 爆発する置き物 (<see cref="ExplosiveProp"/>) が弾と爆発で耐久を減らし、0 で爆発して近くの人・建造物・ほかの置き物に被害を配り、
    /// ほかの置き物が遅れて連鎖することを確かめる。Edit Mode でも Physics の問い合わせはコライダーを見つけるので、生成後に SyncTransforms する。
    /// </summary>
    public sealed class ExplosivePropTests
    {
        // ほかのテストやシーンの物と重ならない場所。
        private static readonly Vector3 _origin = new Vector3(300f, 0f, -300f);

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
            CombatEvents.ClearSubscribers();
        }

        [Test]
        public void AShotThatEmptiesTheHealthExplodesOnTheNextTick()
        {
            var barrel = CreateProp("Barrel", Vector3.zero, ExplosivePropSpec.Default);
            var collider = barrel.GetComponent<Collider>();

            var hits = StructureDamage.Apply(collider, collider.bounds.center, ExplosivePropSpec.Default.MaxHealth, 0f);

            Assert.That(hits, Is.EqualTo(1), "a bullet reaches the prop through the structure hit");
            Assert.That(barrel.IsTriggered, Is.True);
            Assert.That(barrel.IsChained, Is.False);
            Assert.That(barrel.HasExploded, Is.False, "it does not explode inside the hit");

            Assert.That(barrel.Tick(0f), Is.True);
            Assert.That(barrel == null, Is.True, "the prop is removed after exploding");
        }

        [Test]
        public void AWeakShotOnlyWearsItDown()
        {
            var barrel = CreateProp("Barrel", Vector3.zero, new ExplosivePropSpec(maxHealth: 30));

            Assert.That(barrel.ApplyDamage(10, chained: false), Is.EqualTo(ExplosiveChargeStep.Damaged));
            Assert.That(barrel.Health, Is.EqualTo(20));
            Assert.That(barrel.Tick(5f), Is.False);
            Assert.That(barrel.HasExploded, Is.False);
        }

        [Test]
        public void TheExplosionKillsANearbyTargetAndSparesOneBeyondTheRadius()
        {
            var barrel = CreateProp("Barrel", Vector3.zero, ExplosivePropSpec.Default);
            var near = CreateTarget("Near", new Vector3(1.2f, 1f, 0f));
            var far = CreateTarget("Far", new Vector3(ExplosivePropSpec.Default.DamageRadius + 2f, 1f, 0f));
            ExplosivePropExplosion? result = null;
            barrel.Exploded += (prop, explosion) => result = explosion;

            barrel.ApplyDamage(ExplosivePropSpec.Default.MaxHealth, chained: false);
            barrel.Tick(0f);

            Assert.That(result.HasValue, Is.True);
            Assert.That(near.IsDead, Is.True);
            Assert.That(far.Health.Current, Is.EqualTo(far.Health.Max));
            Assert.That(result.Value.Damaged, Is.EqualTo(1));
            Assert.That(result.Value.Killed, Is.EqualTo(1));
            Assert.That(result.Value.Chained, Is.False);
        }

        [Test]
        public void TheExplosionDoesNotHitATargetBehindAWall()
        {
            var barrel = CreateProp("Barrel", Vector3.zero, ExplosivePropSpec.Default);
            var hidden = CreateTarget("Hidden", new Vector3(2.5f, 1f, 0f));
            CreateBox("Wall", _origin + new Vector3(1.2f, 1.5f, 0f), new Vector3(0.3f, 3f, 4f));

            barrel.ApplyDamage(ExplosivePropSpec.Default.MaxHealth, chained: false);
            barrel.Tick(0f);

            Assert.That(hidden.Health.Current, Is.EqualTo(hidden.Health.Max));
        }

        [Test]
        public void ANeighbourGoesOffAsAChainAfterTheChainDelay()
        {
            var spec = new ExplosivePropSpec(chainDelaySeconds: 0.5f);
            var first = CreateProp("First", Vector3.zero, spec);
            var second = CreateProp("Second", new Vector3(1.6f, 0f, 0f), spec);
            ExplosivePropExplosion? chained = null;
            second.Exploded += (prop, explosion) => chained = explosion;

            first.ApplyDamage(spec.MaxHealth, chained: false);
            first.Tick(0f);

            Assert.That(second.IsTriggered, Is.True);
            Assert.That(second.IsChained, Is.True);
            Assert.That(second.RemainingSeconds, Is.EqualTo(0.5f));
            Assert.That(ExplosiveProp.IsBlastInProgress, Is.False, "the blast is over once the explosion returns");

            Assert.That(second.Tick(0.4f), Is.False);
            Assert.That(second.Tick(0.2f), Is.True);
            Assert.That(chained.HasValue, Is.True);
            Assert.That(chained.Value.Chained, Is.True);
        }

        [Test]
        public void ANeighbourOutsideTheRadiusDoesNotChain()
        {
            var first = CreateProp("First", Vector3.zero, ExplosivePropSpec.Default);
            var away = CreateProp("Away", new Vector3(ExplosivePropSpec.Default.DamageRadius + 2f, 0f, 0f), ExplosivePropSpec.Default);

            first.ApplyDamage(ExplosivePropSpec.Default.MaxHealth, chained: false);
            first.Tick(0f);

            Assert.That(away.IsTriggered, Is.False);
            Assert.That(away.Health, Is.EqualTo(ExplosivePropSpec.Default.MaxHealth));
        }

        [Test]
        public void NoStructureDamageMeansNoChain()
        {
            var first = CreateProp("First", Vector3.zero, new ExplosivePropSpec(structureDamageAtCenter: 0));
            var second = CreateProp("Second", new Vector3(1.6f, 0f, 0f), ExplosivePropSpec.Default);

            first.ApplyDamage(ExplosivePropSpec.Default.MaxHealth, chained: false);
            first.Tick(0f);

            Assert.That(second.IsTriggered, Is.False);
        }

        [Test]
        public void ABurningPropExplodesAfterTheBurnSeconds()
        {
            var can = CreateProp("GasCan", Vector3.zero, new ExplosivePropSpec(maxHealth: 5, burnSeconds: 2f));
            var target = CreateTarget("Near", new Vector3(1.2f, 1f, 0f));
            var steps = new List<ExplosiveChargeStep>();
            can.Triggered += (prop, step) => steps.Add(step);

            Assert.That(can.ApplyDamage(5, chained: false), Is.EqualTo(ExplosiveChargeStep.Ignited));
            Assert.That(can.IsBurning, Is.True);
            Assert.That(steps, Is.EqualTo(new[] { ExplosiveChargeStep.Ignited }));

            Assert.That(can.Tick(1.9f), Is.False);
            Assert.That(target.IsDead, Is.False, "nothing happens while it burns");
            Assert.That(can.Tick(0.2f), Is.True);
            Assert.That(target.IsDead, Is.True);
        }

        [Test]
        public void TheExplosionHitsStructuresInTheRadius()
        {
            var barrel = CreateProp("Barrel", Vector3.zero, ExplosivePropSpec.Default);
            var structure = CreateBox("Hut", _origin + new Vector3(2f, 0.5f, 0f), Vector3.one).AddComponent<StructureHitRecorder>();
            ExplosivePropExplosion? result = null;
            barrel.Exploded += (prop, explosion) => result = explosion;

            barrel.ApplyDamage(ExplosivePropSpec.Default.MaxHealth, chained: false);
            barrel.Tick(0f);

            Assert.That(structure.Calls, Has.Count.EqualTo(1));
            Assert.That(structure.Calls[0].Damage, Is.GreaterThan(0));
            Assert.That(result.HasValue, Is.True);
            Assert.That(result.Value.StructureHits, Is.EqualTo(1));
        }

        [Test]
        public void ABuildingBreaksOnlyWhenDestructionIsOn()
        {
            var on = CreateBuilding("On", new Vector3(0f, 0f, 1.2f), destructionEnabled: true);
            var off = CreateBuilding("Off", new Vector3(0f, 0f, -1.2f), destructionEnabled: false);
            var barrel = CreateProp("Barrel", Vector3.zero, ExplosivePropSpec.Default);

            barrel.ApplyDamage(ExplosivePropSpec.Default.MaxHealth, chained: false);
            barrel.Tick(0f);

            Assert.That(on.Integrity.IsDestroyed(0), Is.True, "destruction on: the blast breaks the piece");
            Assert.That(off.Integrity.IsDestroyed(0), Is.False, "destruction off: the piece stands");
            Assert.That(off.Integrity.Health(0), Is.EqualTo(DestructiblePiece.DefaultMaxHealth));
        }

        [Test]
        public void AGrenadeSetsThePropOffWithoutChaining()
        {
            var barrel = CreateProp("Barrel", Vector3.zero, ExplosivePropSpec.Default);
            var grenade = ThrownGrenade.Spawn(
                null,
                _origin + new Vector3(0f, 0.5f, -1.5f),
                Vector3.zero,
                GrenadeSpec.Default.FuseSeconds,
                GrenadeSpec.Default.Explosion,
                GrenadeSpec.Default.StructureDamageAtCenter);
            _created.Add(grenade.gameObject);
            Physics.SyncTransforms();

            grenade.Explode();

            Assert.That(barrel.IsTriggered, Is.True);
            Assert.That(barrel.IsChained, Is.False, "only another prop's blast counts as a chain");
        }

        [Test]
        public void KillsAreCreditedToWhoeverSetThePropOff()
        {
            var shooter = new GameObject("Shooter");
            _created.Add(shooter);
            var barrel = CreateProp("Barrel", Vector3.zero, ExplosivePropSpec.Default);
            var target = CreateTarget("Near", new Vector3(1.2f, 1f, 0f));
            var kills = new List<KillFeedEntry>();
            CombatEvents.Killed += kills.Add;

            using (CombatEvents.Attribute(shooter, "rifle"))
            {
                barrel.ApplyDamage(ExplosivePropSpec.Default.MaxHealth, chained: false);
            }

            barrel.Tick(0f);

            Assert.That(target.IsDead, Is.True);
            Assert.That(kills, Has.Count.EqualTo(1));
            Assert.That(kills[0].KillerId, Is.EqualTo(CombatEvents.OfflineId(shooter)));
            Assert.That(kills[0].WeaponId, Is.EqualTo(ExplosiveProp.WeaponId));
            Assert.That(CombatEvents.CurrentAttribution.HasValue, Is.False, "the attribution is restored after the blast");
        }

        [Test]
        public void ConfigureReplacesTheValuesAndRefillsTheHealth()
        {
            var barrel = CreateProp("Barrel", Vector3.zero, ExplosivePropSpec.Default);
            barrel.ApplyDamage(5, chained: false);
            var spec = new ExplosivePropSpec(maxHealth: 50, burnSeconds: 1f);

            barrel.Configure(spec);

            Assert.That(barrel.Spec, Is.SameAs(spec));
            Assert.That(barrel.Health, Is.EqualTo(50));
            Assert.Throws<System.ArgumentNullException>(() => barrel.Configure(null));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => barrel.ApplyDamage(-1, chained: false));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => barrel.Tick(-1f));
        }

        [Test]
        public void InvalidInspectorValuesAreReportedOnceAndIgnoreHits()
        {
            var box = CreateBox("Broken", _origin, Vector3.one);
            var prop = box.AddComponent<ExplosiveProp>();
            var serialized = new SerializedObject(prop);
            serialized.FindProperty("_fullDamageRadius").floatValue = 10f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(ExplosiveProp.LogTag) + ".*Broken"));
            Assert.That(prop.ApplyDamage(100, chained: false), Is.EqualTo(ExplosiveChargeStep.None));
            Assert.That(prop.ApplyDamage(100, chained: false), Is.EqualTo(ExplosiveChargeStep.None));
            Assert.That(prop.Spec, Is.Null);
            Assert.That(prop.Tick(1f), Is.False);
        }

        [Test]
        public void TheIdFallsBackToTheObjectName()
        {
            var barrel = CreateProp("Barrel", Vector3.zero, ExplosivePropSpec.Default);
            Assert.That(barrel.Id, Is.EqualTo("Barrel"));

            barrel.Id = "Custom";
            Assert.That(barrel.Id, Is.EqualTo("Custom"));
        }

        private ExplosiveProp CreateProp(string name, Vector3 offset, ExplosivePropSpec spec)
        {
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _created.Add(barrel);
            barrel.name = name;
            barrel.transform.position = _origin + offset + new Vector3(0f, 0.45f, 0f);
            barrel.transform.localScale = new Vector3(0.6f, 0.45f, 0.6f);
            var prop = barrel.AddComponent<ExplosiveProp>();
            prop.Configure(spec);
            Physics.SyncTransforms();
            return prop;
        }

        private Damageable CreateTarget(string name, Vector3 offset)
        {
            var body = CreateBox(name, _origin + offset, new Vector3(0.6f, 2f, 0.6f));
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(name, 100);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            return damageable;
        }

        private DestructibleStructure CreateBuilding(string name, Vector3 offset, bool destructionEnabled)
        {
            var root = new GameObject(name);
            _created.Add(root);
            root.transform.position = _origin + offset;
            var structure = root.AddComponent<DestructibleStructure>();
            structure.SpawnDebris = false;
            DestructiblePiece.Create(root.transform, "Piece", new Vector3(0f, 0.5f, 0f), Vector3.one, 0, grounded: true);
            structure.DestructionEnabled = destructionEnabled;
            structure.Build();
            Physics.SyncTransforms();
            return structure;
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

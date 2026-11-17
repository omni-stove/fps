using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class RulesPlayerHealthAndZoneDamageTests
    {
        private static readonly ZoneDamageMultipliers _headTimesThree = new ZoneDamageMultipliers(3.0, 1.0, 0.75, 2.0);

        private readonly List<Object> _created = new List<Object>();
        private readonly List<BootstrapWorld> _worlds = new List<BootstrapWorld>();
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _rifle = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
        }

        [TearDown]
        public void TearDown()
        {
            GameRulesOverride.Clear();
            GameContent.Use(null);
            foreach (var world in _worlds)
            {
                DestroyWorld(world);
            }

            _worlds.Clear();
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
            GameSignals.ResetAll();
        }

        [Test]
        public void TheRulesHeadMultiplierTriplesAHeadHit()
        {
            var target = CreateDamageable("target");
            target.ApplyRules(GameRules.Default.WithZoneDamage(_headTimesThree));

            var head = target.ApplyHit(20, HitZone.Head, 0f, DamageFalloff.None);
            var body = target.ApplyHit(20, HitZone.Body, 0f, DamageFalloff.None);

            Assert.That(head, Is.EqualTo(60));
            Assert.That(body, Is.EqualTo(20));
            Assert.That(target.Health.Current, Is.EqualTo(20));
        }

        [Test]
        public void WithoutRulesTheDamageableUsesTheDefaultMultipliers()
        {
            var target = CreateDamageable("target");

            Assert.That(target.ZoneMultipliers, Is.SameAs(ZoneDamageMultipliers.Default));
            Assert.That(target.ApplyHit(20, HitZone.Head, 0f, DamageFalloff.None), Is.EqualTo(40));
        }

        [Test]
        public void ASetMultiplierStaysOverTheRulesWhicheverComesFirst()
        {
            var boss = new ZoneDamageMultipliers(0.25, 0.2, 0.1, 3.0);
            var rules = GameRules.Default.WithZoneDamage(_headTimesThree);

            var setFirst = CreateDamageable("set-first");
            setFirst.ZoneMultipliers = boss;
            setFirst.ApplyRules(rules);

            var rulesFirst = CreateDamageable("rules-first");
            rulesFirst.ApplyRules(rules);
            rulesFirst.ZoneMultipliers = boss;

            Assert.That(setFirst.ZoneMultipliers, Is.SameAs(boss));
            Assert.That(rulesFirst.ZoneMultipliers, Is.SameAs(boss));
            Assert.That(setFirst.ApplyHit(100, HitZone.Head, 0f, DamageFalloff.None), Is.EqualTo(25));
        }

        [Test]
        public void ApplyRulesRejectsNull()
        {
            var target = CreateDamageable("target");

            Assert.Throws<System.ArgumentNullException>(() => target.ApplyRules(null));
        }

        [Test]
        public void AHitscanShotAtTheHeadOfARulesTargetDealsThreeTimesTheBody()
        {
            var head = CreateTarget("head", new Vector3(0f, 1f, 10f), HitZone.Head);
            head.ApplyRules(GameRules.Default.WithZoneDamage(_headTimesThree));

            var outcome = HitscanDelivery.Instance.Deliver(
                new ShotRequest(null, null, null, new RoundSpec(), new ImpactSpec(20), "test-rifle", Physics.DefaultRaycastLayers),
                new PlannedRound(new Vector3(0f, 1f, 0f).ToNumerics(), Vector3.forward.ToNumerics(), 0));

            Assert.That(outcome.Owner, Is.SameAs(head));
            Assert.That(outcome.Damage, Is.EqualTo(60));
            Assert.That(head.Health.Current, Is.EqualTo(40));
        }

        [Test]
        public void ThePlayerStartsWithTheRulesMaxHealthOverTheInspectorValue()
        {
            var definition = Rules(playerMaxHealth: 200);
            var bootstrap = CreateBootstrap("Bootstrap-RulesHealth");
            bootstrap.RulesDefinition = definition;
            bootstrap.PlayerMaxHealth = 150;

            var world = Build(bootstrap);

            Assert.That(world.PlayerDamageable.MaxHealth, Is.EqualTo(200));
            Assert.That(world.PlayerDamageable.Health.Max, Is.EqualTo(200));
            Assert.That(world.PlayerDamageable.Health.Current, Is.EqualTo(200));
        }

        [Test]
        public void TheOverriddenRulesMaxHealthIsUsedOverTheSceneRules()
        {
            GameRulesOverride.Set(GameRules.Default.WithPlayerMaxHealth(250));
            var bootstrap = CreateBootstrap("Bootstrap-OverrideHealth");
            bootstrap.RulesDefinition = Rules(playerMaxHealth: 200);
            bootstrap.PlayerMaxHealth = 150;
            LogAssert.Expect(LogType.Log, GameBootstrap.FormatOverrideLog("TestRules"));

            var world = Build(bootstrap);

            Assert.That(world.PlayerDamageable.MaxHealth, Is.EqualTo(250));
        }

        [Test]
        public void WithoutRulesThePlayerUsesTheInspectorMaxHealth()
        {
            GameContent.Use(Track(ScriptableObject.CreateInstance<GameContent>()));
            var bootstrap = CreateBootstrap("Bootstrap-NoRules");
            bootstrap.PlayerMaxHealth = 150;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"^\[Bootstrap\] GameRulesDefinition が設定されていない"));

            var world = Build(bootstrap);

            Assert.That(world.Rules, Is.SameAs(GameRules.Default));
            Assert.That(world.PlayerDamageable.MaxHealth, Is.EqualTo(150));
            Assert.That(world.PlayerDamageable.Health.Current, Is.EqualTo(150));
            Assert.That(world.PlayerDamageable.ZoneMultipliers, Is.SameAs(ZoneDamageMultipliers.Default));
        }

        [Test]
        public void ThePlayerAndTheTargetDummiesTakeTheRulesZoneDamage()
        {
            var bootstrap = CreateBootstrap("Bootstrap-ZoneDamage");
            bootstrap.RulesDefinition = Rules(head: 3.0);

            var world = Build(bootstrap);

            Assert.That(world.PlayerDamageable.ZoneMultipliers.Head, Is.EqualTo(3.0));
            Assert.That(world.Targets, Is.Not.Empty);
            foreach (var dummy in world.Targets)
            {
                Assert.That(dummy.Damageable.ZoneMultipliers.Head, Is.EqualTo(3.0), dummy.name);
                Assert.That(dummy.Damageable.ZoneMultipliers.Body, Is.EqualTo(1.0), dummy.name);
            }

            var dummyDamageable = world.Targets[0].Damageable;
            Assert.That(dummyDamageable.ApplyHit(25, HitZone.Head, 0f, DamageFalloff.None), Is.EqualTo(75));
        }

        [Test]
        public void ADamageablePlacedInTheSceneTakesTheZoneDamageOfTheBuiltBootstrap()
        {
            var bootstrap = CreateBootstrap("Bootstrap-SceneRules");
            bootstrap.RulesDefinition = Rules(head: 3.0);
            Build(bootstrap);
            var enemy = CreateDamageable("enemy");

            enemy.ApplySceneRules();

            Assert.That(GameBootstrap.FindSceneRules(), Is.SameAs(bootstrap.World.Rules));
            Assert.That(enemy.ZoneMultipliers.Head, Is.EqualTo(3.0));
        }

        [Test]
        public void WithoutABootstrapASceneDamageableKeepsItsMultipliers()
        {
            var enemy = CreateDamageable("enemy");

            enemy.ApplySceneRules();

            Assert.That(GameBootstrap.FindSceneRules(), Is.Null);
            Assert.That(enemy.ZoneMultipliers, Is.SameAs(ZoneDamageMultipliers.Default));
        }

        private GameRulesDefinition Rules(int playerMaxHealth = GameRules.DefaultPlayerMaxHealth, double head = 2.0)
        {
            var definition = Track(ScriptableObject.CreateInstance<GameRulesDefinition>());
            definition.name = "TestRules";
            definition.PlayerMaxHealth = playerMaxHealth;
            definition.HeadDamageMultiplier = head;
            return definition;
        }

        private GameBootstrap CreateBootstrap(string name)
        {
            var host = Track(new GameObject(name));
            var bootstrap = host.AddComponent<GameBootstrap>();
            bootstrap.CreatesFloor = false;
            bootstrap.WeaponDefinitions = new[] { _rifle };
            return bootstrap;
        }

        private BootstrapWorld Build(GameBootstrap bootstrap)
        {
            LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
            var world = bootstrap.Build();
            _worlds.Add(world);
            return world;
        }

        private Damageable CreateDamageable(string id)
        {
            var go = Track(new GameObject(id));
            var damageable = go.AddComponent<Damageable>();
            damageable.Initialize(id, 100);
            return damageable;
        }

        private Damageable CreateTarget(string id, Vector3 position, HitZone zone)
        {
            var target = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            target.name = id;
            target.transform.position = position;
            var damageable = target.AddComponent<Damageable>();
            damageable.Initialize(id, 100);
            target.AddComponent<Hitbox>().Initialize(zone, damageable);
            Physics.SyncTransforms();
            return damageable;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        private static void DestroyWorld(BootstrapWorld world)
        {
            if (world == null)
            {
                return;
            }

            Object.DestroyImmediate(world.Player);
            Object.DestroyImmediate(world.Light.gameObject);
            Object.DestroyImmediate(world.TargetRoot);
        }
    }
}

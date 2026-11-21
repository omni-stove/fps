using System;
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
    // Edit Mode では WeaponController が初期化されない (Awake が走らない) ので、持たせる武器は WeaponController.Definitions で見る。
    // 最初の感染者は、乱数の代わりに FirstPick (いつも並びの先頭を選ぶ) で ID 0 (プレイヤー) にする。
    public sealed class InfectedRunnerTests
    {
        private const float StartDelaySeconds = 5f;
        private const float RoundSeconds = 60f;
        private const float RespawnSeconds = 2f;
        private const int InfectedMaxHealth = 250;
        private const int ScorePerInfection = 2;
        private const int ScorePerSurvivedRound = 4;

        private static readonly Vector3 SurvivorSpawn = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 InfectedSpawn = new Vector3(0f, 0f, 40f);

        private readonly List<Object> _objects = new List<Object>();
        private InfectedRunner _runner;
        private WeaponDefinition _rifle;
        private WeaponDefinition _handgun;
        private SideSpawnPoint[] _spawns;

        [SetUp]
        public void SetUp()
        {
            _rifle = Weapon("Rifle", TestWeapons.RifleSpec());
            _handgun = Weapon("Handgun", TestWeapons.HandgunSpec());
            _runner = Track(new GameObject("Runner")).AddComponent<InfectedRunner>();
            _runner.OutbreakPicker = FirstPick;
            _spawns = new[]
            {
                SpawnPoint("Spawn-0-1", Infected.SurvivorSide, SurvivorSpawn),
                SpawnPoint("Spawn-1-1", Infected.InfectedSide, InfectedSpawn),
            };
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            GameContent.Use(null);
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void EveryoneStartsOnTheSurvivorSide()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-1-1", 1);

            Begin(player, bot);

            Assert.That(player.Side, Is.EqualTo(Infected.SurvivorSide));
            Assert.That(bot.Side, Is.EqualTo(Infected.SurvivorSide), "the scene side is not used");
            Assert.That(_runner.Session.GetSide(_runner.IdOf(bot)), Is.EqualTo(Infected.SurvivorSide));
            Assert.That(_runner.Infected.HasBrokenOut, Is.False);
            var detail = Detail();
            Assert.That(detail.HasBrokenOut, Is.False);
            Assert.That(detail.SecondsUntilOutbreak, Is.EqualTo(StartDelaySeconds));
            Assert.That(detail.SurvivorCount, Is.EqualTo(2));
            Assert.That(detail.IsLocalPlayerInfected, Is.EqualTo((bool?)false));
        }

        [Test]
        public void TheFirstInfectedMovesToTheInfectedSpawnWithTheInfectedWeaponAndHealth()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-0-1", 0);
            Begin(player, bot);

            LogAssert.Expect(LogType.Log, "[Mode] infected turn player=0 by=first");
            LogAssert.Expect(LogType.Log, "[Mode] infected outbreak first=0");
            LogAssert.Expect(LogType.Log, "[Mode] infected equip player=0 weapon=weapon.handgun");
            _runner.Tick(StartDelaySeconds);

            Assert.That(player.Side, Is.EqualTo(Infected.InfectedSide));
            Assert.That(bot.Side, Is.EqualTo(Infected.SurvivorSide));
            Assert.That(Flat(player.transform.position - InfectedSpawn), Is.LessThan(1e-3f), "they start again among the infected");
            Assert.That(player.Damageable.MaxHealth, Is.EqualTo(InfectedMaxHealth));
            Assert.That(player.Damageable.Health.Current, Is.EqualTo(InfectedMaxHealth));
            Assert.That(Controller(player).Definitions, Is.EqualTo(new[] { _handgun }));
            Assert.That(Controller(bot).Definitions, Is.EqualTo(new[] { _rifle }), "the survivors keep their weapons");
            Assert.That(Detail().IsLocalPlayerInfected, Is.EqualTo((bool?)true));
        }

        [Test]
        public void ASurvivorKilledByTheInfectedBecomesInfectedWhenTheyRespawn()
        {
            var player = Member("Player", 0);
            var survivor = Member("Bot-0-1", 0);
            var other = Member("Bot-0-2", 0);
            Begin(player, survivor, other);
            _runner.Tick(StartDelaySeconds);

            Kill(survivor, player);

            Assert.That(survivor.Side, Is.EqualTo(Infected.InfectedSide));
            Assert.That(_runner.Infected.IsInfected(_runner.IdOf(survivor)), Is.True);
            Assert.That(_runner.Session.GetScore(Infected.InfectedSide), Is.EqualTo((double)ScorePerInfection));
            Assert.That(Controller(survivor).Definitions, Is.EqualTo(new[] { _rifle }), "the weapon is swapped on the respawn, not while dying");

            _runner.Tick(RespawnSeconds);

            Assert.That(survivor.IsAlive, Is.True);
            Assert.That(Flat(survivor.transform.position - InfectedSpawn), Is.LessThan(1e-3f));
            Assert.That(survivor.Damageable.MaxHealth, Is.EqualTo(InfectedMaxHealth));
            Assert.That(Controller(survivor).Definitions, Is.EqualTo(new[] { _handgun }));
            Assert.That(Detail().SurvivorCount, Is.EqualTo(1));
            Assert.That(Detail().InfectedCount, Is.EqualTo(2));
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void TheInfectedWinWhenEverySurvivorIsInfected()
        {
            var player = Member("Player", 0);
            var first = Member("Bot-0-1", 0);
            var second = Member("Bot-0-2", 0);
            Begin(player, first, second);
            _runner.Tick(StartDelaySeconds);

            Kill(first, player);
            _runner.Tick(0.1f);
            Assert.That(_runner.IsMatchOver, Is.False);

            Kill(second, player);
            _runner.Tick(0.1f);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)Infected.InfectedSide));
            Assert.That(_runner.IsMatchOver, Is.True);
            Assert.That(_runner.Status.IsEnded, Is.True);
            Assert.That(_runner.Status.Winner, Is.EqualTo((Side?)Infected.InfectedSide));
            Assert.That(Detail().SurvivorCount, Is.Zero);
        }

        [Test]
        public void TheSurvivorsWinWhenTimeRunsOut()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-0-1", 0);
            Begin(player, bot);
            _runner.Tick(StartDelaySeconds);

            _runner.Tick(RoundSeconds);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)Infected.SurvivorSide));
            Assert.That(_runner.Session.GetScore(Infected.SurvivorSide), Is.EqualTo((double)ScorePerSurvivedRound));
            Assert.That(_runner.IsMatchOver, Is.True);
            Assert.That(bot.Side, Is.EqualTo(Infected.SurvivorSide));
        }

        [Test]
        public void ASurvivorWhoDiesBeforeTheOutbreakComesBackAsASurvivor()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-0-1", 0);
            Begin(player, bot);

            Kill(bot, null);
            _runner.Tick(RespawnSeconds);

            Assert.That(bot.Side, Is.EqualTo(Infected.SurvivorSide));
            Assert.That(bot.IsAlive, Is.True);
            Assert.That(bot.Damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(Controller(bot).Definitions, Is.EqualTo(new[] { _rifle }));
        }

        [Test]
        public void WithoutAnInfectedWeaponTheInfectedKeepTheirWeapons()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-0-1", 0);
            _runner.Begin(new Infected(Spec(), FirstPick), new[] { player, bot }, _spawns, player);

            _runner.Tick(StartDelaySeconds);

            Assert.That(_runner.InfectedWeapon, Is.Null);
            Assert.That(player.Side, Is.EqualTo(Infected.InfectedSide));
            Assert.That(Controller(player).Definitions, Is.EqualTo(new[] { _rifle }));
        }

        [Test]
        public void TheSpecComesFromTheTuningJsonWithoutADefinition()
        {
            UseEmptyContent();

            var spec = _runner.CreateSpec();

            Assert.That(spec.InitialInfected, Is.EqualTo(InfectedRunner.DefaultInitialInfected));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(InfectedRunner.DefaultStartDelaySeconds));
            Assert.That(spec.RoundSeconds, Is.EqualTo(InfectedRunner.DefaultRoundSeconds));
            Assert.That(spec.HasInfectedWeapon, Is.False);
        }

        [Test]
        public void AWiredDefinitionGivesTheSpecAndTheWeapon()
        {
            var definition = Definition();
            definition.InitialInfected = 3;

            _runner.Definition = definition;
            var spec = _runner.CreateSpec();

            Assert.That(spec.InitialInfected, Is.EqualTo(3));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(StartDelaySeconds));
            Assert.That(spec.RoundSeconds, Is.EqualTo(RoundSeconds));
            Assert.That(spec.InfectedMaxHealth, Is.EqualTo(InfectedMaxHealth));
            Assert.That(spec.InfectedRespawnSeconds, Is.EqualTo(RespawnSeconds));
            Assert.That(spec.InfectedWeaponId, Is.EqualTo(TestWeapons.HandgunSpec().Id));
            Assert.That(_runner.ResolveInfectedWeapon(spec), Is.SameAs(_handgun));
        }

        [Test]
        public void ANewDefinitionHasTheRunnerDefaultsAndCopiesASpec()
        {
            var definition = Track(ScriptableObject.CreateInstance<InfectedDefinition>());

            var spec = definition.ToSpec();
            Assert.That(spec.InitialInfected, Is.EqualTo(InfectedRunner.DefaultInitialInfected));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(InfectedRunner.DefaultStartDelaySeconds));
            Assert.That(spec.RoundSeconds, Is.EqualTo(InfectedRunner.DefaultRoundSeconds));
            Assert.That(spec.InfectedMaxHealth, Is.EqualTo(InfectedRunner.DefaultInfectedMaxHealth));
            Assert.That(spec.InfectedSpeedMultiplier, Is.EqualTo(InfectedRunner.DefaultInfectedSpeedMultiplier));
            Assert.That(spec.InfectedRespawnSeconds, Is.EqualTo(InfectedRunner.DefaultInfectedRespawnSeconds));
            Assert.That(spec.ScorePerInfection, Is.EqualTo(InfectedRunner.DefaultScorePerInfection));
            Assert.That(spec.ScorePerSurvivedRound, Is.EqualTo(InfectedRunner.DefaultScorePerSurvivedRound));
            Assert.That(spec.HasInfectedWeapon, Is.False);

            definition.CopyFrom(new InfectedSpec(2, 8f, 120f, 180, 1.5f, 1f, 3, 5, "ignored"));
            spec = definition.ToSpec();
            Assert.That(spec.InitialInfected, Is.EqualTo(2));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(8f));
            Assert.That(spec.RoundSeconds, Is.EqualTo(120f));
            Assert.That(spec.InfectedMaxHealth, Is.EqualTo(180));
            Assert.That(spec.InfectedSpeedMultiplier, Is.EqualTo(1.5f));
            Assert.That(spec.InfectedRespawnSeconds, Is.EqualTo(1f));
            Assert.That(spec.ScorePerInfection, Is.EqualTo(3));
            Assert.That(spec.ScorePerSurvivedRound, Is.EqualTo(5));
            Assert.That(spec.HasInfectedWeapon, Is.False, "the weapon is set as an asset, not copied from the id");
            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
        }

        [Test]
        public void TheWeaponIdOfTheJsonIsFoundInTheWeaponCatalog()
        {
            var catalog = Track(ScriptableObject.CreateInstance<WeaponCatalogAsset>());
            catalog.name = "Catalog";
            catalog.SetWeapons(new[] { _rifle, _handgun });
            var spec = Spec(TestWeapons.HandgunSpec().Id);
            UseEmptyContent();

            var missing = Assert.Throws<InvalidOperationException>(() => _runner.ResolveInfectedWeapon(spec));
            StringAssert.Contains("no weapon catalog", missing.Message);

            _runner.WeaponCatalog = catalog;
            Assert.That(_runner.ResolveInfectedWeapon(spec), Is.SameAs(_handgun));

            var unknown = Assert.Throws<InvalidOperationException>(() => _runner.ResolveInfectedWeapon(Spec("no-such-weapon")));
            StringAssert.Contains("no-such-weapon", unknown.Message);
            Assert.That(_runner.ResolveInfectedWeapon(Spec()), Is.Null);
            Assert.Throws<ArgumentNullException>(() => _runner.ResolveInfectedWeapon(null));
        }

        [Test]
        public void TryBeginFailsWithAnInvalidDefinitionAndNamesIt()
        {
            var definition = Definition();
            definition.name = "BrokenInfected";
            definition.StartDelaySeconds = RoundSeconds;
            _runner.Definition = definition;

            var error = Assert.Throws<InvalidOperationException>(() => _runner.CreateSpec());
            StringAssert.Contains("BrokenInfected", error.Message);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("BrokenInfected"));
            Assert.That(_runner.TryBegin(), Is.False);
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void AFailedBeginLeavesNoModeAttached()
        {
            Assert.Throws<ArgumentException>(
                () => _runner.Begin(new Infected(Spec(), FirstPick), Array.Empty<SideMember>(), _spawns),
                "there is nobody to infect");
            Assert.That(_runner.IsStarted, Is.False);
            Assert.That(_runner.Infected, Is.Null);
            Assert.Throws<ArgumentNullException>(() => _runner.Begin(null, Array.Empty<SideMember>(), _spawns));
        }

        [Test]
        public void StopForgetsTheMode()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-0-1", 0);
            Begin(player, bot);
            var infected = _runner.Infected;

            _runner.Stop();
            infected.Session.Tick(StartDelaySeconds);

            Assert.That(_runner.Infected, Is.Null);
            Assert.That(infected.HasBrokenOut, Is.True);
            Assert.That(player.Side, Is.EqualTo(Infected.SurvivorSide), "a stopped runner does not move anyone");
        }

        private void Begin(params SideMember[] members)
        {
            _runner.Definition = Definition();
            _runner.Begin(new Infected(_runner.CreateSpec(), FirstPick), members, _spawns, members[0]);
        }

        // 入口 (GameContent) をプロジェクトの Resources のものから空のものに替え、置いてある調整値やカタログを読ませない。
        private void UseEmptyContent()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = "EmptyContent";
            GameContent.Use(content);
        }

        private InfectedDefinition Definition()
        {
            var definition = Track(ScriptableObject.CreateInstance<InfectedDefinition>());
            definition.name = "Infected";
            definition.InitialInfected = 1;
            definition.StartDelaySeconds = StartDelaySeconds;
            definition.RoundSeconds = RoundSeconds;
            definition.InfectedMaxHealth = InfectedMaxHealth;
            definition.InfectedRespawnSeconds = RespawnSeconds;
            definition.ScorePerInfection = ScorePerInfection;
            definition.ScorePerSurvivedRound = ScorePerSurvivedRound;
            definition.InfectedWeapon = _handgun;
            return definition;
        }

        private static InfectedSpec Spec(string weaponId = InfectedSpec.DefaultInfectedWeaponId)
        {
            return new InfectedSpec(
                1,
                StartDelaySeconds,
                RoundSeconds,
                InfectedMaxHealth,
                InfectedSpec.DefaultInfectedSpeedMultiplier,
                RespawnSeconds,
                ScorePerInfection,
                ScorePerSurvivedRound,
                weaponId);
        }

        private static void Kill(SideMember victim, SideMember killer)
        {
            if (killer == null)
            {
                victim.Damageable.Kill();
                return;
            }

            using (CombatEvents.Attribute(killer.gameObject, "handgun"))
            {
                victim.Damageable.Kill();
            }
        }

        private static float Flat(Vector3 offset)
        {
            return new Vector2(offset.x, offset.z).magnitude;
        }

        private static WeaponController Controller(SideMember member)
        {
            return member.GetComponentInChildren<WeaponController>(true);
        }

        private InfectedStatus Detail()
        {
            Assert.That(_runner.Status, Is.Not.Null);
            Assert.That(_runner.Status.Detail, Is.InstanceOf<InfectedStatus>());
            return (InfectedStatus)_runner.Status.Detail;
        }

        private SideMember Member(string name, int side)
        {
            var go = Track(new GameObject(name, typeof(CapsuleCollider)));
            go.transform.position = new Vector3(side * 5f, 0f, 10f);

            // 戻す位置は Collider の範囲から足元を測るので、物理を動かさない Edit Mode では移した位置を物理へ渡しておく
            Physics.SyncTransforms();
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);

            var view = new GameObject(name + "-Camera");
            view.transform.SetParent(go.transform, false);
            view.AddComponent<WeaponController>().Definitions = new[] { _rifle };
            return member;
        }

        private SideSpawnPoint SpawnPoint(string name, Side side, Vector3 position)
        {
            var point = Track(new GameObject(name)).AddComponent<SideSpawnPoint>();
            point.transform.position = position;
            point.Side = side;
            return point;
        }

        private WeaponDefinition Weapon(string name, WeaponSpec spec)
        {
            var definition = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            definition.name = name;
            WeaponAssets.Configure(definition, spec);
            return definition;
        }

        private T Track<T>(T obj)
            where T : Object
        {
            _objects.Add(obj);
            return obj;
        }

        private static int FirstPick(int count) => 0;
    }
}

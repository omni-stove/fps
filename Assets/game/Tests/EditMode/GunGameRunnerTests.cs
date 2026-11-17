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
    public sealed class GunGameRunnerTests
    {
        private const string KnifeId = "0d6f2c84-5b1e-4a37-9c08-3e7a1f5b2d96";

        private readonly List<Object> _objects = new List<Object>();
        private GunGameRunner _runner;
        private WeaponDefinition _rifle;
        private WeaponDefinition _launcher;
        private WeaponDefinition _handgun;
        private WeaponDefinition _knife;
        private GunGameLadderDefinition _ladder;

        [SetUp]
        public void SetUp()
        {
            _rifle = Weapon("Rifle", TestWeapons.RifleSpec());
            _launcher = Weapon("Launcher", TestWeapons.LauncherSpec());
            _handgun = Weapon("Handgun", TestWeapons.HandgunSpec());
            _knife = Weapon(
                "Knife",
                new WeaponSpec(KnifeId, "Knife", WeaponCategory.Handgun, FireMode.Semi, 1, 0, 0.5f, 0.1f, 100, 2f, DamageFalloff.None));

            _ladder = Track(ScriptableObject.CreateInstance<GunGameLadderDefinition>());
            _ladder.name = "TestLadder";
            _ladder.SetTiers(new[] { _rifle, _launcher, _handgun });
            _ladder.SetMeleeWeapons(new[] { _knife });
            _ladder.DemotesOnMelee = true;
            _ladder.RespawnSeconds = 0f;

            _runner = Track(new GameObject("Runner")).AddComponent<GunGameRunner>();
            _runner.Ladder = _ladder;
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
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
        public void BeginGivesEveryoneOnlyTheFirstTierWeapon()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-1", 1);
            Controller(player).Definitions = new[] { _rifle, _handgun };

            Begin(player, bot);

            Assert.That(_runner.GunGame, Is.Not.Null);
            Assert.That(Controller(player).Definitions, Is.EqualTo(new[] { _rifle }));
            Assert.That(Controller(bot).Definitions, Is.EqualTo(new[] { _rifle }));
            Assert.That(_runner.Session.Layout, Is.InstanceOf<FreeForAll>());
        }

        [Test]
        public void AKillWithTheTierWeaponSwapsTheKillersWeaponAtTheNextTick()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-1", 1);
            Begin(player, bot);
            Assert.That(Detail().GunGame, Is.SameAs(_runner.GunGame));
            Assert.That(Detail().LocalPlayerId, Is.EqualTo((int?)ModeRunnerBase.LocalPlayerId));

            LogAssert.Expect(LogType.Log, "[Mode] gungame promote player=0 tier=2/3 weapon=weapon.launcher");
            KillBy(player, bot, TestWeapons.RifleId);

            Assert.That(_runner.GunGame.GetTier(0), Is.EqualTo(1));
            Assert.That(Controller(player).Definitions, Is.EqualTo(new[] { _rifle }), "the weapon is swapped at the next tick, not while firing");

            LogAssert.Expect(LogType.Log, "[Mode] gungame equip player=0 tier=2/3 weapon=weapon.launcher");
            _runner.Tick(0f);

            Assert.That(Controller(player).Definitions, Is.EqualTo(new[] { _launcher }));
            Assert.That(Controller(bot).Definitions, Is.EqualTo(new[] { _rifle }));
            Assert.That(Detail().GunGame.GetTier(Detail().LocalPlayerId.Value), Is.EqualTo(1), "the status reads the new tier");
            Assert.That(_runner.Session.GetScore(new Side(0)), Is.EqualTo(1d), "kills still score");
        }

        [Test]
        public void AKillWithAnotherWeaponDoesNotAdvance()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-1", 1);
            Begin(player, bot);

            KillBy(player, bot, TestWeapons.HandgunId);
            _runner.Tick(0f);

            Assert.That(_runner.GunGame.GetTier(0), Is.Zero);
            Assert.That(Controller(player).Definitions, Is.EqualTo(new[] { _rifle }));
        }

        [Test]
        public void AKnifeKillDemotesTheVictimOneTier()
        {
            var player = Member("Player", 0);
            var a = Member("Bot-1", 1);
            var b = Member("Bot-2", 2);
            Begin(player, a, b);

            KillBy(a, player, TestWeapons.RifleId);
            _runner.Tick(0f);
            KillBy(a, b, TestWeapons.LauncherId);
            _runner.Tick(0f);
            Assert.That(Controller(a).Definitions, Is.EqualTo(new[] { _handgun }));

            LogAssert.Expect(LogType.Log, "[Mode] gungame demote player=1 tier=2/3 weapon=weapon.launcher");
            KillBy(player, a, KnifeId);
            _runner.Tick(0f);

            Assert.That(_runner.GunGame.GetTier(1), Is.EqualTo(1));
            Assert.That(Controller(a).Definitions, Is.EqualTo(new[] { _launcher }));
            Assert.That(_runner.GunGame.GetTier(0), Is.Zero, "the knife is not the killer's tier weapon");
        }

        [Test]
        public void BeginMakesThePlayersMeleeKillWithTheFirstMeleeWeapon()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-1", 1);
            var melee = player.gameObject.AddComponent<MeleeController>();

            Begin(player, bot);

            Assert.That(melee.MeleeWeapon, Is.SameAs(_knife));
            Assert.That(melee.StrikeWeaponId, Is.EqualTo(KnifeId));
        }

        [Test]
        public void AKillWithTheLastWeaponWinsAndLogsTheWinner()
        {
            var player = Member("Player", 0);
            var a = Member("Bot-1", 1);
            var b = Member("Bot-2", 2);
            Begin(player, a, b);

            KillBy(player, a, TestWeapons.RifleId);
            _runner.Tick(0f);
            KillBy(player, b, TestWeapons.LauncherId);
            _runner.Tick(0f);
            Assert.That(Detail().GunGame.GetNextWeapon(ModeRunnerBase.LocalPlayerId), Is.Null, "the player is on the last tier");

            LogAssert.Expect(LogType.Log, "[Mode] gungame finish player=0 weapon=weapon.handgun");
            KillBy(player, a, TestWeapons.HandgunId);
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Playing));

            LogAssert.Expect(LogType.Log, "[Mode] ended winner=0");
            _runner.Tick(0f);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)new Side(0)));
            Assert.That(_runner.Status.IsEnded, Is.True);
            Assert.That(_runner.Status.Winner, Is.EqualTo((Side?)new Side(0)));
            Assert.That(Controller(player).Definitions, Is.EqualTo(new[] { _handgun }), "the winner keeps the last weapon");
        }

        [Test]
        public void ASessionNeedsAValidLadder()
        {
            _runner.Ladder = null;
            Assert.Throws<InvalidOperationException>(() => _runner.CreateGunGameSession());

            var empty = Track(ScriptableObject.CreateInstance<GunGameLadderDefinition>());
            _runner.Ladder = empty;
            Assert.Throws<InvalidOperationException>(() => _runner.CreateGunGameSession());

            _runner.Ladder = _ladder;
            _ladder.TimeLimitSeconds = 0f;
            Assert.Throws<InvalidOperationException>(() => _runner.CreateGunGameSession());
        }

        [Test]
        public void TheSessionUsesTheLaddersTimeLimitAndRespawn()
        {
            _ladder.TimeLimitSeconds = 30f;
            _ladder.RespawnSeconds = 4f;
            var player = Member("Player", 0);
            var bot = Member("Bot-1", 1);
            Begin(player, bot);

            Assert.That(_runner.Session.TimeLimitSeconds, Is.EqualTo(30f));

            KillBy(player, bot, TestWeapons.HandgunId);

            Assert.That(_runner.Session.GetRespawnRemaining(1), Is.EqualTo(4f));
        }

        [Test]
        public void TheMatchEndsWhenTheLaddersTimeLimitRunsOut()
        {
            _ladder.TimeLimitSeconds = 30f;
            var player = Member("Player", 0);
            var bot = Member("Bot-1", 1);
            Begin(player, bot);

            _runner.Tick(29f);
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Playing));

            _runner.Tick(1f);
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
        }

        [Test]
        public void TheLadderAssetConvertsToTheCoreLadder()
        {
            var ladder = _ladder.ToLadder();

            Assert.That(ladder.TierCount, Is.EqualTo(3));
            Assert.That(ladder.Tiers[0].WeaponId, Is.EqualTo(TestWeapons.RifleId));
            Assert.That(ladder.Tiers[1].NameKey, Is.EqualTo(TestWeapons.LauncherNameKey));
            Assert.That(ladder.IsMelee(KnifeId), Is.True);
            Assert.That(ladder.DemotesOnMelee, Is.True);

            var broken = Track(ScriptableObject.CreateInstance<GunGameLadderDefinition>());
            broken.SetTiers(new WeaponDefinition[] { null });
            Assert.Throws<WeaponDefinitionException>(() => broken.ToLadder());
        }

        private void Begin(SideMember player, SideMember a)
        {
            _runner.Begin(_runner.CreateGunGameSession(), new[] { player, a }, Array.Empty<SideSpawnPoint>(), player);
        }

        private void Begin(SideMember player, SideMember a, SideMember b)
        {
            _runner.Begin(_runner.CreateGunGameSession(), new[] { player, a, b }, Array.Empty<SideSpawnPoint>(), player);
        }

        private static void KillBy(SideMember killer, SideMember victim, string weaponId)
        {
            using (CombatEvents.Attribute(killer.gameObject, weaponId))
            {
                victim.Damageable.Kill();
            }
        }

        private static WeaponController Controller(SideMember member)
        {
            return member.GetComponentInChildren<WeaponController>(true);
        }

        private SideMember Member(string name, int side)
        {
            var go = Track(new GameObject(name));
            go.transform.position = new Vector3(side * 5f, 0f, 0f);
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);

            var view = new GameObject(name + "-Camera");
            view.transform.SetParent(go.transform, false);
            view.AddComponent<WeaponController>();
            return member;
        }

        private WeaponDefinition Weapon(string name, WeaponSpec spec)
        {
            var definition = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            definition.name = name;
            WeaponAssets.Configure(definition, spec);
            return definition;
        }

        private GunGameStatus Detail()
        {
            Assert.That(_runner.Status, Is.Not.Null);
            Assert.That(_runner.Status.Detail, Is.InstanceOf<GunGameStatus>());
            return (GunGameStatus)_runner.Status.Detail;
        }

        private T Track<T>(T obj)
            where T : Object
        {
            _objects.Add(obj);
            return obj;
        }
    }
}

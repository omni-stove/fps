using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// GunGameRunner が段に合わせて持ち武器を替え、HUD に段と次の武器を出し、最後の段の武器で倒した人の勝ちで終えることを確かめる。
    /// Edit Mode では WeaponController が初期化されない (Awake が走らない) ので、持たせる武器は <see cref="WeaponController.Definitions"/> で見る。
    /// bot (EnemyAgent) の持ち替えは EnemyAgentTests で確かめる。
    /// </summary>
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
            _rifle = Weapon("Rifle", WeaponAssetBuilder.RifleSpec());
            _launcher = Weapon("Launcher", WeaponAssetBuilder.LauncherSpec());
            _handgun = Weapon("Handgun", WeaponAssetBuilder.HandgunSpec());
            _knife = Weapon(
                "Knife",
                new WeaponSpec(KnifeId, "Knife", WeaponCategory.Handgun, FireMode.Semi, 1, 0, 0.5f, 0.1f, 100, 2f, DamageFalloff.None));

            _ladder = Track(ScriptableObject.CreateInstance<GunGameLadderDefinition>());
            _ladder.name = "TestLadder";
            GunGameSceneBuilder.Configure(_ladder, new[] { _rifle, _launcher, _handgun }, new[] { _knife }, true);

            _runner = Track(new GameObject("Runner")).AddComponent<GunGameRunner>();
            _runner.Ladder = _ladder;
            _runner.RespawnSeconds = 0f;
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
            var hud = BoundHudView();
            Begin(player, bot, hud);
            Assert.That(hud.Lines, Is.EqualTo(new[] { "段 1/3 Rifle", "次: Launcher" }));

            LogAssert.Expect(LogType.Log, "[Mode] gungame promote player=0 tier=2/3 weapon=weapon.launcher");
            KillBy(player, bot, WeaponAssetBuilder.RifleId);

            Assert.That(_runner.GunGame.GetTier(0), Is.EqualTo(1));
            Assert.That(Controller(player).Definitions, Is.EqualTo(new[] { _rifle }), "the weapon is swapped at the next tick, not while firing");

            LogAssert.Expect(LogType.Log, "[Mode] gungame equip player=0 tier=2/3 weapon=weapon.launcher");
            _runner.Tick(0f);

            Assert.That(Controller(player).Definitions, Is.EqualTo(new[] { _launcher }));
            Assert.That(Controller(bot).Definitions, Is.EqualTo(new[] { _rifle }));
            Assert.That(hud.Lines, Is.EqualTo(new[] { "段 2/3 Launcher", "次: Handgun" }));
            Assert.That(_runner.Session.GetScore(new Side(0)), Is.EqualTo(1d), "kills still score");
        }

        [Test]
        public void AKillWithAnotherWeaponDoesNotAdvance()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-1", 1);
            Begin(player, bot);

            KillBy(player, bot, WeaponAssetBuilder.HandgunId);
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

            KillBy(a, player, WeaponAssetBuilder.RifleId);
            _runner.Tick(0f);
            KillBy(a, b, WeaponAssetBuilder.LauncherId);
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
        public void AKillWithTheLastWeaponWinsAndLogsTheWinner()
        {
            var player = Member("Player", 0);
            var a = Member("Bot-1", 1);
            var b = Member("Bot-2", 2);
            var hud = BoundHudView();
            Begin(player, a, b, hud);

            KillBy(player, a, WeaponAssetBuilder.RifleId);
            _runner.Tick(0f);
            KillBy(player, b, WeaponAssetBuilder.LauncherId);
            _runner.Tick(0f);
            Assert.That(hud.Lines, Is.EqualTo(new[] { "段 3/3 Handgun", "次: なし (この武器で倒せば勝ち)" }));

            LogAssert.Expect(LogType.Log, "[Mode] gungame finish player=0 weapon=weapon.handgun");
            KillBy(player, a, WeaponAssetBuilder.HandgunId);
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Playing));

            LogAssert.Expect(LogType.Log, "[Mode] ended winner=0");
            _runner.Tick(0f);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)new Side(0)));
            Assert.That(hud.ResultText, Is.EqualTo(Localizer.Resolve(ModeHudText.SideName(new Side(0))) + " の勝ち"));
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
            _runner.TimeLimitSeconds = 0f;
            Assert.Throws<ArgumentOutOfRangeException>(() => _runner.CreateGunGameSession());
        }

        [Test]
        public void TheLadderAssetConvertsToTheCoreLadder()
        {
            var ladder = _ladder.ToLadder();

            Assert.That(ladder.TierCount, Is.EqualTo(3));
            Assert.That(ladder.Tiers[0].WeaponId, Is.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(ladder.Tiers[1].NameKey, Is.EqualTo(WeaponAssetBuilder.LauncherNameKey));
            Assert.That(GameText.Resolve(ladder.Tiers[1].Name), Is.EqualTo("Launcher"));
            Assert.That(ladder.IsMelee(KnifeId), Is.True);
            Assert.That(ladder.DemotesOnMelee, Is.True);

            var broken = Track(ScriptableObject.CreateInstance<GunGameLadderDefinition>());
            broken.SetTiers(new WeaponDefinition[] { null });
            Assert.Throws<WeaponDefinitionException>(() => broken.ToLadder());

            Assert.Throws<ArgumentException>(
                () => GunGameSceneBuilder.Configure(broken, Array.Empty<WeaponDefinition>(), Array.Empty<WeaponDefinition>(), true));
            Assert.Throws<ArgumentException>(
                () => GunGameSceneBuilder.Configure(broken, new[] { _rifle }, new WeaponDefinition[] { null }, true));
        }

        private void Begin(SideMember player, SideMember a, ModeHudView hud = null)
        {
            _runner.Begin(_runner.CreateGunGameSession(), new[] { player, a }, Array.Empty<SideSpawnPoint>(), player, hud);
        }

        private void Begin(SideMember player, SideMember a, SideMember b, ModeHudView hud = null)
        {
            _runner.Begin(_runner.CreateGunGameSession(), new[] { player, a, b }, Array.Empty<SideSpawnPoint>(), player, hud);
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
            WeaponAssetBuilder.Configure(definition, spec);
            return definition;
        }

        private ModeHudView BoundHudView()
        {
            var view = Track(new GameObject("ModeHud")).AddComponent<ModeHudView>();
            view.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());
            return view;
        }

        private T Track<T>(T obj)
            where T : Object
        {
            _objects.Add(obj);
            return obj;
        }
    }
}

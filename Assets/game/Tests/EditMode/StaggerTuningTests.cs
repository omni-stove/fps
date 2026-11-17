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
    // Edit Mode では Awake が走らないので、取り付けの後に Bind を直接呼んで、怯みの値が届いたかを見る。
    public sealed class StaggerTuningTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GameObject _player;
        private Damageable _damageable;
        private Stagger _savedRule;

        [SetUp]
        public void SetUp()
        {
            _savedRule = StaggerReaction.ActiveRule;

            // プロジェクトの Resources/ShooterKit に置かれた入口の欄に左右されないよう、空の入口に替えておく。
            GameContent.Use(Content("Empty"));

            _player = Track(new GameObject("Player"));
            _player.AddComponent<PlayerController>();
            _damageable = _player.AddComponent<Damageable>();
            _damageable.Initialize("player", 100);
        }

        [TearDown]
        public void TearDown()
        {
            StaggerReaction.ActiveRule = _savedRule;
            StaggerReaction.ActiveSpec = null;
            GameContent.Use(null);
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
        public void TheKitShipsTheCurrentDefaultsInResources()
        {
            Assert.That(StaggerActionInstaller.TuningResourcePath, Is.EqualTo("Tuning/stagger"));
            Assert.That(Resources.Load<TextAsset>(StaggerActionInstaller.TuningResourcePath), Is.Not.Null);

            var tuning = StaggerActionInstaller.Tuning;

            Assert.That(tuning, Is.Not.SameAs(StaggerTuning.Default), "read from Resources/Tuning/stagger.json");
            AssertSameValues(tuning.Player, StaggerSpec.PlayerDefault);
            AssertSameValues(tuning.Enemy, StaggerSpec.EnemyDefault);
        }

        [Test]
        public void TheEntryFieldReachesThePlayer()
        {
            var content = Content("Game");
            content.StaggerTuning = Text("GameStagger", "{ \"player\": { \"flinchSeconds\": 0.3 } }");
            GameContent.Use(content);

            new StaggerActionInstaller().Install(_player, Rules(Stagger.On));
            var stagger = _player.GetComponent<PlayerStagger>();
            Assert.That(stagger.Bind(), Is.True);

            Assert.That(stagger.CreateSpec().FlinchSeconds, Is.EqualTo(0.3f));
            Assert.That(stagger.Judge.Spec.FlinchSeconds, Is.EqualTo(0.3f));
            Assert.That(stagger.Judge.Spec.StumbleSeconds, Is.EqualTo(StaggerSpec.DefaultPlayerStumbleSeconds), "omitted items keep the player defaults");

            LogAssert.Expect(LogType.Log, "[Stagger] id=player staggered kind=Flinch seconds=0.30 zone=Head damage=Bullet");
            _damageable.ApplyHit(25, HitZone.Head, 5f, DamageFalloff.None);

            Assert.That(stagger.Current, Is.EqualTo(StaggerKind.Flinch));
        }

        [Test]
        public void TheEntryFieldReachesEnemiesBoundBeforeAndAfterTheInstall()
        {
            var content = Content("Game");
            content.StaggerTuning = Text("GameStagger", "{ \"enemy\": { \"kneelSeconds\": 2 } }");
            GameContent.Use(content);
            var placed = Enemy("Placed");
            Assert.That(placed.Bind(), Is.True);
            Assert.That(placed.Judge.Spec.KneelSeconds, Is.EqualTo(StaggerSpec.DefaultEnemyKneelSeconds));

            new StaggerActionInstaller().Install(_player, Rules(Stagger.On));

            Assert.That(placed.Judge.Spec.KneelSeconds, Is.EqualTo(2f), "an enemy placed in the scene binds before the install");
            Assert.That(placed.CreateSpec().KneelSeconds, Is.EqualTo(2f));
            Assert.That(StaggerReaction.ActiveSpec.KneelSeconds, Is.EqualTo(2f));
            Assert.That(StaggerReaction.ActiveSpec.FlinchSeconds, Is.EqualTo(StaggerSpec.DefaultEnemyFlinchSeconds));

            var spawned = Enemy("Spawned");
            Assert.That(spawned.Bind(), Is.True);

            Assert.That(spawned.Judge.Spec.KneelSeconds, Is.EqualTo(2f), "an enemy spawned later takes the installed values");
        }

        [Test]
        public void ABrokenEntryFieldLogsTheItemAndUsesTheDefaults()
        {
            var content = Content("Broken");
            content.StaggerTuning = Text("BrokenStagger", "{ \"player\": { \"windowSeconds\": 0 } }");
            GameContent.Use(content);
            LogAssert.Expect(
                LogType.Error,
                new Regex(@"(?s)^\[Tuning\] stagger JSON 'GameContent 'Broken' \(BrokenStagger\)' item 'player\.windowSeconds' is invalid: .*; using the default values instead$"));

            Assert.That(StaggerActionInstaller.Tuning, Is.SameAs(StaggerTuning.Default));
        }

        [Test]
        public void ApplyingASpecWhileStaggeredEndsTheStaggerAndUsesTheNewValues()
        {
            var stagger = _player.AddComponent<PlayerStagger>();
            Assert.That(stagger.Bind(), Is.True);
            stagger.Rule = Stagger.On;
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stagger\] id=player staggered kind=Stumble "));
            _damageable.TakeDamage(5, Vector3.zero, Vector3.forward, DamageKind.Melee);
            Assert.That(stagger.IsStaggered, Is.True);

            var swapped = new StaggerSpec(0.3f, 0f, 0.4f, 0.6f, 20f, 8f, 1f, 1f, 1f, 1f, 0.5f, 0.5f);
            LogAssert.Expect(LogType.Log, "[Stagger] id=player recovered kind=Stumble");
            stagger.ApplySpec(swapped);

            Assert.That(stagger.IsStaggered, Is.False);
            Assert.That(stagger.Judge.Spec, Is.SameAs(swapped));
            Assert.That(stagger.CreateSpec().MoveMultiplier, Is.EqualTo(0.5f));
            Assert.Throws<System.ArgumentNullException>(() => stagger.ApplySpec(null));

            var reaction = Enemy("Enemy");
            Assert.That(reaction.Bind(), Is.True);
            reaction.ApplySpec(swapped);
            Assert.That(reaction.Judge.Spec, Is.SameAs(swapped), "a bound enemy takes the new values too");
            Assert.Throws<System.ArgumentNullException>(() => reaction.ApplySpec(null));
        }

        private static void AssertSameValues(StaggerSpec actual, StaggerSpec expected)
        {
            Assert.That(actual.FlinchSeconds, Is.EqualTo(expected.FlinchSeconds));
            Assert.That(actual.KneelSeconds, Is.EqualTo(expected.KneelSeconds));
            Assert.That(actual.StumbleSeconds, Is.EqualTo(expected.StumbleSeconds));
            Assert.That(actual.BlastSeconds, Is.EqualTo(expected.BlastSeconds));
            Assert.That(actual.HeadDamage, Is.EqualTo(expected.HeadDamage));
            Assert.That(actual.LimbDamage, Is.EqualTo(expected.LimbDamage));
            Assert.That(actual.MeleeDamage, Is.EqualTo(expected.MeleeDamage));
            Assert.That(actual.ExplosionDamage, Is.EqualTo(expected.ExplosionDamage));
            Assert.That(actual.WindowSeconds, Is.EqualTo(expected.WindowSeconds));
            Assert.That(actual.ImmunitySeconds, Is.EqualTo(expected.ImmunitySeconds));
            Assert.That(actual.MoveMultiplier, Is.EqualTo(expected.MoveMultiplier));
            Assert.That(actual.TurnMultiplier, Is.EqualTo(expected.TurnMultiplier));
        }

        private static GameRules Rules(Stagger stagger)
        {
            return GameRules.Default.WithActions(ActionRules.Default.WithStagger(stagger));
        }

        private StaggerReaction Enemy(string name)
        {
            var host = Track(new GameObject(name));
            host.AddComponent<Damageable>().Initialize(name, 100);
            return host.AddComponent<StaggerReaction>();
        }

        private GameContent Content(string name)
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = name;
            return content;
        }

        private TextAsset Text(string name, string json)
        {
            var asset = Track(new TextAsset(json));
            asset.name = name;
            return asset;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}

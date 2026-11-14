using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// SquadCommandController が照準の先に応じて味方の bot (EnemyAgent) に守る・狙う指示を出し、長押しで付いてこさせ、
    /// 分隊の無いときは近くの同じ陣営の bot にだけ出すことと、bot が指示を受けて応答の字幕を出し、狙う敵を目標にすることを Edit Mode の物理で確かめる。
    /// bot が実際に歩く部分は NavMesh が要るので、射撃場の smoke (SquadCommandSmokeStep) で確かめる。
    /// </summary>
    public sealed class SquadCommandControllerTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1f, 0f);

        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<SubtitleSpeech> _speech = new List<SubtitleSpeech>();
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
            SubtitleFeed.Spoken += OnSpoken;
        }

        [TearDown]
        public void TearDown()
        {
            SubtitleFeed.Spoken -= OnSpoken;
            _speech.Clear();
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void PointingAtTheGroundSendsNearbyAlliesToHoldIt()
        {
            var commander = Commander(0);
            var ally = Bot("Ally", 0, new Vector3(3f, 0f, 0f));
            var foe = Bot("Foe", 1, new Vector3(-3f, 0f, 0f));
            var faraway = Bot("Faraway", 0, new Vector3(0f, 0f, -60f));
            Floor();

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Squad\] accepted id=Ally kind=Hold "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Squad\] order kind=Hold target=none at=\(-?0\.00, -?0\.00, 5\.00\) recipients=1 squads=false$"));
            Assert.That(commander.TryOrderAlong(_eye, new Vector3(0f, -1f, 5f), out var recipients), Is.True);

            Assert.That(recipients, Is.EqualTo(1));
            Assert.That(commander.Recipients, Is.EqualTo(new[] { ally }));
            Assert.That(ally.Order.Kind, Is.EqualTo(BotErrandKind.Hold));
            Assert.That(ally.Order.Destination.Z, Is.EqualTo(5f).Within(1e-3f));
            Assert.That(ally.OrderLeader, Is.SameAs(commander.GetComponent<SideMember>()));
            Assert.That(foe.Order.IsOrder, Is.False, "an enemy bot is not ordered");
            Assert.That(faraway.Order.IsOrder, Is.False, "without squads a far bot is not ordered");

            Assert.That(commander.CurrentOrder, Is.EqualTo(BotErrandKind.Hold));
            Assert.That(commander.CurrentPoint.z, Is.EqualTo(5f).Within(1e-3f));
            Assert.That(commander.LastRefusal, Is.EqualTo(SquadOrderRefusal.None));
            Assert.That(_speech.Select(s => s.Text), Is.EqualTo(new[] { SquadOrderText.Reply("Ally", BotErrandKind.Hold) }));
        }

        [Test]
        public void PointingAtAnEnemyMakesAlliesTargetIt()
        {
            var commander = Commander(0);
            var ally = Bot("Ally", 0, new Vector3(3f, 0f, 0f));
            var enemy = Enemy("Enemy", new Vector3(0f, 1f, 10f));

            Assert.That(commander.TryOrderAlong(_eye, Vector3.forward, out _), Is.True);

            Assert.That(ally.Order.Kind, Is.EqualTo(BotErrandKind.Attack));
            Assert.That(ally.OrderTarget, Is.SameAs(enemy));
            Assert.That(commander.CurrentOrder, Is.EqualTo(BotErrandKind.Attack));
            Assert.That(commander.CurrentTarget, Is.SameAs(enemy));
            Assert.That(Vector3.Distance(commander.CurrentPoint, enemy.AimPoint), Is.LessThan(1e-4f));
        }

        [Test]
        public void AnAttackOrderEndsWhenTheTargetFallsAndTheBotFollows()
        {
            var commander = Commander(0);
            var ally = Bot("Ally", 0, new Vector3(3f, 0f, 0f));
            var enemy = Enemy("Enemy", new Vector3(0f, 1f, 10f));
            commander.TryOrderAlong(_eye, Vector3.forward, out _);
            Assert.That(ally.OrderTarget, Is.SameAs(enemy));

            enemy.Kill();
            commander.Tick();

            Assert.That(commander.CurrentOrder, Is.EqualTo(BotErrandKind.None), "the marker goes away with the target");
        }

        [Test]
        public void RecallCallsEveryAllyBackAndClearsTheMarker()
        {
            var commander = Commander(0);
            var ally = Bot("Ally", 0, new Vector3(3f, 0f, 0f));
            var other = Bot("Other", 0, new Vector3(-3f, 0f, 2f));
            Floor();
            commander.TryOrderAlong(_eye, new Vector3(0f, -1f, 5f), out _);
            _speech.Clear();

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Squad\] order kind=Follow target=none at=.* recipients=2 squads=false$"));
            Assert.That(commander.Recall(out var recipients), Is.True);

            Assert.That(recipients, Is.EqualTo(2));
            Assert.That(ally.Order.Kind, Is.EqualTo(BotErrandKind.Follow));
            Assert.That(other.Order.Kind, Is.EqualTo(BotErrandKind.Follow));
            Assert.That(commander.CurrentOrder, Is.EqualTo(BotErrandKind.None));
            Assert.That(_speech.Select(s => s.Speaker), Is.EquivalentTo(new[] { "Ally", "Other" }));
        }

        [Test]
        public void RefusesWithoutASideOrWithoutAllies()
        {
            var lone = Track(new GameObject("Lone"));
            lone.transform.position = _eye;
            lone.AddComponent<Damageable>().Initialize("Lone", Damageable.DefaultMaxHealth);
            var sideless = lone.AddComponent<SquadCommandController>();
            sideless.Configure();
            Floor();

            LogAssert.Expect(LogType.Log, SquadOrderLog.Refused(SquadOrderRefusal.NoSide));
            Assert.That(sideless.TryOrderAlong(_eye, new Vector3(0f, -1f, 5f), out _), Is.False);
            Assert.That(sideless.LastRefusal, Is.EqualTo(SquadOrderRefusal.NoSide));

            var commander = Commander(0);
            Bot("Foe", 1, new Vector3(3f, 0f, 0f));
            LogAssert.Expect(LogType.Log, SquadOrderLog.Refused(SquadOrderRefusal.NoRecipients));
            Assert.That(commander.TryOrderAlong(_eye, new Vector3(0f, -1f, 5f), out _), Is.False);
            Assert.That(commander.Recall(out _), Is.False);
            Assert.That(commander.LastRefusal, Is.EqualTo(SquadOrderRefusal.NoRecipients));
            Assert.That(commander.CurrentOrder, Is.EqualTo(BotErrandKind.None));
        }

        [Test]
        public void RefusesWhenNothingIsInReachOrTheCommanderIsDead()
        {
            var commander = Commander(0);
            Bot("Ally", 0, new Vector3(3f, 0f, 0f));

            Assert.That(commander.TryOrderAlong(_eye, Vector3.up, out _), Is.False);
            Assert.That(commander.LastRefusal, Is.EqualTo(SquadOrderRefusal.NoTarget));
            Assert.Throws<System.ArgumentException>(() => commander.TryOrderAlong(_eye, Vector3.zero, out _));

            commander.GetComponent<Damageable>().Kill();
            Assert.That(commander.Recall(out _), Is.False);
            Assert.That(commander.LastRefusal, Is.EqualTo(SquadOrderRefusal.Dead));
        }

        [Test]
        public void ADeadBotDoesNotTakeOrdersAndDropsItsOrderWhenItFalls()
        {
            var commander = Commander(0);
            var ally = Bot("Ally", 0, new Vector3(3f, 0f, 0f));
            var fallen = Bot("Fallen", 0, new Vector3(-3f, 0f, 0f));
            fallen.GetComponent<Damageable>().Kill();
            Floor();

            commander.TryOrderAlong(_eye, new Vector3(0f, -1f, 5f), out var recipients);
            Assert.That(recipients, Is.EqualTo(1));
            Assert.That(fallen.Order.IsOrder, Is.False);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Squad\] done id=Ally kind=Hold reason=Died next=None$"));
            ally.GetComponent<Damageable>().Kill();
            Assert.That(ally.Order.IsOrder, Is.False);
        }

        [Test]
        public void AnAttackOrderPinsTheBotsTarget()
        {
            var commander = Commander(0);
            var ally = Bot("Ally", 0, new Vector3(3f, 0f, 0f));
            var enemy = Enemy("Enemy", new Vector3(0f, 1f, 10f));

            Assert.That(ally.OrderAttack(enemy, commander.GetComponent<SideMember>(), SquadOrderSpec.Default), Is.True);
            Assert.That(ally.OrderAttack(ally.GetComponent<Damageable>(), commander.GetComponent<SideMember>(), SquadOrderSpec.Default), Is.False);
            Assert.That(ally.Order.Kind, Is.EqualTo(BotErrandKind.Attack), "the refused order did not replace the one it had");

            ally.ClearOrder("Test");
            Assert.That(ally.Order.IsOrder, Is.False);
            Assert.That(ally.OrderLeader, Is.Null);
            Assert.Throws<System.ArgumentNullException>(() => ally.OrderFollow(null, SquadOrderSpec.Default));
            Assert.Throws<System.ArgumentNullException>(() => ally.OrderAttack(null, commander.GetComponent<SideMember>(), SquadOrderSpec.Default));
        }

        [Test]
        public void SquadModesOrderTheWholeSquad()
        {
            var commander = Commander(0);
            Assert.That(SquadCommandController.InSquadMode(commander.GetComponent<SideMember>()), Is.False, "no mode is running");
            Assert.That(SquadCommandController.InSquadMode(null), Is.False);
            Assert.That(Track(new GameObject("Runner")).AddComponent<BattleRoyaleRunner>().HasSquads, Is.True);
        }

        [Test]
        public void TheInspectorValuesBecomeTheSpec()
        {
            var commander = Commander(0);
            commander.ApplySpec(new SquadOrderSpec(holdSeconds: 1f, nearbyRadius: 5f));
            commander.Configure();

            Assert.That(commander.Spec.HoldSeconds, Is.EqualTo(1f));
            Assert.That(commander.Spec.NearbyRadius, Is.EqualTo(5f));
            Assert.Throws<System.ArgumentNullException>(() => commander.ApplySpec(null));
        }

        private void OnSpoken(SubtitleSpeech speech)
        {
            _speech.Add(speech);
        }

        private SquadCommandController Commander(int side)
        {
            var go = Track(new GameObject("Commander"));
            go.transform.position = _eye;
            go.AddComponent<Damageable>().Initialize("Commander", Damageable.DefaultMaxHealth);
            go.AddComponent<SideMember>().Side = new Side(side);
            var commander = go.AddComponent<SquadCommandController>();
            commander.Configure();
            return commander;
        }

        private EnemyAgent Bot(string name, int side, Vector3 feet)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            go.name = name;
            go.transform.position = feet + Vector3.up;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            go.AddComponent<SideMember>().Side = new Side(side);
            var agent = go.AddComponent<EnemyAgent>();
            agent.Id = name;
            agent.WeaponDefinition = _rifle;
            Assert.That(agent.TryInitialize(), Is.True);
            Physics.SyncTransforms();
            return agent;
        }

        private Damageable Enemy(string name, Vector3 position)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            go.name = name;
            go.transform.position = position;
            var damageable = go.AddComponent<Damageable>();
            damageable.Initialize(name, Damageable.DefaultMaxHealth);
            Physics.SyncTransforms();
            return damageable;
        }

        // 照準 (目の高さ 1m から下向き) が z = 5 で当たる床。
        private void Floor()
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.name = "Floor";
            go.transform.position = new Vector3(0f, -0.5f, 0f);
            go.transform.localScale = new Vector3(40f, 1f, 40f);
            Physics.SyncTransforms();
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}

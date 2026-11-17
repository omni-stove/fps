using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class UavRewardTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1f, 0f);

        private readonly List<GameObject> _objects = new List<GameObject>();
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
            GameSignals.ResetAll();
            MinimapReveals.ResetAll();
        }

        [Test]
        public void RevealsOnlyLivingEnemiesOnTheMinimapForTheUavSeconds()
        {
            MinimapReveals.Clock = () => _now;
            var pings = User(side: 0);
            var enemy = Body("Enemy", new Vector3(0f, 1f, 10f), side: 1);
            var ally = Body("Ally", new Vector3(-5f, 1f, 10f), side: 0);
            var dead = Body("Dead", new Vector3(10f, 1f, 10f), side: 1);
            dead.Kill();

            UavReward.Use(pings.gameObject, pings, 10f);

            Assert.That(MinimapReveals.RemainingSeconds(enemy), Is.EqualTo(10f));
            Assert.That(MinimapReveals.IsRevealed(ally), Is.False);
            Assert.That(MinimapReveals.IsRevealed(dead), Is.False);
            Assert.That(MinimapReveals.IsRevealed(pings.GetComponent<Damageable>()), Is.False, "the user is not revealed");
            Assert.That(MinimapReveals.Count, Is.EqualTo(1));
        }

        [Test]
        public void RevealsEnemiesOnTheMinimapEvenWhenThePingRuleIsOff()
        {
            MinimapReveals.Clock = () => _now;
            var pings = User(side: 0);
            var first = Body("A", new Vector3(0f, 1f, 10f), side: 1);
            var second = Body("B", new Vector3(5f, 1f, 10f), side: 1);
            pings.Configure(GameRules.Default.WithActions(GameRules.Default.Actions.WithPing(Shooter.Core.Ping.Off)));

            Assert.That(UavReward.Use(pings.gameObject, pings, 10f), Is.Zero);
            Assert.That(pings.Markers, Is.Empty);
            Assert.That(MinimapReveals.IsRevealed(first), Is.True);
            Assert.That(MinimapReveals.IsRevealed(second), Is.True);
        }

        [Test]
        public void MarksOnlyLivingEnemiesOfTheUser()
        {
            var pings = User(side: 0);
            var enemy = Body("Enemy", new Vector3(0f, 1f, 10f), side: 1);
            var unaligned = Body("Dummy", new Vector3(5f, 1f, 10f), side: null);
            var ally = Body("Ally", new Vector3(-5f, 1f, 10f), side: 0);
            var dead = Body("Dead", new Vector3(10f, 1f, 10f), side: 1);
            dead.Kill();

            LogAssert.Expect(LogType.Log, "[Streak] uav user=User marked=2 seconds=10.00");
            var marked = UavReward.Use(pings.gameObject, pings, 10f);

            Assert.That(marked, Is.EqualTo(2));
            Assert.That(pings.IsMarked(enemy), Is.True);
            Assert.That(pings.IsMarked(unaligned), Is.True, "a body without a side is an enemy");
            Assert.That(pings.IsMarked(ally), Is.False);
            Assert.That(pings.IsMarked(dead), Is.False);
            Assert.That(pings.IsMarked(pings.GetComponent<Damageable>()), Is.False, "the user is not marked");
            Assert.That(pings.Markers.All(m => m.Kind == PingKind.Enemy && m.IsTracking), Is.True);
        }

        [Test]
        public void WithoutASideTheUserMarksEveryoneElse()
        {
            var pings = User(side: null);
            var sided = Body("Sided", new Vector3(0f, 1f, 10f), side: 0);
            var plain = Body("Plain", new Vector3(5f, 1f, 10f), side: null);

            Assert.That(UavReward.Use(pings.gameObject, pings, 10f), Is.EqualTo(2));
            Assert.That(pings.IsMarked(sided), Is.True);
            Assert.That(pings.IsMarked(plain), Is.True);
        }

        [Test]
        public void TheMarksDisappearAfterTheGivenSeconds()
        {
            var pings = User(side: 0);
            var enemy = Body("Enemy", new Vector3(0f, 1f, 10f), side: 1);

            UavReward.Use(pings.gameObject, pings, 10f);
            var marker = pings.Markers.Single();
            Assert.That(marker.ExpiresAt - marker.PlacedAt, Is.EqualTo(10f), "the UAV's seconds, not the ping lifetime");

            pings.Tick(9.99f);
            Assert.That(pings.IsMarked(enemy), Is.True);

            LogAssert.Expect(LogType.Log, new Regex($@"^\[Ping\] expired id={marker.Id} kind=Enemy "));
            pings.Tick(10f);
            Assert.That(pings.IsMarked(enemy), Is.False);
            Assert.That(pings.Markers, Is.Empty);
            Assert.That(pings.TargetOf(marker.Id), Is.Null);
        }

        [Test]
        public void TheMarksFollowEnemiesOutOfSight()
        {
            var pings = User(side: 0);
            var enemy = Body("Enemy", new Vector3(0f, 1f, 10f), side: 1);
            Wall(new Vector3(0f, 1f, 5f));

            UavReward.Use(pings.gameObject, pings, 10f);
            var id = pings.Markers.Single().Id;

            enemy.transform.position = new Vector3(2f, 1f, 12f);
            Physics.SyncTransforms();
            pings.Tick(1f);

            Assert.That(pings.TryGetMarker(id, out var followed), Is.True);
            Assert.That(followed.IsTracking, Is.True, "a revealed enemy is not lost behind the wall");
            Assert.That(Vector3.Distance(followed.Position.ToUnity(), enemy.AimPoint), Is.LessThan(1e-4f));
        }

        [Test]
        public void MarksEveryEnemyBeyondThePingLimit()
        {
            var pings = User(side: 0, new PingSpec(maxMarkers: 1));
            var enemies = new[]
            {
                Body("A", new Vector3(0f, 1f, 10f), side: 1),
                Body("B", new Vector3(5f, 1f, 10f), side: 1),
                Body("C", new Vector3(-5f, 1f, 10f), side: 1),
            };

            Assert.That(UavReward.Use(pings.gameObject, pings, 10f), Is.EqualTo(3));
            Assert.That(enemies.All(pings.IsMarked), Is.True);
        }

        [Test]
        public void MarksNothingWhenThePingRuleIsOff()
        {
            var pings = User(side: 0);
            Body("Enemy", new Vector3(0f, 1f, 10f), side: 1);
            pings.Configure(GameRules.Default.WithActions(GameRules.Default.Actions.WithPing(Shooter.Core.Ping.Off)));

            Assert.That(UavReward.Use(pings.gameObject, pings, 10f), Is.Zero);
            Assert.That(pings.Markers, Is.Empty);
            Assert.That(pings.LastRefusal, Is.EqualTo(PingRefusal.RuleOff));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var pings = User(side: 0);

            Assert.Throws<ArgumentNullException>(() => UavReward.Use(null, pings, 10f));
            Assert.Throws<ArgumentNullException>(() => UavReward.Use(pings.gameObject, null, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => UavReward.Use(pings.gameObject, pings, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => UavReward.Use(pings.gameObject, pings, float.NaN));
            Assert.Throws<ArgumentNullException>(() => pings.TryMarkEnemy(null, 10f, out _));
            Assert.That(pings.Markers, Is.Empty);
        }

        private PingController User(int? side, PingSpec spec = null)
        {
            var go = Track(new GameObject("User"));
            go.transform.position = _eye;
            go.AddComponent<Damageable>().Initialize("User", Damageable.DefaultMaxHealth);
            if (side.HasValue)
            {
                go.AddComponent<SideMember>().Side = new Side(side.Value);
            }

            var pings = go.AddComponent<PingController>();
            if (spec != null)
            {
                pings.ApplySpec(spec);
            }

            pings.Clock = () => _now;
            pings.Configure(GameRules.Default.WithActions(GameRules.Default.Actions.WithPing(Shooter.Core.Ping.On)));
            return pings;
        }

        private Damageable Body(string name, Vector3 position, int? side)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            go.name = name;
            go.transform.position = position;
            var damageable = go.AddComponent<Damageable>();
            damageable.Initialize(name, Damageable.DefaultMaxHealth);
            if (side.HasValue)
            {
                go.AddComponent<SideMember>().Side = new Side(side.Value);
            }

            Physics.SyncTransforms();
            return damageable;
        }

        private void Wall(Vector3 center)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.name = "Wall";
            go.transform.position = center;
            go.transform.localScale = new Vector3(8f, 4f, 0.5f);
            Physics.SyncTransforms();
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}

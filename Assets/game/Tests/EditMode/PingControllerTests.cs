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
    /// PingController が照準の先の敵か場所に印を付け、敵を見えている間だけ追い、見失うと残し、寿命で消し、
    /// 同じ陣営の bot (EnemySenses.SelectHostile) に印の付いた敵を優先させることを、Edit Mode の物理で確かめる。
    /// </summary>
    public sealed class PingControllerTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1f, 0f);

        private readonly List<GameObject> _objects = new List<GameObject>();
        private float _now;

        private static GameRules PingRules(Fps.Core.Ping ping)
        {
            return GameRules.Default.WithActions(GameRules.Default.Actions.WithPing(ping));
        }

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
        }

        [Test]
        public void RefusesUntilTheRuleTurnsPingOn()
        {
            var go = Track(new GameObject("Unconfigured"));
            go.transform.position = _eye;
            var unconfigured = go.AddComponent<PingController>();
            Enemy("Enemy", new Vector3(0f, 1f, 10f));

            Assert.That(unconfigured.TryPingAlong(_eye, Vector3.forward, out _), Is.False);
            Assert.That(unconfigured.LastRefusal, Is.EqualTo(PingRefusal.RuleOff));

            unconfigured.Configure(PingRules(Fps.Core.Ping.Off));
            LogAssert.Expect(LogType.Log, PingLog.Refused(PingRefusal.RuleOff));
            Assert.That(unconfigured.TryPingAlong(_eye, Vector3.forward, out _), Is.False);
            Assert.That(unconfigured.Markers, Is.Empty);
            Assert.That(unconfigured.UsesPing, Is.False);
        }

        [Test]
        public void MarksTheEnemyUnderTheAim()
        {
            var pinger = Pinger();
            var enemy = Enemy("Enemy", new Vector3(0f, 1f, 10f));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Ping\] placed id=1 kind=Enemy target=Enemy "));
            Assert.That(pinger.TryPingAlong(_eye, Vector3.forward, out var marker), Is.True);

            Assert.That(marker.Kind, Is.EqualTo(PingKind.Enemy));
            Assert.That(marker.IsTracking, Is.True);
            Assert.That(pinger.TargetOf(marker.Id), Is.SameAs(enemy));
            Assert.That(pinger.IsMarked(enemy), Is.True);
            Assert.That(pinger.LastRefusal, Is.EqualTo(PingRefusal.None));
            Assert.That(Vector3.Distance(marker.Position.ToUnity(), enemy.AimPoint), Is.LessThan(1e-4f));
        }

        [Test]
        public void MarksAPlaceWhenNoEnemyIsUnderTheAim()
        {
            var pinger = Pinger();
            Wall(new Vector3(0f, 1f, 10f));

            Assert.That(pinger.TryPingAlong(_eye, Vector3.forward, out var marker), Is.True);

            Assert.That(marker.Kind, Is.EqualTo(PingKind.Location));
            Assert.That(marker.Position.Z, Is.EqualTo(9.75f).Within(1e-3f), "the mark sits on the face of the wall");
            Assert.That(pinger.TargetOf(marker.Id), Is.Null);
        }

        [Test]
        public void RefusesWhenNothingIsInReach()
        {
            var pinger = Pinger();

            LogAssert.Expect(LogType.Log, PingLog.Refused(PingRefusal.NoTarget));
            Assert.That(pinger.TryPingAlong(_eye, Vector3.up, out _), Is.False);
            Assert.That(pinger.LastRefusal, Is.EqualTo(PingRefusal.NoTarget));
            Assert.Throws<System.ArgumentException>(() => pinger.TryPingAlong(_eye, Vector3.zero, out _));
        }

        [Test]
        public void RefusesWhileThePingerIsDead()
        {
            var pinger = Pinger();
            Enemy("Enemy", new Vector3(0f, 1f, 10f));
            pinger.GetComponent<Damageable>().Kill();

            Assert.That(pinger.TryPingAlong(_eye, Vector3.forward, out _), Is.False);
            Assert.That(pinger.LastRefusal, Is.EqualTo(PingRefusal.Dead));
        }

        [Test]
        public void FollowsAVisibleEnemyAndStaysWhereItWasLost()
        {
            var pinger = Pinger();
            var enemy = Enemy("Enemy", new Vector3(0f, 1f, 10f));
            pinger.TryPingAlong(_eye, Vector3.forward, out var placed);

            enemy.transform.position = new Vector3(1f, 1f, 10f);
            Physics.SyncTransforms();
            pinger.Tick(_now);
            pinger.TryGetMarker(placed.Id, out var followed);
            Assert.That(Vector3.Distance(followed.Position.ToUnity(), enemy.AimPoint), Is.LessThan(1e-4f), "a visible enemy is followed");
            var lastSeen = followed.Position.ToUnity();

            Wall(new Vector3(0.5f, 1f, 5f));
            enemy.transform.position = new Vector3(0f, 1f, 12f);
            Physics.SyncTransforms();
            LogAssert.Expect(LogType.Log, new Regex($@"^\[Ping\] lost id={placed.Id} target=Enemy "));
            pinger.Tick(_now);

            Assert.That(pinger.TryGetMarker(placed.Id, out var lost), Is.True);
            Assert.That(lost.IsTracking, Is.False);
            Assert.That(Vector3.Distance(lost.Position.ToUnity(), lastSeen), Is.LessThan(1e-4f), "the mark stays where the enemy was last seen");
            Assert.That(pinger.IsMarked(enemy), Is.True, "a lost enemy is still marked");
        }

        [Test]
        public void MarksExpireAfterTheirLifetime()
        {
            var pinger = Pinger(new PingSpec(lifetimeSeconds: 2f));
            Enemy("Enemy", new Vector3(0f, 1f, 10f));
            pinger.TryPingAlong(_eye, Vector3.forward, out var placed);

            pinger.Tick(1.9f);
            Assert.That(pinger.Markers, Has.Count.EqualTo(1));

            LogAssert.Expect(LogType.Log, new Regex($@"^\[Ping\] expired id={placed.Id} kind=Enemy "));
            pinger.Tick(2f);
            Assert.That(pinger.Markers, Is.Empty);
            Assert.That(pinger.TargetOf(placed.Id), Is.Null);
        }

        [Test]
        public void KeepsOnlyTheConfiguredNumberOfMarks()
        {
            var pinger = Pinger(new PingSpec(maxMarkers: 2));
            Wall(new Vector3(0f, 1f, 10f));

            pinger.TryPingAlong(_eye, Vector3.forward, out var first);
            pinger.TryPingAlong(_eye, new Vector3(0.1f, 0f, 1f), out _);
            pinger.TryPingAlong(_eye, new Vector3(-0.1f, 0f, 1f), out _);

            Assert.That(pinger.Markers, Has.Count.EqualTo(2));
            Assert.That(pinger.TryGetMarker(first.Id, out _), Is.False, "the oldest mark makes room");
        }

        [Test]
        public void ForgetsAnEnemyThatDies()
        {
            var pinger = Pinger();
            var enemy = Enemy("Enemy", new Vector3(0f, 1f, 10f));
            pinger.TryPingAlong(_eye, Vector3.forward, out var placed);

            enemy.Kill();
            LogAssert.Expect(LogType.Log, $"[Ping] removed id={placed.Id} reason=TargetGone target=Enemy");
            pinger.Tick(_now);

            Assert.That(pinger.Markers, Is.Empty);
            Assert.That(pinger.IsMarked(enemy), Is.False);
        }

        [Test]
        public void DoesNotMarkAnAllyAsAnEnemy()
        {
            var pinger = Pinger();
            pinger.gameObject.AddComponent<SideMember>().Side = new Side(0);
            var ally = Enemy("Ally", new Vector3(0f, 1f, 10f));
            ally.gameObject.AddComponent<SideMember>().Side = new Side(0);

            Assert.That(pinger.TryPingAlong(_eye, Vector3.forward, out var marker), Is.True);

            Assert.That(marker.Kind, Is.EqualTo(PingKind.Location), "pinging an ally marks the spot instead");
            Assert.That(pinger.IsMarked(ally), Is.False);
        }

        [Test]
        public void ConfiguringAgainClearsTheMarks()
        {
            var pinger = Pinger();
            Wall(new Vector3(0f, 1f, 10f));
            pinger.TryPingAlong(_eye, Vector3.forward, out _);

            LogAssert.Expect(LogType.Log, PingLog.Cleared(1));
            pinger.Configure(PingRules(Fps.Core.Ping.Off));

            Assert.That(pinger.Markers, Is.Empty);
            Assert.That(pinger.Rule, Is.EqualTo(Fps.Core.Ping.Off));
        }

        [Test]
        public void AlliesOfThePingerPreferTheMarkedEnemy()
        {
            var bot = Member("Bot", 0, Vector3.zero);
            var near = Member("Near", 1, new Vector3(0f, 0f, 5f));
            var far = Member("Far", 1, new Vector3(20f, 0f, 20f));
            var pinger = Pinger();
            pinger.transform.position = new Vector3(20f, 1f, 0f);
            pinger.gameObject.AddComponent<SideMember>().Side = new Side(0);
            Physics.SyncTransforms();
            var members = new[] { bot, near, far };

            Assert.That(EnemySenses.SelectHostile(bot, members), Is.SameAs(near), "without a mark the nearest enemy is chosen");

            var from = pinger.transform.position;
            Assert.That(pinger.TryPingAlong(from, far.Damageable.AimPoint - from, out _), Is.True);
            Assert.That(PingController.IsMarkedFor(new Side(0), far.Damageable), Is.True);
            Assert.That(PingController.IsMarkedFor(new Side(1), far.Damageable), Is.False, "the other side does not see the mark");
            Assert.That(EnemySenses.SelectHostile(bot, members), Is.SameAs(far));
            Assert.That(EnemySenses.SelectHostile(near, members), Is.SameAs(bot), "the marked enemy's own side ignores the mark");

            pinger.enabled = false;
            Assert.That(PingController.IsMarkedFor(new Side(0), far.Damageable), Is.False, "a disabled pinger shares nothing");
            pinger.enabled = true;

            far.Damageable.Kill();
            Assert.That(EnemySenses.SelectHostile(bot, members), Is.SameAs(near), "with the marked enemy down, the nearest is chosen again");
        }

        [Test]
        public void BindViewHandsTheMarksToTheView()
        {
            var pinger = Pinger();
            var first = new FakeView();
            var second = new FakeView();

            LogAssert.Expect(LogType.Log, PingLog.View(nameof(FakeView)));
            pinger.BindView(first);
            Assert.That(first.Source, Is.SameAs(pinger));
            Assert.That(pinger.View, Is.SameAs(first));

            pinger.BindView(second);
            Assert.That(first.Source, Is.Null, "the old view is released");
            Assert.That(second.Source, Is.SameAs(pinger));

            pinger.BindView(null);
            Assert.That(second.Source, Is.Null);
            Assert.That(pinger.View, Is.Null);
        }

        [Test]
        public void ApplySpecIsUsedFromTheNextConfigure()
        {
            var pinger = Pinger();
            pinger.ApplySpec(new PingSpec(lifetimeSeconds: 9f, maxMarkers: 5));

            Assert.That(pinger.Spec.MaxMarkers, Is.EqualTo(PingSpec.DefaultMaxMarkers), "the running spec does not change mid-match");

            pinger.Configure(PingRules(Fps.Core.Ping.On));
            Assert.That(pinger.Spec.LifetimeSeconds, Is.EqualTo(9f));
            Assert.That(pinger.Spec.MaxMarkers, Is.EqualTo(5));
            Assert.Throws<System.ArgumentNullException>(() => pinger.ApplySpec(null));
            Assert.Throws<System.ArgumentNullException>(() => pinger.Configure(null));
        }

        [Test]
        public void TheIntervalIsKeptWithTheOtherValues()
        {
            var pinger = Pinger();
            pinger.ApplySpec(new PingSpec(minIntervalSeconds: 2f));

            pinger.Configure(PingRules(Fps.Core.Ping.On));

            Assert.That(pinger.Spec.MinIntervalSeconds, Is.EqualTo(2f));
            Assert.That(pinger.CreateSpec().MinIntervalSeconds, Is.EqualTo(2f), "the applied values are the serialized ones");
        }

        private PingController Pinger(PingSpec spec = null)
        {
            var go = Track(new GameObject("Pinger"));
            go.transform.position = _eye;
            go.AddComponent<Damageable>().Initialize("Pinger", Damageable.DefaultMaxHealth);
            var pinger = go.AddComponent<PingController>();
            if (spec != null)
            {
                pinger.ApplySpec(spec);
            }

            pinger.Clock = () => _now;
            pinger.Configure(PingRules(Fps.Core.Ping.On));
            return pinger;
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

        private void Wall(Vector3 center)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.name = "Wall";
            go.transform.position = center;
            go.transform.localScale = new Vector3(4f, 4f, 0.5f);
            Physics.SyncTransforms();
        }

        private SideMember Member(string name, int side, Vector3 position)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            go.name = name;
            go.transform.position = position + new Vector3(0f, 1f, 0f);
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);
            return member;
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }

        private sealed class FakeView : IPingView
        {
            public PingController Source { get; private set; }

            public void Show(PingController source)
            {
                Source = source;
            }
        }
    }
}

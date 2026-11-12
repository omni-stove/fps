using System;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class TargetDummyTests
    {
        private static readonly Vector3 Home = new Vector3(3f, 0f, 7f);
        private static readonly Quaternion HomeRotation = Quaternion.Euler(0f, 180f, 0f);

        private TargetDummy _dummy;

        [SetUp]
        public void SetUp()
        {
            _dummy = TargetDummy.Create("dummy", Home, HomeRotation, null, 50, TargetDummy.DefaultRespawnDelay);
        }

        [TearDown]
        public void TearDown()
        {
            if (_dummy != null)
            {
                Object.DestroyImmediate(_dummy.gameObject);
            }
        }

        [Test]
        public void CreateBuildsBodyAndHeadHitboxesOwnedByTheDamageable()
        {
            Assert.That(_dummy.name, Is.EqualTo("dummy"));
            Assert.That(_dummy.IsInitialized, Is.True);
            Assert.That(_dummy.Damageable, Is.SameAs(_dummy.GetComponent<Damageable>()));
            Assert.That(_dummy.Damageable.Id, Is.EqualTo("dummy"));
            Assert.That(_dummy.Damageable.Health.Max, Is.EqualTo(50));
            Assert.That(_dummy.transform.position, Is.EqualTo(Home));
            Assert.That(_dummy.HomePosition, Is.EqualTo(Home));

            var body = _dummy.transform.Find(TargetDummy.BodyName);
            var head = _dummy.transform.Find(TargetDummy.HeadName);
            Assert.That(body.GetComponent<CapsuleCollider>(), Is.Not.Null);
            Assert.That(head.GetComponent<SphereCollider>(), Is.Not.Null);
            Assert.That(body.localPosition, Is.EqualTo(TargetDummy.BodyLocalPosition));
            Assert.That(head.localPosition, Is.EqualTo(TargetDummy.HeadLocalPosition));
            Assert.That(head.localPosition.y, Is.GreaterThan(body.localPosition.y), "the head sits on top of the body");

            var bodyHitbox = body.GetComponent<Hitbox>();
            var headHitbox = head.GetComponent<Hitbox>();
            Assert.That(bodyHitbox.Zone, Is.EqualTo(HitZone.Body));
            Assert.That(headHitbox.Zone, Is.EqualTo(HitZone.Head));
            Assert.That(bodyHitbox.Owner, Is.SameAs(_dummy.Damageable));
            Assert.That(headHitbox.Owner, Is.SameAs(_dummy.Damageable));
        }

        [Test]
        public void CreateParentsUnderTheGivenTransform()
        {
            var parent = new GameObject("Parent");
            try
            {
                parent.transform.position = new Vector3(10f, 0f, 0f);
                var child = TargetDummy.Create("child", Home, Quaternion.identity, parent.transform);

                Assert.That(child.transform.parent, Is.SameAs(parent.transform));
                Assert.That(child.transform.position, Is.EqualTo(Home), "the position is in world space");
                Assert.That(child.Damageable.Health.Max, Is.EqualTo(TargetDummy.DefaultMaxHealth));
                Assert.That(child.RespawnDelay, Is.EqualTo(TargetDummy.DefaultRespawnDelay));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void CreateRejectsBadArgumentsWithoutLeavingObjectsBehind()
        {
            Assert.Throws<ArgumentException>(() => TargetDummy.Create(string.Empty, Home, Quaternion.identity));
            Assert.Throws<ArgumentOutOfRangeException>(() => TargetDummy.Create("bad-hp", Home, Quaternion.identity, null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TargetDummy.Create("bad-delay", Home, Quaternion.identity, null, 10, -1f));

            Assert.That(GameObject.Find("bad-hp"), Is.Null);
            Assert.That(GameObject.Find("bad-delay"), Is.Null);
        }

        [Test]
        public void DyingLogsHidesTheBodyAndStartsTheCountdown()
        {
            LogAssert.Expect(LogType.Log, "[Combat] target died id=dummy");
            _dummy.Damageable.Kill();

            Assert.That(_dummy.Damageable.IsDead, Is.True);
            Assert.That(_dummy.IsWaitingToRespawn, Is.True);
            Assert.That(_dummy.RemainingRespawnSeconds, Is.EqualTo(TargetDummy.DefaultRespawnDelay));
            foreach (var collider in _dummy.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(collider.enabled, Is.False, $"{collider.name} must not soak up shots while dead");
            }

            foreach (var renderer in _dummy.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(renderer.enabled, Is.False);
            }
        }

        [Test]
        public void RespawnsAtHomeWithFullHealthAfterThreeSeconds()
        {
            LogAssert.Expect(LogType.Log, "[Combat] target died id=dummy");
            _dummy.Damageable.Kill();
            _dummy.transform.SetPositionAndRotation(new Vector3(-5f, 2f, 1f), Quaternion.identity);

            Assert.That(_dummy.Tick(2.5f), Is.False);
            Assert.That(_dummy.Damageable.IsDead, Is.True);

            LogAssert.Expect(LogType.Log, TargetDummy.FormatRespawnedLog("dummy"));
            Assert.That(_dummy.Tick(0.5f), Is.True);

            Assert.That(_dummy.IsWaitingToRespawn, Is.False);
            Assert.That(_dummy.Damageable.IsDead, Is.False);
            Assert.That(_dummy.Damageable.Health.Current, Is.EqualTo(50));
            Assert.That(_dummy.transform.position, Is.EqualTo(Home));
            Assert.That(Quaternion.Angle(_dummy.transform.rotation, HomeRotation), Is.LessThan(1e-3f));
            foreach (var collider in _dummy.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(collider.enabled, Is.True);
            }

            foreach (var renderer in _dummy.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(renderer.enabled, Is.True);
            }

            Assert.That(_dummy.Tick(10f), Is.False, "respawns only once per death");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CanBeKilledAgainAfterRespawning()
        {
            LogAssert.Expect(LogType.Log, "[Combat] target died id=dummy");
            _dummy.Damageable.Kill();
            LogAssert.Expect(LogType.Log, TargetDummy.FormatRespawnedLog("dummy"));
            _dummy.Tick(TargetDummy.DefaultRespawnDelay);

            // ResetHealth で Health を作り直しても Died の購読は続くので、2 回目の死亡でも復活を待つ。
            LogAssert.Expect(LogType.Log, "[Combat] target died id=dummy");
            _dummy.Damageable.Kill();

            Assert.That(_dummy.IsWaitingToRespawn, Is.True);
        }

        [Test]
        public void DoesNothingWhileAlive()
        {
            Assert.That(_dummy.Tick(100f), Is.False);
            Assert.That(_dummy.IsWaitingToRespawn, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ReinitializingKeepsASingleSubscription()
        {
            _dummy.Initialize(_dummy.Damageable, 1f);
            _dummy.Initialize(_dummy.Damageable, 1f);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=dummy");
            _dummy.Damageable.Kill();
            LogAssert.Expect(LogType.Log, TargetDummy.FormatRespawnedLog("dummy"));

            Assert.That(_dummy.Tick(1f), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void InitializeRejectsBadArgumentsAndKeepsTheOldSettings()
        {
            Assert.Throws<ArgumentNullException>(() => _dummy.Initialize(null, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _dummy.Initialize(_dummy.Damageable, float.NaN));

            Assert.That(_dummy.RespawnDelay, Is.EqualTo(TargetDummy.DefaultRespawnDelay));
        }

        [Test]
        public void ShotsPassThroughADeadDummy()
        {
            var behind = TargetDummy.Create("behind", Home + new Vector3(0f, 0f, 3f), Quaternion.identity);
            try
            {
                LogAssert.Expect(LogType.Log, "[Combat] target died id=dummy");
                _dummy.Damageable.Kill();
                Physics.SyncTransforms();

                var origin = Home + new Vector3(0f, 1f, -5f);
                Assert.That(Hitscan.TryFirstHit(origin, Vector3.forward, 20f, null, out var hit), Is.True);
                Assert.That(Hitbox.FindFor(hit.collider).Owner, Is.SameAs(behind.Damageable));
            }
            finally
            {
                Object.DestroyImmediate(behind.gameObject);
            }
        }
    }
}

using System;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class HitboxDamageableTests
    {
        private GameObject _root;
        private Damageable _damageable;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Dummy");
            _damageable = _root.AddComponent<Damageable>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        private Hitbox AddPart(string partName, HitZone zone, bool wireOwner = true)
        {
            var part = new GameObject(partName, typeof(BoxCollider));
            part.transform.SetParent(_root.transform, false);
            var hitbox = part.AddComponent<Hitbox>();
            if (wireOwner)
            {
                hitbox.Initialize(zone, _damageable);
            }

            return hitbox;
        }

        [Test]
        public void HealthStartsFullAtMaxHealth()
        {
            Assert.That(_damageable.Health.Max, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(_damageable.Health.Current, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(_damageable.IsDead, Is.False);
        }

        [Test]
        public void MaxHealthChangeAfterHealthExistsAppliesOnNextReset()
        {
            _damageable.MaxHealth = 50;
            Assert.That(_damageable.Health.Max, Is.EqualTo(50));

            _damageable.MaxHealth = 80;
            Assert.That(_damageable.MaxHealth, Is.EqualTo(80));
            Assert.That(_damageable.Health.Max, Is.EqualTo(50), "the current Health keeps its max");

            _damageable.ResetHealth();
            Assert.That(_damageable.Health.Max, Is.EqualTo(80));
            Assert.That(_damageable.Health.Current, Is.EqualTo(80));
        }

        [Test]
        public void IsDeadDoesNotCreateHealth()
        {
            var host = new GameObject("Lazy");
            try
            {
                var lazy = host.AddComponent<Damageable>();

                Assert.That(lazy.IsDead, Is.False);

                // Health がまだ無ければ、作られた Health に後から設定した MaxHealth が反映される。
                lazy.MaxHealth = 30;
                Assert.That(lazy.Health.Max, Is.EqualTo(30));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void DiedIsStillRaisedAfterResetHealth()
        {
            var deaths = 0;
            _damageable.Died += _ => deaths++;

            LogAssert.Expect(LogType.Log, "[Combat] target died id=Dummy");
            _damageable.Kill();
            Assert.That(_damageable.IsDead, Is.True);

            _damageable.ResetHealth();
            Assert.That(_damageable.IsDead, Is.False);
            Assert.That(_damageable.Health.Current, Is.EqualTo(Damageable.DefaultMaxHealth));

            LogAssert.Expect(LogType.Log, "[Combat] target died id=Dummy");
            _damageable.ApplyHit(Damageable.DefaultMaxHealth, HitZone.Body, 0f, DamageFalloff.None);

            Assert.That(_damageable.IsDead, Is.True);
            Assert.That(deaths, Is.EqualTo(2));
        }

        [Test]
        public void ApplyHitReturnsTheCalculatedDamageEvenWhenDead()
        {
            var expected = DamageCalculator.Calculate(25, HitZone.Head, 0f, DamageFalloff.None);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=Dummy");
            _damageable.Kill();

            Assert.That(_damageable.ApplyHit(25, HitZone.Head, 0f, DamageFalloff.None), Is.EqualTo(expected));
            Assert.That(_damageable.Health.Current, Is.Zero);
        }

        [Test]
        public void MaxHealthMustBePositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _damageable.MaxHealth = 0);
            Assert.That(_damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
        }

        [Test]
        public void ApplyHitPassesTheCalculatedDamageToHealth()
        {
            var expected = DamageCalculator.Calculate(25, HitZone.Body, 5f, DamageFalloff.None);

            var damage = _damageable.ApplyHit(25, HitZone.Body, 5f, DamageFalloff.None);

            Assert.That(damage, Is.EqualTo(expected));
            Assert.That(_damageable.Health.Current, Is.EqualTo(Damageable.DefaultMaxHealth - expected));
        }

        [Test]
        public void HeadHitboxDoublesTheDamage()
        {
            var head = AddPart("Head", HitZone.Head);

            var damage = head.ApplyHit(25, 0f, DamageFalloff.None);

            Assert.That(damage, Is.EqualTo(50));
            Assert.That(_damageable.Health.Current, Is.EqualTo(50));
        }

        [Test]
        public void LimbHitboxAppliesFalloff()
        {
            var limb = AddPart("Arm", HitZone.Limb);
            var falloff = new DamageFalloff(10f, 20f, 0.5f);

            // 20m 以上は 0.5 倍、手足は 0.75 倍: 40 * 0.75 * 0.5 = 15。
            var damage = limb.ApplyHit(40, 30f, falloff);

            Assert.That(damage, Is.EqualTo(15));
            Assert.That(_damageable.Health.Current, Is.EqualTo(85));
        }

        [Test]
        public void HitboxFindsItsOwnerInParentsWhenNotWired()
        {
            var body = AddPart("Body", HitZone.Body, wireOwner: false);

            Assert.That(body.Zone, Is.EqualTo(HitZone.Body));
            Assert.That(body.Owner, Is.SameAs(_damageable));

            body.ApplyHit(10, 0f, DamageFalloff.None);
            Assert.That(_damageable.Health.Current, Is.EqualTo(90));
        }

        [Test]
        public void HitboxWithoutOwnerThrows()
        {
            var orphan = new GameObject("Orphan");
            try
            {
                var hitbox = orphan.AddComponent<Hitbox>();

                Assert.That(hitbox.Owner, Is.Null);
                Assert.Throws<InvalidOperationException>(() => hitbox.ApplyHit(10, 0f, DamageFalloff.None));
                Assert.Throws<ArgumentNullException>(() => hitbox.Initialize(HitZone.Head, null));
                Assert.Throws<ArgumentOutOfRangeException>(() => hitbox.Initialize((HitZone)99, _damageable));
            }
            finally
            {
                Object.DestroyImmediate(orphan);
            }
        }

        [Test]
        public void HealthStopsAtZeroAndDiesOnce()
        {
            var head = AddPart("Head", HitZone.Head);
            var deaths = 0;
            _damageable.Died += _ => deaths++;

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Combat\] target died id=Dummy$"));
            head.ApplyHit(60, 0f, DamageFalloff.None);
            head.ApplyHit(60, 0f, DamageFalloff.None);

            Assert.That(_damageable.Health.Current, Is.Zero);
            Assert.That(_damageable.IsDead, Is.True);
            Assert.That(deaths, Is.EqualTo(1));
        }

        [Test]
        public void FindForLooksAtTheColliderAndItsParents()
        {
            var body = AddPart("Body", HitZone.Body);
            var child = new GameObject("Plate", typeof(SphereCollider));
            child.transform.SetParent(body.transform, false);

            Assert.That(Hitbox.FindFor(body.GetComponent<Collider>()), Is.SameAs(body));
            Assert.That(Hitbox.FindFor(child.GetComponent<Collider>()), Is.SameAs(body));
            Assert.Throws<ArgumentNullException>(() => Hitbox.FindFor(null));
        }

        [Test]
        public void TryApplyHitDamagesTheOwnerAndLogs()
        {
            var head = AddPart("Head", HitZone.Head);

            LogAssert.Expect(LogType.Log, "[Combat] hit zone=Head dmg=50");
            var applied = WeaponController.TryApplyHit(head.GetComponent<Collider>(), 0f, 25, DamageFalloff.None);

            Assert.That(applied, Is.True);
            Assert.That(_damageable.Health.Current, Is.EqualTo(50));
        }

        [Test]
        public void TryApplyHitIgnoresCollidersWithoutHitbox()
        {
            var wall = new GameObject("Wall", typeof(BoxCollider));
            try
            {
                Assert.That(WeaponController.TryApplyHit(wall.GetComponent<Collider>(), 1f, 25, DamageFalloff.None), Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(wall);
            }
        }

        [Test]
        public void TryApplyHitLogsAnErrorForHitboxWithoutOwner()
        {
            var orphan = new GameObject("Orphan", typeof(BoxCollider), typeof(Hitbox));
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[Combat\] Hitbox 'Orphan' has no Damageable owner"));

                Assert.That(WeaponController.TryApplyHit(orphan.GetComponent<Collider>(), 1f, 25, DamageFalloff.None), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(orphan);
            }
        }
    }
}

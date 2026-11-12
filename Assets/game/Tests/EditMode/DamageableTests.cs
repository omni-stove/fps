using System;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class DamageableTests
    {
        private GameObject _host;
        private Damageable _damageable;

        [SetUp]
        public void SetUp()
        {
            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "Target";
            _host.transform.position = new Vector3(0f, 1f, 0f);
            _damageable = _host.AddComponent<Damageable>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void CreatesHealthOnFirstUseWithTheDefaultMax()
        {
            Assert.That(_damageable.Health.Max, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(_damageable.Health.Current, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(_damageable.Id, Is.EqualTo("Target"), "falls back to the GameObject name");
        }

        [Test]
        public void InitializeSetsIdAndRefillsHealth()
        {
            _damageable.ApplyDamage(30);

            _damageable.Initialize("dummy", 50);

            Assert.That(_damageable.Id, Is.EqualTo("dummy"));
            Assert.That(_damageable.MaxHealth, Is.EqualTo(50));
            Assert.That(_damageable.Health.Current, Is.EqualTo(50));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InitializeRejectsNonPositiveMax(int max)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _damageable.Initialize("x", max));
        }

        [Test]
        public void ApplyDamageRejectsNegativeAmounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _damageable.ApplyDamage(-1));
        }

        [Test]
        public void DiesOnceAndLogs()
        {
            _damageable.Initialize("dummy", 20);
            var died = 0;
            _damageable.Died += d =>
            {
                Assert.That(d, Is.SameAs(_damageable));
                died++;
            };

            LogAssert.Expect(LogType.Log, "[Combat] died id=dummy");
            _damageable.ApplyDamage(15);
            _damageable.ApplyDamage(15);
            _damageable.ApplyDamage(15);

            Assert.That(died, Is.EqualTo(1));
            Assert.That(_damageable.IsDead, Is.True);
            Assert.That(_damageable.Health.Current, Is.Zero);
        }

        [Test]
        public void ReinitializingDoesNotNotifyTwice()
        {
            _damageable.Initialize("a", 10);
            _damageable.Initialize("a", 10);
            var died = 0;
            _damageable.Died += _ => died++;

            LogAssert.Expect(LogType.Log, "[Combat] died id=a");
            _damageable.ApplyDamage(10);

            Assert.That(died, Is.EqualTo(1));
        }

        [Test]
        public void AimAndGroundPointsComeFromTheColliders()
        {
            // Capsule プリミティブは高さ 2m、中心が Transform の位置。
            Physics.SyncTransforms();
            Assert.That(Vector3.Distance(_damageable.AimPoint, new Vector3(0f, 1f, 0f)), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(_damageable.GroundPoint, Vector3.zero), Is.LessThan(1e-3f));
        }

        [Test]
        public void PointsFallBackToTheTransformWithoutColliders()
        {
            var bare = new GameObject("Bare");
            try
            {
                bare.transform.position = new Vector3(1f, 2f, 3f);
                var damageable = bare.AddComponent<Damageable>();

                Assert.That(damageable.TryGetBounds(out _), Is.False);
                Assert.That(damageable.AimPoint, Is.EqualTo(bare.transform.position));
                Assert.That(damageable.GroundPoint, Is.EqualTo(bare.transform.position));
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }
    }
}

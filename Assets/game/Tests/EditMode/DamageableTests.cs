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
            _damageable.TakeDamage(30);

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
        public void TakeDamageRejectsNegativeAmounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _damageable.TakeDamage(-1));
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

            LogAssert.Expect(LogType.Log, "[Combat] target died id=dummy");
            _damageable.TakeDamage(15);
            _damageable.TakeDamage(15);
            _damageable.TakeDamage(15);

            Assert.That(died, Is.EqualTo(1));
            Assert.That(_damageable.IsDead, Is.True);
            Assert.That(_damageable.Health.Current, Is.Zero);
        }

        [Test]
        public void DamagedReportsTheAmountAndSourceWhileAlive()
        {
            _damageable.Initialize("dummy", 20);
            var reports = new System.Collections.Generic.List<DamageReport>();
            _damageable.Damaged += (d, report) =>
            {
                Assert.That(d, Is.SameAs(_damageable));
                reports.Add(report);
            };
            var source = new Vector3(1f, 0f, -5f);

            _damageable.TakeDamage(5, source);
            _damageable.ApplyHit(10, Fps.Core.HitZone.Body, 0f, Fps.Core.DamageFalloff.None);
            LogAssert.Expect(LogType.Log, "[Combat] target died id=dummy");
            _damageable.TakeDamage(15, source);
            _damageable.TakeDamage(15, source);

            Assert.That(reports, Has.Count.EqualTo(3), "the killing blow is reported, damage after death is not");
            Assert.That(reports[0].Amount, Is.EqualTo(5));
            Assert.That(reports[0].SourcePosition, Is.EqualTo(source));
            Assert.That(reports[1].Amount, Is.EqualTo(10));
            Assert.That(reports[1].SourcePosition.HasValue, Is.False);
            Assert.That(reports[2].Amount, Is.EqualTo(15));
            Assert.That(reports[2].SourcePosition, Is.EqualTo(source));
        }

        [Test]
        public void ReinitializingDoesNotNotifyTwice()
        {
            _damageable.Initialize("a", 10);
            _damageable.Initialize("a", 10);
            var died = 0;
            _damageable.Died += _ => died++;

            LogAssert.Expect(LogType.Log, "[Combat] target died id=a");
            _damageable.TakeDamage(10);

            Assert.That(died, Is.EqualTo(1));
        }

        [Test]
        public void RestoreHealthSetsCurrentAndNotifiesTheReset()
        {
            _damageable.Initialize("dummy", 50);
            _damageable.TakeDamage(45);
            Fps.Core.Health reset = null;
            _damageable.HealthReset += health => reset = health;

            _damageable.RestoreHealth(30);

            Assert.That(_damageable.Health.Current, Is.EqualTo(30));
            Assert.That(_damageable.Health.Max, Is.EqualTo(50));
            Assert.That(reset, Is.SameAs(_damageable.Health));

            _damageable.RestoreHealth(80);
            Assert.That(_damageable.Health.Current, Is.EqualTo(50), "values above the max are clamped");
        }

        [Test]
        public void RestoreHealthBringsTheDeadBackAndKeepsSubscribers()
        {
            _damageable.Initialize("dummy", 20);
            var died = 0;
            _damageable.Died += _ => died++;
            LogAssert.Expect(LogType.Log, "[Combat] target died id=dummy");
            _damageable.Kill();

            _damageable.RestoreHealth(5);
            Assert.That(_damageable.IsDead, Is.False);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=dummy");
            _damageable.TakeDamage(5);
            Assert.That(died, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RestoreHealthRejectsNonPositiveValues(int current)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _damageable.RestoreHealth(current));
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

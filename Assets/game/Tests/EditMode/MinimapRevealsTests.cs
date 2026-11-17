using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MinimapRevealsTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
            MinimapReveals.ResetAll();
            MinimapReveals.Clock = () => _now;
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
            MinimapReveals.ResetAll();
        }

        [Test]
        public void ARevealedEnemyStaysForTheGivenSecondsThenLeaves()
        {
            var enemy = Body("Enemy");

            MinimapReveals.Reveal(enemy, 3f);
            _now = 1f;
            Assert.That(MinimapReveals.RemainingSeconds(enemy), Is.EqualTo(2f));

            _now = 2.99f;
            Assert.That(MinimapReveals.IsRevealed(enemy), Is.True);
            Assert.That(Revealed(), Is.EqualTo(new[] { enemy }));

            _now = 3f;
            Assert.That(MinimapReveals.IsRevealed(enemy), Is.False);
            Assert.That(MinimapReveals.RemainingSeconds(enemy), Is.Zero);
            Assert.That(Revealed(), Is.Empty);
            Assert.That(MinimapReveals.Count, Is.Zero);
        }

        [Test]
        public void RevealingAgainKeepsTheLongerTime()
        {
            var enemy = Body("Enemy");

            MinimapReveals.Reveal(enemy, 10f);
            _now = 1f;
            MinimapReveals.Reveal(enemy, 3f);
            Assert.That(MinimapReveals.RemainingSeconds(enemy), Is.EqualTo(9f), "a shorter reveal does not cut the longer one");

            MinimapReveals.Reveal(enemy, 20f);
            Assert.That(MinimapReveals.RemainingSeconds(enemy), Is.EqualTo(20f));
            Assert.That(MinimapReveals.Count, Is.EqualTo(1));
        }

        [Test]
        public void KeepsTheOrderTheEnemiesWereFound()
        {
            var first = Body("First");
            var second = Body("Second");
            var third = Body("Third");

            MinimapReveals.Reveal(second, 5f);
            MinimapReveals.Reveal(first, 5f);
            MinimapReveals.Reveal(third, 5f);

            Assert.That(Revealed(), Is.EqualTo(new[] { second, first, third }));
        }

        [Test]
        public void DeadAndDestroyedEnemiesLeave()
        {
            var dead = Body("Dead");
            var destroyed = Body("Destroyed");
            var alive = Body("Alive");
            MinimapReveals.Reveal(dead, 5f);
            MinimapReveals.Reveal(destroyed, 5f);
            MinimapReveals.Reveal(alive, 5f);

            dead.Kill();
            Object.DestroyImmediate(destroyed.gameObject);

            Assert.That(MinimapReveals.IsRevealed(dead), Is.False);
            Assert.That(MinimapReveals.Prune(), Is.EqualTo(2));
            Assert.That(Revealed(), Is.EqualTo(new[] { alive }));
        }

        [Test]
        public void ResetEmptiesTheRevealsAndTheClock()
        {
            MinimapReveals.Reveal(Body("Enemy"), 5f);

            MinimapReveals.ResetAll();

            Assert.That(MinimapReveals.Count, Is.Zero);
            Assert.That(MinimapReveals.Clock, Is.Null);
        }

        [Test]
        public void ClearEmptiesTheReveals()
        {
            MinimapReveals.Reveal(Body("Enemy"), 5f);

            MinimapReveals.Clear();

            Assert.That(MinimapReveals.Count, Is.Zero);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var enemy = Body("Enemy");

            Assert.Throws<ArgumentNullException>(() => MinimapReveals.Reveal(null, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => MinimapReveals.Reveal(enemy, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => MinimapReveals.Reveal(enemy, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => MinimapReveals.Reveal(enemy, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => MinimapReveals.Reveal(enemy, float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => MinimapReveals.CopyRevealedTo(null));
            Assert.That(MinimapReveals.Count, Is.Zero);
            Assert.That(MinimapReveals.RemainingSeconds(null), Is.Zero);
        }

        private static List<Damageable> Revealed()
        {
            var into = new List<Damageable>();
            MinimapReveals.CopyRevealedTo(into);
            return into;
        }

        private Damageable Body(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            var damageable = go.AddComponent<Damageable>();
            damageable.Initialize(name, Damageable.DefaultMaxHealth);
            return damageable;
        }
    }
}

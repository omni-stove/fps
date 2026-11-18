using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.NetV2.Tests
{
    // 押した瞬間にサーバーの答えを待たずに振る見た目を出し、サーバーと同じ間隔でしか振り直さないことを確かめる
    public sealed class ClientMeleeTests
    {
        private const float _frame = 0.05f;

        // 振り終え 0.5 秒、そこから 0.4 秒待つ
        private static readonly MeleeSpec _spec = new MeleeSpec(
            damage: 30, reach: 2f, arcDegrees: 90f, swingSeconds: 0.5f, cooldownSeconds: 0.4f, impactSeconds: 0.2f, lungeDistance: 0f);

        private ClientMelee _melee;
        private List<MeleeSpec> _started;
        private int _cancelled;

        [SetUp]
        public void SetUp()
        {
            _melee = new ClientMelee(_spec);
            _started = new List<MeleeSpec>();
            _cancelled = 0;
            _melee.SwingStarted += _started.Add;
            _melee.SwingCancelled += () => _cancelled++;
        }

        [Test]
        public void PressingShowsASwingAtOnce()
        {
            Assert.That(_melee.Update(true, true, _frame), Is.True);

            Assert.That(_started, Has.Count.EqualTo(1));
            Assert.That(_started[0], Is.SameAs(_spec));
            Assert.That(_melee.IsSwinging, Is.True);
        }

        [Test]
        public void HoldingTheButtonSwingsOnlyOnce()
        {
            _melee.Update(true, true, _frame);
            Frames(true, true, 1.5f);

            Assert.That(_started, Has.Count.EqualTo(1));
        }

        [Test]
        public void APressBeforeTheCooldownEndsShowsNoSwing()
        {
            _melee.Update(true, true, _frame);
            _melee.Update(false, true, _frame);

            // 0.6 秒: 振り終えたが 0.9 秒まで待つ
            Frames(false, true, 0.5f);
            Assert.That(_melee.IsSwinging, Is.False);
            Assert.That(_melee.CooldownRemaining, Is.GreaterThan(0f));

            Assert.That(_melee.Update(true, true, _frame), Is.False);
            Assert.That(_started, Has.Count.EqualTo(1));
        }

        [Test]
        public void APressAfterTheCooldownSwingsAgain()
        {
            _melee.Update(true, true, _frame);
            _melee.Update(false, true, _frame);
            Frames(false, true, 1f);
            Assert.That(_melee.CooldownRemaining, Is.Zero);

            Assert.That(_melee.Update(true, true, _frame), Is.True);
            Assert.That(_started, Has.Count.EqualTo(2));
        }

        [Test]
        public void APressWhileUnableToSwingShowsNothing()
        {
            Assert.That(_melee.Update(true, false, _frame), Is.False);

            // 振れる状態に戻っても、押し続けているだけでは振らない
            Assert.That(_melee.Update(true, true, _frame), Is.False);
            Assert.That(_started, Is.Empty);
        }

        [Test]
        public void BeingUnableToSwingMidSwingCancelsTheSwing()
        {
            _melee.Update(true, true, _frame);

            _melee.Update(false, false, _frame);

            Assert.That(_melee.IsSwinging, Is.False);
            Assert.That(_cancelled, Is.EqualTo(1));
            Assert.That(_melee.CooldownRemaining, Is.GreaterThan(0f), "cancelling still waits, as the server does");
        }

        [Test]
        public void ResetDropsTheSwingAndTheCooldown()
        {
            _melee.Update(true, true, _frame);
            _melee.Update(false, true, _frame);

            _melee.Reset();

            Assert.That(_melee.IsSwinging, Is.False);
            Assert.That(_melee.CooldownRemaining, Is.Zero);
            Assert.That(_cancelled, Is.EqualTo(1));
            Assert.That(_melee.Update(true, true, _frame), Is.True);
        }

        [Test]
        public void ANewSpecIsUsedFromTheNextSwing()
        {
            var slow = new MeleeSpec(
                damage: 30, reach: 2f, arcDegrees: 90f, swingSeconds: 1f, cooldownSeconds: 0.4f, impactSeconds: 0.5f, lungeDistance: 0f);
            _melee.Update(true, true, _frame);

            _melee.Spec = slow;
            _melee.Update(false, true, _frame);
            Frames(false, true, 1f);
            _melee.Update(true, true, _frame);

            Assert.That(_started, Is.EqualTo(new[] { _spec, slow }));
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new ClientMelee(null));
            Assert.Throws<ArgumentNullException>(() => _melee.Spec = null);
            Assert.Throws<ArgumentOutOfRangeException>(() => _melee.Update(true, true, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _melee.Update(true, true, float.NaN));
            Assert.That(_started, Is.Empty);
        }

        private void Frames(bool held, bool canSwing, float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _frame)
            {
                _melee.Update(held, canSwing, _frame);
            }
        }
    }
}

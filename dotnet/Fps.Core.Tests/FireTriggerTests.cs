using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class FireTriggerTests
    {
        [TestCase(FireMode.Auto, false, false, false)]
        [TestCase(FireMode.Auto, true, true, true)]
        [TestCase(FireMode.Auto, true, false, true)]
        [TestCase(FireMode.Semi, false, false, false)]
        [TestCase(FireMode.Semi, true, true, true)]
        [TestCase(FireMode.Semi, true, false, false)]
        [TestCase(FireMode.Semi, false, true, true)]
        public void WantsFollowsTheFireMode(FireMode mode, bool isPressed, bool wasPressedThisFrame, bool expected)
        {
            Assert.That(FireTrigger.Wants(mode, isPressed, wasPressedThisFrame), Is.EqualTo(expected));
        }

        [Test]
        public void SemiDoesNotFireAgainWhileHeld()
        {
            var trigger = new FireTrigger(FireMode.Semi, 0.2f);

            Assert.That(trigger.Pull(true, true), Is.True);
            for (var i = 0; i < 10; i++)
            {
                trigger.Tick(0.2f);
                Assert.That(trigger.Pull(true, false), Is.False);
            }
        }

        [Test]
        public void SemiFiresAgainAfterReleasingAndPressing()
        {
            var trigger = new FireTrigger(FireMode.Semi, 0.2f);

            Assert.That(trigger.Pull(true, true), Is.True);
            trigger.Tick(0.2f);
            Assert.That(trigger.Pull(false, false), Is.False);
            Assert.That(trigger.Pull(true, true), Is.True);
        }

        [Test]
        public void SemiPressedWithinTheFireIntervalIsDropped()
        {
            var trigger = new FireTrigger(FireMode.Semi, 0.2f);

            Assert.That(trigger.Pull(true, true), Is.True);
            trigger.Tick(0.1f);
            Assert.That(trigger.Pull(true, true), Is.False);

            // 押した分を溜めて後から撃たない
            trigger.Tick(0.1f);
            Assert.That(trigger.Pull(true, false), Is.False);
        }

        [Test]
        public void AutoFiresEveryFireIntervalWhileHeld()
        {
            // 2 の累乗の分数にして、引き算の丸めで撃つフレームがずれないようにする
            const float interval = 0.125f;
            const float frame = 0.03125f;
            var trigger = new FireTrigger(FireMode.Auto, interval);

            var shots = 0;
            for (var i = 0; i < 40; i++)
            {
                if (i > 0)
                {
                    trigger.Tick(frame);
                }

                if (trigger.Pull(true, i == 0))
                {
                    shots++;
                }
            }

            // 1/32 秒ごとの 40 フレーム (1.21875 秒) で、0 秒・0.125 秒・…・1.125 秒の 10 発
            Assert.That(shots, Is.EqualTo(10));
        }

        [Test]
        public void AutoDoesNotFireBeforeTheFireIntervalAndStopsWhenReleased()
        {
            var trigger = new FireTrigger(FireMode.Auto, 0.1f);

            Assert.That(trigger.Pull(true, true), Is.True);
            trigger.Tick(0.05f);
            Assert.That(trigger.Pull(true, false), Is.False);
            trigger.Tick(0.05f);
            Assert.That(trigger.Pull(true, false), Is.True);
            trigger.Tick(0.1f);
            Assert.That(trigger.Pull(false, false), Is.False);
        }

        [Test]
        public void ZeroIntervalAutoFiresEveryPull()
        {
            var trigger = new FireTrigger(FireMode.Auto, 0f);

            Assert.That(trigger.Pull(true, true), Is.True);
            Assert.That(trigger.Pull(true, false), Is.True);
        }

        [Test]
        public void TakesTheModeAndIntervalFromTheSpec()
        {
            var spec = new WeaponSpec(
                "00000000-0000-4000-8000-000000000002", null, WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None);

            var trigger = new FireTrigger(spec);

            Assert.That(trigger.Mode, Is.EqualTo(FireMode.Semi));
            Assert.That(trigger.FireInterval, Is.EqualTo(0.2f));
        }

        [Test]
        public void RejectsInvalidInput()
        {
            Assert.Throws<ArgumentNullException>(() => new FireTrigger(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FireTrigger((FireMode)99, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FireTrigger(FireMode.Auto, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FireTrigger(FireMode.Auto, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FireTrigger(FireMode.Auto, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => FireTrigger.Wants((FireMode)99, true, true));

            var trigger = new FireTrigger(FireMode.Auto, 0.1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => trigger.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => trigger.Tick(float.NaN));
        }
    }
}

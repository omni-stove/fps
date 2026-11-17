using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SpinUpTriggerTests
    {
        // 2 の累乗の刻みにして、足し合わせた秒数が待ちの秒数とちょうど揃うようにする。
        private const float _step = 1f / 64f;
        private const float _fireInterval = 0.125f;
        private static readonly float[] _fullRateShotTimes = { 0.5f, 0.625f, 0.75f, 0.875f, 1f };

        private static SpinUpTrigger Minigun(float spinUpSeconds = 0.5f, float startIntervalMultiplier = 2f, float spinDownSeconds = 0.25f)
        {
            return new SpinUpTrigger(
                new SpinUpSpec(spinUpSeconds, startIntervalMultiplier, spinDownSeconds), new AutoTrigger(_fireInterval));
        }

        private static List<float> ShotTimesWhileHeld(SpinUpTrigger trigger, float seconds)
        {
            var times = new List<float>();
            var shots = trigger.Press();
            for (var i = 0; i < shots; i++)
            {
                times.Add(0f);
            }

            var steps = (int)Math.Round(seconds / _step);
            for (var step = 1; step <= steps; step++)
            {
                shots = trigger.Tick(_step);
                for (var i = 0; i < shots; i++)
                {
                    times.Add(step * _step);
                }
            }

            return times;
        }

        private static void SpinFully(SpinUpTrigger trigger)
        {
            ShotTimesWhileHeld(trigger, 2f);
            Assert.That(trigger.SpinFraction, Is.EqualTo(1f));
        }

        [Test]
        public void DoesNotFireWhileSpinningUp()
        {
            var trigger = Minigun();

            Assert.That(trigger.Press(), Is.EqualTo(0));
            for (var i = 0; i < 31; i++)
            {
                Assert.That(trigger.Tick(_step), Is.EqualTo(0), $"tick {i}");
            }

            Assert.That(trigger.Tick(_step), Is.EqualTo(1), "fires once the spin-up seconds have passed");
        }

        [Test]
        public void StartsAtTheMultipliedIntervalAndShrinksToTheFireInterval()
        {
            var times = ShotTimesWhileHeld(Minigun(), 2f);

            Assert.That(times[0], Is.EqualTo(0.5f));
            var intervals = new List<float>();
            for (var i = 1; i < times.Count; i++)
            {
                intervals.Add(times[i] - times[i - 1]);
            }

            Assert.That(intervals[0], Is.GreaterThan(_fireInterval * 1.5f));
            Assert.That(intervals[0], Is.LessThanOrEqualTo(_fireInterval * 2f + _step));
            for (var i = 1; i < intervals.Count; i++)
            {
                Assert.That(intervals[i], Is.LessThanOrEqualTo(intervals[i - 1]), $"interval {i}");
            }

            Assert.That(intervals[intervals.Count - 1], Is.EqualTo(_fireInterval));
        }

        [Test]
        public void NeverFiresFasterThanTheFireInterval()
        {
            var trigger = Minigun();
            var times = ShotTimesWhileHeld(trigger, 3f);

            Assert.That(trigger.MinShotSpacingSeconds, Is.EqualTo(_fireInterval));
            for (var i = 1; i < times.Count; i++)
            {
                Assert.That(times[i] - times[i - 1], Is.GreaterThanOrEqualTo(_fireInterval), $"shot {i}");
            }
        }

        [Test]
        public void AMultiplierOfOneFiresAtTheFireIntervalAsSoonAsItSpinsUp()
        {
            var times = ShotTimesWhileHeld(Minigun(startIntervalMultiplier: 1f), 1f);

            Assert.That(times, Is.EqualTo(_fullRateShotTimes));
        }

        [Test]
        public void ReleasingSpinsDownAtTheConfiguredSpeed()
        {
            var trigger = Minigun();
            SpinFully(trigger);

            Assert.That(trigger.Release(), Is.EqualTo(0));
            Assert.That(trigger.Tick(0.125f), Is.EqualTo(0));
            Assert.That(trigger.SpinFraction, Is.EqualTo(0.5f));
            Assert.That(trigger.Tick(0.125f), Is.EqualTo(0));
            Assert.That(trigger.SpinFraction, Is.EqualTo(0f));
        }

        [Test]
        public void PressingAgainBeforeItStopsFiresWithoutTheFullWait()
        {
            var trigger = Minigun();
            SpinFully(trigger);
            trigger.Release();
            trigger.Tick(0.0625f);

            var times = ShotTimesWhileHeld(trigger, 0.5f);

            Assert.That(times, Is.Not.Empty);
            Assert.That(times[0], Is.LessThan(0.5f));
        }

        [Test]
        public void AfterItStopsPressingWaitsTheSpinUpSecondsAgain()
        {
            var trigger = Minigun();
            SpinFully(trigger);
            trigger.Release();
            trigger.Tick(1f);

            var times = ShotTimesWhileHeld(trigger, 1f);

            Assert.That(times[0], Is.EqualTo(0.5f));
        }

        [Test]
        public void StowingStopsTheSpinAtOnce()
        {
            var trigger = Minigun();
            SpinFully(trigger);

            trigger.Stow();

            Assert.That(trigger.SpinFraction, Is.EqualTo(0f));
            var times = ShotTimesWhileHeld(trigger, 1f);
            Assert.That(times[0], Is.EqualTo(0.5f), "waits the spin-up seconds again");
        }

        [Test]
        public void TheSpinShowsThroughTheOuterParts()
        {
            var model = TriggerModels.For(new TriggerSpec(
                FireMode.Auto, _fireInterval, spinUp: new SpinUpSpec(0.5f, 2f, 0.25f), cycle: new CycleSpec(0.5f)));

            model.Press();
            model.Tick(0.5f);

            Assert.That(model.SpinFraction, Is.EqualTo(0.5f));
        }

        [Test]
        public void ZeroSpinDownSecondsStopsAtOnce()
        {
            var trigger = Minigun(spinDownSeconds: 0f);
            SpinFully(trigger);
            trigger.Release();

            Assert.That(trigger.Tick(0f), Is.EqualTo(0));
            Assert.That(trigger.SpinFraction, Is.EqualTo(0f));
        }

        [Test]
        public void TheWeaponSpecBuildsASpinUpTriggerOverItsFireInterval()
        {
            var model = TriggerModels.For(new TriggerSpec(FireMode.Auto, _fireInterval, spinUp: new SpinUpSpec(0.5f, 2f, 0.25f)));

            Assert.That(model, Is.TypeOf<SpinUpTrigger>());
            Assert.That(model.MinShotSpacingSeconds, Is.EqualTo(_fireInterval));
            Assert.That(model.Press(), Is.EqualTo(0));
        }

        [Test]
        public void RejectsANegativeTick()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Minigun().Tick(-1f));
        }
    }
}

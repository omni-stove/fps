using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ZoneScheduleTests
    {
        private const float _tolerance = 1e-4f;

        // 半径 100 から、10 秒待って 20 秒で 50 へ、5 秒待って 10 秒で 20 へ。合わせて 45 秒。
        private static ZoneSchedule TwoPhases(int seed = 7)
        {
            return new ZoneSchedule(
                new Vector2(10f, -5f),
                100f,
                new[]
                {
                    new ZonePhase(10f, 20f, 50f, 1f),
                    new ZonePhase(5f, 10f, 20f, 4f),
                },
                seed);
        }

        [Test]
        public void PhaseTimesAddUp()
        {
            var schedule = TwoPhases();

            Assert.That(schedule.PhaseCount, Is.EqualTo(2));
            Assert.That(schedule.TotalSeconds, Is.EqualTo(45f).Within(_tolerance));
            Assert.That(schedule.PhaseStartSeconds(0), Is.EqualTo(0f));
            Assert.That(schedule.PhaseStartSeconds(1), Is.EqualTo(30f).Within(_tolerance));
            Assert.That(schedule.TargetRadius(0), Is.EqualTo(50f));
            Assert.That(schedule.TargetRadius(1), Is.EqualTo(20f));
        }

        [Test]
        public void WaitsWithTheInitialCircleFirst()
        {
            var schedule = TwoPhases();
            var state = schedule.Evaluate(4f);

            Assert.That(state.PhaseIndex, Is.EqualTo(0));
            Assert.That(state.Stage, Is.EqualTo(ZoneStage.Waiting));
            Assert.That(state.Center, Is.EqualTo(new Vector2(10f, -5f)));
            Assert.That(state.Radius, Is.EqualTo(100f));
            Assert.That(state.TargetRadius, Is.EqualTo(50f));
            Assert.That(state.TargetCenter, Is.EqualTo(schedule.TargetCenter(0)));
            Assert.That(state.DamagePerSecond, Is.EqualTo(1f));
            Assert.That(state.SecondsUntilShrink, Is.EqualTo(6f).Within(_tolerance));
            Assert.That(state.SecondsUntilStageEnds, Is.EqualTo(6f).Within(_tolerance));
        }

        [Test]
        public void ShrinksLinearlyTowardTheNextCircle()
        {
            var schedule = TwoPhases();
            var target = schedule.TargetCenter(0);
            var state = schedule.Evaluate(20f);

            Assert.That(state.Stage, Is.EqualTo(ZoneStage.Shrinking));
            Assert.That(state.PhaseIndex, Is.EqualTo(0));
            Assert.That(state.Radius, Is.EqualTo(75f).Within(_tolerance));
            var halfway = Vector2.Lerp(new Vector2(10f, -5f), target, 0.5f);
            Assert.That(state.Center.X, Is.EqualTo(halfway.X).Within(_tolerance));
            Assert.That(state.Center.Y, Is.EqualTo(halfway.Y).Within(_tolerance));
            Assert.That(state.SecondsUntilShrink, Is.EqualTo(0f));
            Assert.That(state.SecondsUntilStageEnds, Is.EqualTo(10f).Within(_tolerance));
            Assert.That(schedule.RadiusAt(20f), Is.EqualTo(state.Radius));
            Assert.That(schedule.CenterAt(20f), Is.EqualTo(state.Center));
        }

        [Test]
        public void ThePhaseBoundaryBelongsToTheNextPhase()
        {
            var schedule = TwoPhases();
            var state = schedule.Evaluate(30f);

            Assert.That(state.PhaseIndex, Is.EqualTo(1));
            Assert.That(state.Stage, Is.EqualTo(ZoneStage.Waiting));
            Assert.That(state.Radius, Is.EqualTo(50f));
            Assert.That(state.Center, Is.EqualTo(schedule.TargetCenter(0)));
            Assert.That(state.DamagePerSecond, Is.EqualTo(4f));
            Assert.That(state.SecondsUntilShrink, Is.EqualTo(5f).Within(_tolerance));
        }

        [Test]
        public void StaysOnTheLastCircleAfterAllPhases()
        {
            var schedule = TwoPhases();

            foreach (var time in new[] { 45f, 1000f })
            {
                var state = schedule.Evaluate(time);
                Assert.That(state.Stage, Is.EqualTo(ZoneStage.Closed));
                Assert.That(state.PhaseIndex, Is.EqualTo(1));
                Assert.That(state.Radius, Is.EqualTo(20f));
                Assert.That(state.Center, Is.EqualTo(schedule.TargetCenter(1)));
                Assert.That(state.TargetCenter, Is.EqualTo(state.Center));
                Assert.That(state.TargetRadius, Is.EqualTo(state.Radius));
                Assert.That(state.DamagePerSecond, Is.EqualTo(4f));
                Assert.That(state.SecondsUntilShrink, Is.EqualTo(0f));
                Assert.That(state.SecondsUntilStageEnds, Is.EqualTo(0f));
            }
        }

        [Test]
        public void EveryNextCircleFitsInsideThePreviousOne()
        {
            for (var seed = -50; seed < 50; seed++)
            {
                var schedule = TwoPhases(seed);
                var previousCenter = schedule.InitialCenter;
                var previousRadius = schedule.InitialRadius;
                for (var i = 0; i < schedule.PhaseCount; i++)
                {
                    var center = schedule.TargetCenter(i);
                    var radius = schedule.TargetRadius(i);
                    Assert.That(
                        Vector2.Distance(center, previousCenter) + radius,
                        Is.LessThanOrEqualTo(previousRadius + _tolerance),
                        "seed " + seed + " phase " + i);
                    previousCenter = center;
                    previousRadius = radius;
                }
            }
        }

        [Test]
        public void TheSameSeedPicksTheSameCenters()
        {
            var a = TwoPhases(123);
            var b = TwoPhases(123);
            var c = TwoPhases(124);

            Assert.That(a.TargetCenter(0), Is.EqualTo(b.TargetCenter(0)));
            Assert.That(a.TargetCenter(1), Is.EqualTo(b.TargetCenter(1)));
            Assert.That(a.TargetCenter(0), Is.Not.EqualTo(c.TargetCenter(0)));
        }

        [Test]
        public void CentersAreSpreadAcrossTheCircle()
        {
            // 中心からの距離を一様に取ると中心付近に偏る。円の中で一様なら、外側半分 (面積 3/4) に 4 分の 3 ほどが入る。
            var outer = 0;
            const int count = 2000;
            for (var seed = 0; seed < count; seed++)
            {
                var schedule = new ZoneSchedule(Vector2.Zero, 10f, new[] { new ZonePhase(0f, 1f, 0.0001f, 0f) }, seed);
                if (schedule.TargetCenter(0).Length() > 5f)
                {
                    outer++;
                }
            }

            Assert.That(outer, Is.InRange(1400, 1600));
        }

        [Test]
        public void KeepsTheCenterWhenTheRadiusDoesNotShrink()
        {
            var schedule = new ZoneSchedule(new Vector2(3f, 4f), 30f, new[] { new ZonePhase(1f, 1f, 30f, 2f) }, 5);

            Assert.That(schedule.TargetCenter(0), Is.EqualTo(new Vector2(3f, 4f)));
        }

        [Test]
        public void ZeroLengthShrinkJumpsAtTheEndOfTheWait()
        {
            var schedule = new ZoneSchedule(
                Vector2.Zero,
                40f,
                new[] { new ZonePhase(0.1f, 0f, 30f, 1f), new ZonePhase(0.2f, 0f, 10f, 2f) },
                3);

            Assert.That(schedule.Evaluate(0.05f).Radius, Is.EqualTo(40f));
            Assert.That(schedule.Evaluate(0.1f).Radius, Is.EqualTo(30f));
            Assert.That(schedule.Evaluate(0.1f).Stage, Is.EqualTo(ZoneStage.Waiting));
            for (var t = 0f; t < schedule.TotalSeconds; t += 0.001f)
            {
                var state = schedule.Evaluate(t);
                Assert.That(float.IsNaN(state.Radius), Is.False, "t=" + t);
                Assert.That(float.IsNaN(state.Center.X), Is.False, "t=" + t);
            }

            Assert.That(schedule.Evaluate(0.3f).Stage, Is.EqualTo(ZoneStage.Closed));
            Assert.That(schedule.Evaluate(0.3f).Radius, Is.EqualTo(10f));
        }

        [Test]
        public void ContainsIncludesTheEdgeAndIgnoresNaN()
        {
            var state = new ZoneSchedule(Vector2.Zero, 10f, new[] { new ZonePhase(5f, 5f, 5f, 1f) }, 0).Evaluate(0f);

            Assert.That(state.Contains(new Vector2(10f, 0f)), Is.True);
            Assert.That(state.Contains(new Vector2(0f, -9.9f)), Is.True);
            Assert.That(state.Contains(new Vector2(7.1f, 7.1f)), Is.False);
            Assert.That(state.Contains(new Vector2(float.NaN, 0f)), Is.False);
            Assert.That(state.Contains(new Vector2(float.PositiveInfinity, 0f)), Is.False);
        }

        [Test]
        public void RejectsInvalidSchedules()
        {
            var phase = new ZonePhase(1f, 1f, 5f, 1f);

            Assert.Throws<ArgumentNullException>(() => new ZoneSchedule(Vector2.Zero, 10f, null, 0));
            Assert.Throws<ArgumentException>(() => new ZoneSchedule(Vector2.Zero, 10f, Array.Empty<ZonePhase>(), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneSchedule(Vector2.Zero, 0f, new[] { phase }, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneSchedule(Vector2.Zero, float.PositiveInfinity, new[] { phase }, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneSchedule(new Vector2(float.NaN, 0f), 10f, new[] { phase }, 0));
            Assert.Throws<ArgumentException>(() => new ZoneSchedule(Vector2.Zero, 4f, new[] { phase }, 0));
            Assert.Throws<ArgumentException>(
                () => new ZoneSchedule(Vector2.Zero, 10f, new[] { phase, new ZonePhase(1f, 1f, 6f, 1f) }, 0));

            var schedule = new ZoneSchedule(Vector2.Zero, 10f, new[] { phase }, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.Evaluate(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.Evaluate(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.Evaluate(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.TargetCenter(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.PhaseStartSeconds(-1));
        }

        [Test]
        public void RejectsInvalidPhases()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZonePhase(-1f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZonePhase(float.NaN, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZonePhase(1f, -1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZonePhase(1f, 1f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZonePhase(1f, 1f, 1f, -0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZonePhase(1f, 1f, 1f, float.PositiveInfinity));
            Assert.That(new ZonePhase(2f, 3f, 1f, 0f).DurationSeconds, Is.EqualTo(5f));
        }
    }
}

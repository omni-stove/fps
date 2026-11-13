using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class StuckMonitorTests
    {
        private static readonly Vector3 _goal = new Vector3(10f, 0f, 0f);

        // 2 秒進めなければ次の段階、0.5m で進めたとみなし、目的地が 2m 変われば測り直す。
        private static StuckMonitor Monitor() => new StuckMonitor(2f, 0.5f, 2f);

        /// <summary>原点で <paramref name="stages"/> 段階目まで詰まらせた監視。</summary>
        private static StuckMonitor StuckAtOrigin(int stages)
        {
            var monitor = Monitor();
            monitor.Tick(0f, Vector3.Zero, _goal);
            for (var i = 0; i < stages; i++)
            {
                monitor.Tick(2f, Vector3.Zero, _goal);
            }

            return monitor;
        }

        [Test]
        public void MovingForwardNeverEscalates()
        {
            var monitor = Monitor();

            for (var i = 0; i < 20; i++)
            {
                Assert.That(monitor.Tick(1f, new Vector3(i * 0.5f, 0f, 0f), _goal), Is.EqualTo(StuckRecovery.None));
            }

            Assert.That(monitor.Stage, Is.EqualTo(StuckRecovery.None));
        }

        [Test]
        public void ChasingAMovingGoalIsNotStuckWhileMoving()
        {
            var monitor = Monitor();

            for (var i = 0; i < 20; i++)
            {
                var goal = new Vector3(10f + (i * 3f), 0f, 0f);
                Assert.That(monitor.Tick(1f, new Vector3(i, 0f, 0f), goal), Is.EqualTo(StuckRecovery.None));
            }
        }

        [Test]
        public void StandingStillWithoutAGoalIsNotStuck()
        {
            var monitor = Monitor();

            for (var i = 0; i < 10; i++)
            {
                Assert.That(monitor.Tick(1f, Vector3.Zero, null), Is.EqualTo(StuckRecovery.None));
            }
        }

        [Test]
        public void EscalatesOneStageEachTimeTheWindowPasses()
        {
            var monitor = Monitor();
            monitor.Tick(0f, Vector3.Zero, _goal);

            Assert.That(monitor.Tick(1.5f, Vector3.Zero, _goal), Is.EqualTo(StuckRecovery.None));
            Assert.That(monitor.Tick(0.5f, Vector3.Zero, _goal), Is.EqualTo(StuckRecovery.Repath));
            Assert.That(monitor.Tick(1f, Vector3.Zero, _goal), Is.EqualTo(StuckRecovery.None), "段階を返すのは区切りの tick だけ");
            Assert.That(monitor.Stage, Is.EqualTo(StuckRecovery.Repath));
            Assert.That(monitor.Tick(1f, Vector3.Zero, _goal), Is.EqualTo(StuckRecovery.Sidestep));
            Assert.That(monitor.Tick(2f, Vector3.Zero, _goal), Is.EqualTo(StuckRecovery.AbandonGoal));
            Assert.That(monitor.Tick(2f, Vector3.Zero, _goal), Is.EqualTo(StuckRecovery.Warp));
            Assert.That(monitor.TotalStalledSeconds, Is.EqualTo(8f));
        }

        [Test]
        public void StartsOverAfterWarp()
        {
            var monitor = StuckAtOrigin(4);

            Assert.That(monitor.Stage, Is.EqualTo(StuckRecovery.Warp));
            Assert.That(monitor.Tick(2f, Vector3.Zero, _goal), Is.EqualTo(StuckRecovery.Repath));
        }

        [Test]
        public void SmallJitterDoesNotCountAsProgress()
        {
            var monitor = Monitor();
            monitor.Tick(0f, Vector3.Zero, _goal);

            monitor.Tick(1f, new Vector3(0.3f, 0f, 0f), _goal);

            Assert.That(monitor.Tick(1f, new Vector3(-0.3f, 0f, 0f), _goal), Is.EqualTo(StuckRecovery.Repath));
        }

        [Test]
        public void MovingSidewaysDuringRecoveryDoesNotResetTheStage()
        {
            var monitor = StuckAtOrigin(2);
            Assert.That(monitor.Stage, Is.EqualTo(StuckRecovery.Sidestep));

            Assert.That(monitor.Tick(1f, new Vector3(0f, 0f, 2f), _goal), Is.EqualTo(StuckRecovery.None));
            Assert.That(monitor.Stage, Is.EqualTo(StuckRecovery.Sidestep), "寄り道で横へ動いても目的地には近づいていない");
            Assert.That(monitor.Tick(1f, Vector3.Zero, _goal), Is.EqualTo(StuckRecovery.AbandonGoal), "元の場所へ戻ってきたら次の段階");
        }

        [Test]
        public void GettingCloserToTheGoalDuringRecoveryResetsTheStage()
        {
            var monitor = StuckAtOrigin(2);
            var recovered = new List<float>();
            monitor.Recovered += recovered.Add;

            Assert.That(monitor.Tick(0.5f, new Vector3(0.4f, 0f, 0f), _goal), Is.EqualTo(StuckRecovery.None));
            Assert.That(monitor.Stage, Is.EqualTo(StuckRecovery.Sidestep), "0.5m 縮むまでは立ち直っていない");
            Assert.That(recovered, Is.Empty);

            Assert.That(monitor.Tick(0.5f, new Vector3(0.6f, 0f, 0f), _goal), Is.EqualTo(StuckRecovery.None));
            Assert.That(monitor.Stage, Is.EqualTo(StuckRecovery.None));
            Assert.That(monitor.TotalStalledSeconds, Is.EqualTo(0f));
            Assert.That(recovered, Has.Count.EqualTo(1));
            Assert.That(recovered[0], Is.EqualTo(4.5f));

            Assert.That(monitor.Tick(2f, new Vector3(0.6f, 0f, 0f), _goal), Is.EqualTo(StuckRecovery.Repath), "また詰まれば最初の段階から");
        }

        [Test]
        public void ANewGoalDuringRecoveryKeepsTheStageAndMeasuresFromTheNewGoal()
        {
            var monitor = StuckAtOrigin(3);
            var recovered = new List<float>();
            monitor.Recovered += recovered.Add;

            var next = new Vector3(0f, 0f, -10f);
            Assert.That(monitor.Tick(0.1f, Vector3.Zero, next), Is.EqualTo(StuckRecovery.None));

            Assert.That(monitor.Stage, Is.EqualTo(StuckRecovery.AbandonGoal), "目的地を替えても挟まったままなら Warp へ進めるよう段階を保つ");
            Assert.That(recovered, Is.Empty);
            Assert.That(monitor.Tick(1.5f, Vector3.Zero, next), Is.EqualTo(StuckRecovery.None), "新しい目的地では時間を測り直す");
            Assert.That(monitor.Tick(0.5f, Vector3.Zero, next), Is.EqualTo(StuckRecovery.Warp));
        }

        [Test]
        public void GettingCloserToANewGoalDuringRecoveryResetsTheStage()
        {
            var monitor = StuckAtOrigin(3);
            var recovered = new List<float>();
            monitor.Recovered += recovered.Add;
            var next = new Vector3(0f, 0f, -10f);
            monitor.Tick(0.1f, Vector3.Zero, next);

            monitor.Tick(0.5f, new Vector3(0f, 0f, -0.6f), next);

            Assert.That(monitor.Stage, Is.EqualTo(StuckRecovery.None));
            Assert.That(recovered, Has.Count.EqualTo(1));
        }

        [Test]
        public void ASlightlyMovedGoalDuringRecoveryKeepsTheStage()
        {
            var monitor = StuckAtOrigin(1);

            Assert.That(monitor.Tick(2f, Vector3.Zero, _goal + new Vector3(0f, 0f, 1f)), Is.EqualTo(StuckRecovery.Sidestep));
        }

        [Test]
        public void LosingTheGoalResetsTheStage()
        {
            var monitor = StuckAtOrigin(1);

            monitor.Tick(0.1f, Vector3.Zero, null);

            Assert.That(monitor.Stage, Is.EqualTo(StuckRecovery.None));
            Assert.That(monitor.Tick(1.9f, Vector3.Zero, _goal), Is.EqualTo(StuckRecovery.None), "目的地を持ち直したら位置を測り直す");
            Assert.That(monitor.Tick(2f, Vector3.Zero, _goal), Is.EqualTo(StuckRecovery.Repath));
        }

        [TestCase(0f, 0.5f, 2f)]
        [TestCase(-1f, 0.5f, 2f)]
        [TestCase(float.NaN, 0.5f, 2f)]
        [TestCase(float.PositiveInfinity, 0.5f, 2f)]
        [TestCase(2f, 0f, 2f)]
        [TestCase(2f, float.NaN, 2f)]
        [TestCase(2f, 0.5f, 0f)]
        [TestCase(2f, 0.5f, float.PositiveInfinity)]
        public void RejectsInvalidSettings(float seconds, float distance, float goalChange)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StuckMonitor(seconds, distance, goalChange));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidDelta(float delta)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Monitor().Tick(delta, Vector3.Zero, _goal));
        }

        [Test]
        public void RejectsNonFiniteVectors()
        {
            var nan = new Vector3(float.NaN, 0f, 0f);

            Assert.Throws<ArgumentOutOfRangeException>(() => Monitor().Tick(0.1f, nan, _goal));
            Assert.Throws<ArgumentOutOfRangeException>(() => Monitor().Tick(0.1f, Vector3.Zero, nan));
            Assert.That(StuckMonitor.IsFinite(new Vector3(1f, 2f, 3f)), Is.True);
            Assert.That(StuckMonitor.IsFinite(new Vector3(0f, float.NegativeInfinity, 0f)), Is.False);
        }
    }
}

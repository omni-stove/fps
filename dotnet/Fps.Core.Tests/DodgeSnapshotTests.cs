using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    /// <summary>
    /// <see cref="DodgeMotion.Capture"/> と <see cref="DodgeMotion.Restore"/> (ネット対戦の予測の巻き戻し)。
    /// </summary>
    public sealed class DodgeSnapshotTests
    {
        private const float _tick = 1f / 30f;

        private static DodgeMotion Create()
        {
            return new DodgeMotion(new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
        }

        [Test]
        public void RestoringACaptureReplaysTheSameRollAndCooldown()
        {
            var original = Create();
            Assert.That(original.TryStart(new Vector2(1f, 0f)), Is.True);
            original.Step(_tick);
            original.Step(_tick);

            var snapshot = original.Capture();
            Assert.That(snapshot.IsRolling, Is.True);
            Assert.That(snapshot.Elapsed, Is.EqualTo(2f * _tick).Within(1e-6f));
            Assert.That(snapshot.IsValid, Is.True);

            var replay = Create();
            replay.Restore(snapshot);
            Assert.That(replay.Capture(), Is.EqualTo(snapshot));

            // 転がり終わって待ち時間が減りきるまで、どの tick も同じ変位と中身になる
            for (var i = 0; i < 40; i++)
            {
                var expected = original.Step(_tick);
                var actual = replay.Step(_tick);
                Assert.That(actual, Is.EqualTo(expected), "tick " + i);
                Assert.That(replay.Capture(), Is.EqualTo(original.Capture()), "tick " + i);
            }

            Assert.That(replay.IsReady, Is.True);
        }

        [Test]
        public void RestoringTheCooldownBlocksTheNextRoll()
        {
            var motion = Create();
            motion.Restore(new DodgeSnapshot(false, Vector2.Zero, 0f, 0.2f));

            Assert.That(motion.IsRolling, Is.False);
            Assert.That(motion.CooldownRemaining, Is.EqualTo(0.2f));
            Assert.That(motion.TryStart(new Vector2(0f, 1f)), Is.False);

            motion.Restore(DodgeSnapshot.Ready);
            Assert.That(motion.TryStart(new Vector2(0f, 1f)), Is.True);
        }

        [Test]
        public void RestoreClampsTheProgressAndNormalizesTheDirection()
        {
            var motion = Create();
            motion.Restore(new DodgeSnapshot(true, new Vector2(0f, 2f), motion.Seconds + 1f, 0f));

            Assert.That(motion.Elapsed, Is.EqualTo(motion.Seconds));
            Assert.That(motion.Direction, Is.EqualTo(new Vector2(0f, 1f)));

            // 進みきった所からは動かずに終わり、逆向きには戻らない
            var displacement = motion.Step(_tick);
            Assert.That(displacement, Is.EqualTo(Vector2.Zero));
            Assert.That(motion.IsRolling, Is.False);
        }

        [Test]
        public void InvalidSnapshotsAreRejected()
        {
            var motion = Create();

            Assert.That(new DodgeSnapshot(true, Vector2.Zero, 0f, 0f).IsValid, Is.False, "rolling needs a direction");
            Assert.That(new DodgeSnapshot(false, Vector2.Zero, -0.1f, 0f).IsValid, Is.False);
            Assert.That(new DodgeSnapshot(false, Vector2.Zero, 0f, -0.1f).IsValid, Is.False);
            Assert.That(new DodgeSnapshot(false, new Vector2(float.NaN, 0f), 0f, 0f).IsValid, Is.False);
            Assert.That(new DodgeSnapshot(false, Vector2.Zero, 0f, float.PositiveInfinity).IsValid, Is.False);
            Assert.That(DodgeSnapshot.Ready.IsValid, Is.True);

            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Restore(new DodgeSnapshot(true, Vector2.Zero, 0f, 0f)));
        }
    }
}

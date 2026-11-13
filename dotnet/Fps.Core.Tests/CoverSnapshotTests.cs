using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    /// <summary>
    /// <see cref="CoverState.Capture"/> と <see cref="CoverState.Restore"/> (ネット対戦の予測の巻き戻し) と、
    /// 種で決めるブラインドファイアの散らばり (<see cref="BlindFireShot.Spread(Vector3, float, int, int)"/>)。
    /// </summary>
    public sealed class CoverSnapshotTests
    {
        private const float _frame = 1f / 60f;

        private static readonly Vector3 _wallNormal = new Vector3(0f, 0f, -1f);

        private static CoverProbeResult Wall(CoverHeight height = CoverHeight.High, CoverEdge edges = CoverEdge.None)
        {
            return new CoverProbeResult(true, true, height, edges, _wallNormal, 0.55f);
        }

        [Test]
        public void RestoringACaptureReplaysTheSameTransitions()
        {
            var original = new CoverState();
            var pillar = Wall(CoverHeight.High, CoverEdge.Left | CoverEdge.Right);
            original.Step(true, false, Vector2.Zero, pillar, _frame);
            original.Step(false, false, new Vector2(-1f, 0f), pillar, _frame);

            // 入りかけの途中 (タイマーと左の好みを持っている) で写す
            var snapshot = original.Capture();
            Assert.That(snapshot.Phase, Is.EqualTo(CoverPhase.Entering));
            Assert.That(snapshot.EnterElapsed, Is.GreaterThan(0f));
            Assert.That(snapshot.PreferredSide, Is.EqualTo(-1f));

            var replay = new CoverState();
            replay.Restore(snapshot);
            Assert.That(replay.Capture(), Is.EqualTo(snapshot));

            for (var i = 0; i < 12; i++)
            {
                var aim = i >= 9;
                var expected = original.Step(false, aim, Vector2.Zero, pillar, _frame);
                var actual = replay.Step(false, aim, Vector2.Zero, pillar, _frame);
                Assert.That(actual, Is.EqualTo(expected), "frame " + i);
                Assert.That(replay.Capture(), Is.EqualTo(original.Capture()), "frame " + i);
            }

            // 両側とも端の柱では、写したときの好み (左) へ覗く
            Assert.That(replay.Phase, Is.EqualTo(CoverPhase.Peeking));
            Assert.That(replay.PeekOffset, Is.EqualTo(-CoverProbe.PeekSideDistance));
        }

        [Test]
        public void BackTimerSurvivesARestore()
        {
            var state = new CoverState();
            state.Step(true, false, Vector2.Zero, Wall(), 0f);
            state.Step(false, false, Vector2.Zero, Wall(), CoverState.EnterSeconds);
            state.Step(false, false, new Vector2(0f, -1f), Wall(), CoverState.BackHoldSeconds * 0.75f);

            var restored = new CoverState();
            restored.Restore(state.Capture());

            Assert.That(restored.Step(false, false, new Vector2(0f, -1f), Wall(), CoverState.BackHoldSeconds * 0.5f), Is.EqualTo(CoverTransition.Exit));
            Assert.That(restored.LastExitReason, Is.EqualTo(CoverState.ReasonBack));
        }

        [Test]
        public void DefaultSnapshotIsOutsideAndPrefersTheRight()
        {
            var state = new CoverState();
            state.Restore(CoverSnapshot.Out);

            Assert.That(state.Phase, Is.EqualTo(CoverPhase.Out));
            Assert.That(state.PreferredSide, Is.EqualTo(1f));
            Assert.That(CoverSnapshot.Out.IsValid, Is.True);
        }

        [Test]
        public void RestoreRejectsBrokenSnapshots()
        {
            var state = new CoverState();

            Assert.Throws<ArgumentOutOfRangeException>(() => state.Restore(
                new CoverSnapshot(CoverPhase.In, CoverHeight.Low, Vector3.Zero, 0f, 0f, 0f, 1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Restore(
                new CoverSnapshot(CoverPhase.In, CoverHeight.Low, _wallNormal, float.NaN, 0f, 0f, 1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Restore(
                new CoverSnapshot(CoverPhase.In, CoverHeight.Low, _wallNormal, 0f, -1f, 0f, 1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Restore(
                new CoverSnapshot((CoverPhase)42, CoverHeight.Low, _wallNormal, 0f, 0f, 0f, 1f)));
            Assert.That(state.Phase, Is.EqualTo(CoverPhase.Out), "a rejected snapshot leaves the state alone");
        }

        [Test]
        public void SeededBlindFireSpreadIsRepeatableAndInsideTheCone()
        {
            var aim = new Vector3(0f, 0f, 1f);
            for (var shot = 0; shot < 200; shot++)
            {
                var first = BlindFireShot.Spread(aim, 10f, 4, shot);
                var again = BlindFireShot.Spread(aim, 10f, 4, shot);

                Assert.That(again, Is.EqualTo(first));
                Assert.That(first.Length(), Is.EqualTo(1f).Within(1e-5f));
                var angle = Math.Acos(Math.Clamp(Vector3.Dot(first, aim), -1f, 1f)) * 180.0 / Math.PI;
                Assert.That(angle, Is.LessThanOrEqualTo(10.0 + 1e-3));
            }

            Assert.That(BlindFireShot.Spread(aim, 0f, 4, 1), Is.EqualTo(aim));
            Assert.That(BlindFireShot.Spread(aim, 10f, 4, 1), Is.Not.EqualTo(SpreadPattern.Direction(aim, 10f, 4, 1)), "a separate stream");
            Assert.Throws<ArgumentOutOfRangeException>(() => BlindFireShot.Spread(aim, 91f, 4, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => BlindFireShot.Spread(Vector3.Zero, 10f, 4, 1));
        }

        [Test]
        public void SpreadPatternDefaultStreamsAreUnchanged()
        {
            var aim = new Vector3(0.2f, 0.1f, 1f);

            Assert.That(
                SpreadPattern.Direction(aim, 5f, 9, 3),
                Is.EqualTo(SpreadPattern.Direction(aim, 5f, 9, 3, SpreadPattern.PolarStream, SpreadPattern.AzimuthStream)));
        }
    }
}

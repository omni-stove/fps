using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class PredictionBufferTests
    {
        private const float _tolerance = 0.05f;

        private int _simulateCalls;

        [SetUp]
        public void SetUp()
        {
            _simulateCalls = 0;
        }

        /// <summary>状態は 1 次元の位置、入力はその tick の移動量。</summary>
        private float Simulate(float state, float input)
        {
            _simulateCalls++;
            return state + input;
        }

        private static float Distance(float a, float b)
        {
            return Math.Abs(a - b);
        }

        private PredictionBuffer<float, float> Create(int capacity = PredictionBuffer<float, float>.DefaultCapacity)
        {
            var buffer = new PredictionBuffer<float, float>(Simulate, Distance, _tolerance, capacity);
            buffer.Reset(0, 0f);
            return buffer;
        }

        /// <summary>tick 1..ticks に毎 tick 1 ずつ進む入力で予測する。</summary>
        private static void PredictConstant(PredictionBuffer<float, float> buffer, int ticks)
        {
            for (var tick = 1; tick <= ticks; tick++)
            {
                buffer.Predict(tick, 1f);
            }
        }

        [Test]
        public void PredictAppliesInputAndAdvances()
        {
            var buffer = Create();

            var state = buffer.Predict(1, 2f);

            Assert.That(state, Is.EqualTo(2f));
            Assert.That(buffer.Current, Is.EqualTo(2f));
            Assert.That(buffer.CurrentTick, Is.EqualTo(1));
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void MatchingConfirmationDoesNothing()
        {
            var buffer = Create();
            PredictConstant(buffer, 5);
            _simulateCalls = 0;

            var result = buffer.Reconcile(3, 3f);

            Assert.That(result.Outcome, Is.EqualTo(ReconcileOutcome.Matched));
            Assert.That(result.Tick, Is.EqualTo(3));
            Assert.That(result.Error, Is.Zero);
            Assert.That(result.ResimulatedTicks, Is.Zero);
            Assert.That(_simulateCalls, Is.Zero);
            Assert.That(buffer.Current, Is.EqualTo(5f));
            Assert.That(buffer.CurrentTick, Is.EqualTo(5));
        }

        [Test]
        public void ErrorWithinToleranceDoesNothing()
        {
            var buffer = Create();
            PredictConstant(buffer, 5);
            _simulateCalls = 0;

            var result = buffer.Reconcile(3, 3.04f);

            Assert.That(result.Outcome, Is.EqualTo(ReconcileOutcome.Matched));
            Assert.That(result.Error, Is.EqualTo(0.04f).Within(1e-5f));
            Assert.That(_simulateCalls, Is.Zero);
            Assert.That(buffer.Current, Is.EqualTo(5f));
        }

        [Test]
        public void ConfirmedTicksAreDiscarded()
        {
            var buffer = Create();
            PredictConstant(buffer, 5);

            buffer.Reconcile(3, 3f);

            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(buffer.LastConfirmedTick, Is.EqualTo(3));
        }

        [Test]
        public void MismatchRewindsAndResimulatesLaterInputs()
        {
            var buffer = Create();
            buffer.Predict(1, 1f);
            buffer.Predict(2, 2f);
            buffer.Predict(3, 3f);
            buffer.Predict(4, 4f);
            _simulateCalls = 0;

            // サーバーでは tick 2 の後に 0.5 押し戻されていた
            var result = buffer.Reconcile(2, 2.5f);

            Assert.That(result.Outcome, Is.EqualTo(ReconcileOutcome.Corrected));
            Assert.That(result.Error, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(result.ResimulatedTicks, Is.EqualTo(2));
            Assert.That(_simulateCalls, Is.EqualTo(2));
            Assert.That(buffer.Current, Is.EqualTo(2.5f + 3f + 4f));
            Assert.That(buffer.CurrentTick, Is.EqualTo(4));
        }

        [Test]
        public void MismatchAtNewestTickReplacesCurrent()
        {
            var buffer = Create();
            PredictConstant(buffer, 3);

            var result = buffer.Reconcile(3, 10f);

            Assert.That(result.Outcome, Is.EqualTo(ReconcileOutcome.Corrected));
            Assert.That(result.ResimulatedTicks, Is.Zero);
            Assert.That(buffer.Current, Is.EqualTo(10f));
            Assert.That(buffer.Count, Is.Zero);
        }

        [Test]
        public void CorrectionConvergesWithServerSimulation()
        {
            // サーバーは同じ入力を同じ規則で進めるが、tick 4 だけ外から 1 押された。
            // 遅れて届く確定状態で照合を続ければ、クライアントの予測はサーバーと一致する。
            const int latency = 3;
            const int ticks = 20;
            var buffer = Create();
            var serverStates = new List<float> { 0f };
            var corrections = 0;

            for (var tick = 1; tick <= ticks; tick++)
            {
                var input = tick % 2 == 0 ? 1f : 0.5f;
                buffer.Predict(tick, input);

                var server = serverStates[tick - 1] + input + (tick == 4 ? 1f : 0f);
                serverStates.Add(server);

                var confirmedTick = tick - latency;
                if (confirmedTick >= 1)
                {
                    var result = buffer.Reconcile(confirmedTick, serverStates[confirmedTick]);
                    if (result.Outcome == ReconcileOutcome.Corrected)
                    {
                        corrections++;
                        Assert.That(result.Tick, Is.EqualTo(4));
                        Assert.That(result.Error, Is.EqualTo(1f).Within(1e-5f));
                    }
                }

                if (confirmedTick >= 4)
                {
                    Assert.That(buffer.Current, Is.EqualTo(serverStates[tick]).Within(1e-4f), "tick " + tick);
                }
            }

            Assert.That(corrections, Is.EqualTo(1));
        }

        [Test]
        public void StaleOrDuplicateConfirmationIsIgnored()
        {
            var buffer = Create();
            PredictConstant(buffer, 5);
            buffer.Reconcile(4, 4f);
            _simulateCalls = 0;

            var older = buffer.Reconcile(2, 100f);
            var duplicate = buffer.Reconcile(4, 100f);

            Assert.That(older.Outcome, Is.EqualTo(ReconcileOutcome.Ignored));
            Assert.That(duplicate.Outcome, Is.EqualTo(ReconcileOutcome.Ignored));
            Assert.That(_simulateCalls, Is.Zero);
            Assert.That(buffer.Current, Is.EqualTo(5f));
            Assert.That(buffer.LastConfirmedTick, Is.EqualTo(4));
        }

        [Test]
        public void ConfirmationForUnpredictedTickIsIgnored()
        {
            var buffer = Create();
            PredictConstant(buffer, 3);

            var result = buffer.Reconcile(7, 100f);

            Assert.That(result.Outcome, Is.EqualTo(ReconcileOutcome.Ignored));
            Assert.That(buffer.Current, Is.EqualTo(3f));
            Assert.That(buffer.Count, Is.EqualTo(3));
            Assert.That(buffer.LastConfirmedTick, Is.Zero);
        }

        [Test]
        public void ConfirmationForSkippedTickIsIgnored()
        {
            var buffer = Create();
            buffer.Predict(1, 1f);
            buffer.Predict(3, 1f);

            var result = buffer.Reconcile(2, 100f);

            Assert.That(result.Outcome, Is.EqualTo(ReconcileOutcome.Ignored));
            Assert.That(buffer.Current, Is.EqualTo(2f));
        }

        [Test]
        public void OverflowDropsOldestRecords()
        {
            var buffer = Create(capacity: 4);
            PredictConstant(buffer, 6);

            Assert.That(buffer.Count, Is.EqualTo(4));
            Assert.That(buffer.Reconcile(2, 100f).Outcome, Is.EqualTo(ReconcileOutcome.Ignored));

            var result = buffer.Reconcile(3, 4f);

            Assert.That(result.Outcome, Is.EqualTo(ReconcileOutcome.Corrected));
            Assert.That(buffer.Current, Is.EqualTo(7f));
        }

        [Test]
        public void ResetClearsRecords()
        {
            var buffer = Create();
            PredictConstant(buffer, 5);

            buffer.Reset(10, 42f);

            Assert.That(buffer.Count, Is.Zero);
            Assert.That(buffer.CurrentTick, Is.EqualTo(10));
            Assert.That(buffer.LastConfirmedTick, Is.EqualTo(10));
            Assert.That(buffer.Current, Is.EqualTo(42f));
            Assert.That(buffer.Reconcile(5, 0f).Outcome, Is.EqualTo(ReconcileOutcome.Ignored));
            Assert.That(buffer.Predict(11, 1f), Is.EqualTo(43f));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void PredictRejectsNonIncreasingTick(int tick)
        {
            var buffer = Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Predict(tick, 1f));
            Assert.That(buffer.Count, Is.Zero);
        }

        [Test]
        public void InvalidErrorMeasureThrowsWithoutChangingState()
        {
            var buffer = new PredictionBuffer<float, float>(Simulate, (a, b) => float.NaN, _tolerance);
            buffer.Reset(0, 0f);
            PredictConstant(buffer, 3);

            Assert.Throws<InvalidOperationException>(() => buffer.Reconcile(2, 2f));
            Assert.That(buffer.Count, Is.EqualTo(3));
            Assert.That(buffer.LastConfirmedTick, Is.Zero);
        }

        [Test]
        public void ConstructorValidatesArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new PredictionBuffer<float, float>(null, Distance, _tolerance));
            Assert.Throws<ArgumentNullException>(() => new PredictionBuffer<float, float>(Simulate, null, _tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PredictionBuffer<float, float>(Simulate, Distance, -0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PredictionBuffer<float, float>(Simulate, Distance, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PredictionBuffer<float, float>(Simulate, Distance, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PredictionBuffer<float, float>(Simulate, Distance, _tolerance, 0));
        }
    }
}

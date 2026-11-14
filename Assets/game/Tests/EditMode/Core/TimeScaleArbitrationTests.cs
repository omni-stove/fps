using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class TimeScaleArbitrationTests
    {
        public const float BaseFixed = 0.02f;
        public const float Within = 1e-6f;

        [Test]
        public void RequestsWithInvalidValuesAreRejected()
        {
            var arbitration = new TimeScaleArbitration();

            Assert.Throws<ArgumentException>(() => arbitration.Request(null, 0.5f, 1f));
            Assert.Throws<ArgumentException>(() => arbitration.Request(string.Empty, 0.5f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => arbitration.Request("a", 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => arbitration.Request("a", -0.5f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => arbitration.Request("a", 1.5f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => arbitration.Request("a", float.NaN, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => arbitration.Request("a", 0.5f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => arbitration.Request("a", 0.5f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => arbitration.Request("a", 0.5f, float.PositiveInfinity));
            Assert.That(arbitration.HasRequests, Is.False);
        }

        [Test]
        public void StepRejectsInvalidArguments()
        {
            var arbitration = new TimeScaleArbitration();

            Assert.Throws<ArgumentOutOfRangeException>(() => arbitration.Step(-0.1f, false, 1f, BaseFixed, null, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => arbitration.Step(float.NaN, false, 1f, BaseFixed, null, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => arbitration.Step(0.1f, false, -1f, BaseFixed, null, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => arbitration.Step(0.1f, false, 1f, 0f, null, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => arbitration.Step(0.1f, false, 1f, float.PositiveInfinity, null, out _, out _));
        }

        [Test]
        public void ANetworkSessionIsRefusedEvenWhenTheRuleIsOn()
        {
            Assert.That(TimeScaleArbitration.Check(SlowMotion.On, false), Is.EqualTo(TimeScaleRefusal.None));
            Assert.That(TimeScaleArbitration.Check(SlowMotion.Off, false), Is.EqualTo(TimeScaleRefusal.RuleOff));
            Assert.That(TimeScaleArbitration.Check(SlowMotion.On, true), Is.EqualTo(TimeScaleRefusal.NetworkSession));
            Assert.That(TimeScaleArbitration.Check(SlowMotion.Off, true), Is.EqualTo(TimeScaleRefusal.NetworkSession));
        }

        [Test]
        public void WithoutRequestsNothingIsWritten()
        {
            var arbitration = new TimeScaleArbitration();

            Assert.That(arbitration.Step(0.1f, false, 1f, BaseFixed, null, out var timeScale, out var fixedDeltaTime), Is.False);
            Assert.That(timeScale, Is.EqualTo(1f));
            Assert.That(fixedDeltaTime, Is.EqualTo(BaseFixed));
            Assert.That(arbitration.Scale, Is.EqualTo(TimeScaleArbitration.NormalScale));
            Assert.That(arbitration.IsApplied, Is.False);
        }

        [Test]
        public void TheSlowestRequestWinsAndEachExpiresOnTime()
        {
            var arbitration = new TimeScaleArbitration();
            var expired = new List<TimeScaleRequest>();
            arbitration.Request("long", 0.5f, 3f);
            Assert.That(arbitration.Request("short", 0.2f, 1f), Is.True);

            Assert.That(arbitration.Step(0f, false, 1f, BaseFixed, expired, out var timeScale, out var fixedDeltaTime), Is.True);
            Assert.That(timeScale, Is.EqualTo(0.2f).Within(Within));
            Assert.That(fixedDeltaTime, Is.EqualTo(BaseFixed * 0.2f).Within(Within));

            Assert.That(arbitration.Step(1f, false, timeScale, fixedDeltaTime, expired, out timeScale, out fixedDeltaTime), Is.True);
            Assert.That(expired, Has.Count.EqualTo(1));
            Assert.That(expired[0].Reason, Is.EqualTo("short"));
            Assert.That(timeScale, Is.EqualTo(0.5f).Within(Within));
            Assert.That(fixedDeltaTime, Is.EqualTo(BaseFixed * 0.5f).Within(Within));

            Assert.That(arbitration.Step(1.5f, false, timeScale, fixedDeltaTime, expired, out timeScale, out fixedDeltaTime), Is.False, "0.5s remain");
            Assert.That(arbitration.Requests[0].RemainingSeconds, Is.EqualTo(0.5f).Within(Within));

            Assert.That(arbitration.Step(0.5f, false, timeScale, fixedDeltaTime, expired, out timeScale, out fixedDeltaTime), Is.True);
            Assert.That(expired, Has.Count.EqualTo(2));
            Assert.That(expired[1].Reason, Is.EqualTo("long"));
            Assert.That(timeScale, Is.EqualTo(1f));
            Assert.That(fixedDeltaTime, Is.EqualTo(BaseFixed));
            Assert.That(arbitration.IsApplied, Is.False);
        }

        [Test]
        public void TheSameReasonReplacesTheScaleAndTheSeconds()
        {
            var arbitration = new TimeScaleArbitration();
            arbitration.Request("hit", 0.5f, 1f);

            Assert.That(arbitration.Request("hit", 0.25f, 4f), Is.False);

            Assert.That(arbitration.Requests, Has.Count.EqualTo(1));
            Assert.That(arbitration.Scale, Is.EqualTo(0.25f));
            Assert.That(arbitration.Requests[0].RemainingSeconds, Is.EqualTo(4f));
        }

        [Test]
        public void TheValuesBeforeTheFirstRequestAreRestored()
        {
            var arbitration = new TimeScaleArbitration();
            arbitration.Request("a", 0.5f, 1f);

            arbitration.Step(0f, false, 0.8f, 0.01f, null, out var timeScale, out var fixedDeltaTime);
            Assert.That(timeScale, Is.EqualTo(0.5f));
            Assert.That(fixedDeltaTime, Is.EqualTo(0.005f).Within(Within));

            Assert.That(arbitration.Step(1f, false, timeScale, fixedDeltaTime, null, out timeScale, out fixedDeltaTime), Is.True);
            Assert.That(timeScale, Is.EqualTo(0.8f));
            Assert.That(fixedDeltaTime, Is.EqualTo(0.01f));
        }

        [Test]
        public void PauseStopsTheClockAndWritesNothing()
        {
            var arbitration = new TimeScaleArbitration();
            var expired = new List<TimeScaleRequest>();
            arbitration.Request("a", 0.5f, 2f);
            arbitration.Step(0f, false, 1f, BaseFixed, expired, out var timeScale, out var fixedDeltaTime);

            // ポーズ画面が速さを 0 にしている間
            Assert.That(arbitration.Step(10f, true, 0f, fixedDeltaTime, expired, out var pausedScale, out _), Is.False);
            Assert.That(pausedScale, Is.EqualTo(0f));
            Assert.That(arbitration.IsPaused, Is.True);
            Assert.That(expired, Is.Empty);
            Assert.That(arbitration.Requests[0].RemainingSeconds, Is.EqualTo(2f));

            // 閉じたポーズ画面が開く前の速さへ戻した
            Assert.That(arbitration.Step(0.5f, false, timeScale, fixedDeltaTime, expired, out timeScale, out fixedDeltaTime), Is.False);
            Assert.That(arbitration.IsPaused, Is.False);
            Assert.That(arbitration.Requests[0].RemainingSeconds, Is.EqualTo(1.5f).Within(Within));

            Assert.That(arbitration.Step(1.5f, false, timeScale, fixedDeltaTime, expired, out timeScale, out fixedDeltaTime), Is.True);
            Assert.That(expired, Has.Count.EqualTo(1));
            Assert.That(timeScale, Is.EqualTo(1f));
            Assert.That(fixedDeltaTime, Is.EqualTo(BaseFixed));
        }

        [Test]
        public void AfterPauseTheRequestedScaleIsWrittenAgainEvenIfThePauseRestoredAnother()
        {
            var arbitration = new TimeScaleArbitration();
            arbitration.Request("a", 0.5f, 2f);
            arbitration.Step(0f, false, 1f, BaseFixed, null, out _, out var fixedDeltaTime);
            arbitration.Step(0.1f, true, 0f, fixedDeltaTime, null, out _, out _);

            Assert.That(arbitration.Step(0f, false, 1f, fixedDeltaTime, null, out var timeScale, out _), Is.True);
            Assert.That(timeScale, Is.EqualTo(0.5f));
        }

        [Test]
        public void CancellingDuringPauseRestoresAfterResume()
        {
            var arbitration = new TimeScaleArbitration();
            arbitration.Request("a", 0.5f, 2f);
            arbitration.Step(0f, false, 1f, BaseFixed, null, out var timeScale, out var fixedDeltaTime);
            arbitration.Step(0.1f, true, 0f, fixedDeltaTime, null, out _, out _);

            Assert.That(arbitration.Cancel("a"), Is.True);
            Assert.That(arbitration.Cancel("a"), Is.False);
            Assert.That(arbitration.Step(0.1f, true, 0f, fixedDeltaTime, null, out _, out _), Is.False, "still paused");

            Assert.That(arbitration.Step(0f, false, timeScale, fixedDeltaTime, null, out timeScale, out fixedDeltaTime), Is.True);
            Assert.That(timeScale, Is.EqualTo(1f));
            Assert.That(fixedDeltaTime, Is.EqualTo(BaseFixed));
        }

        [Test]
        public void ARequestMadeDuringPauseStartsAfterResume()
        {
            var arbitration = new TimeScaleArbitration();
            arbitration.Request("a", 0.25f, 1f);

            Assert.That(arbitration.Step(5f, true, 0f, BaseFixed, null, out _, out _), Is.False);
            Assert.That(arbitration.IsApplied, Is.False);

            Assert.That(arbitration.Step(0f, false, 1f, BaseFixed, null, out var timeScale, out _), Is.True);
            Assert.That(timeScale, Is.EqualTo(0.25f));
            Assert.That(arbitration.BaseTimeScale, Is.EqualTo(1f), "the frozen pause scale is not remembered as the base");
            Assert.That(arbitration.Requests[0].RemainingSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void FixedDeltaTimeDoesNotGoBelowUnitysMinimum()
        {
            var arbitration = new TimeScaleArbitration();
            arbitration.Request("a", 0.01f, 1f);

            arbitration.Step(0f, false, 1f, 0.002f, null, out _, out var fixedDeltaTime);

            Assert.That(fixedDeltaTime, Is.EqualTo(TimeScaleArbitration.MinFixedDeltaTime));
        }

        [Test]
        public void CancelAllReturnsTheRequestsInOrder()
        {
            var arbitration = new TimeScaleArbitration();
            arbitration.Request("a", 0.5f, 1f);
            arbitration.Request("b", 0.4f, 2f);

            var cancelled = arbitration.CancelAll();

            Assert.That(cancelled, Has.Count.EqualTo(2));
            Assert.That(cancelled[0].Reason, Is.EqualTo("a"));
            Assert.That(cancelled[1].Reason, Is.EqualTo("b"));
            Assert.That(arbitration.HasRequests, Is.False);
        }

        [Test]
        public void LogsCarryTheTagAndTheValues()
        {
            Assert.That(
                TimeScaleArbitration.FormatRefused("debug", 0.3f, 5f, TimeScaleRefusal.RuleOff),
                Is.EqualTo("[TimeScale] refused reason=debug scale=0.3 seconds=5 because=slowMotionOff"));
            Assert.That(
                TimeScaleArbitration.FormatRefused("debug", 0.3f, 5f, TimeScaleRefusal.NetworkSession),
                Is.EqualTo("[TimeScale] refused reason=debug scale=0.3 seconds=5 because=networkSession"));
            Assert.That(
                TimeScaleArbitration.FormatAccepted("debug", 0.3f, 5f, 0.3f),
                Is.EqualTo("[TimeScale] accepted reason=debug scale=0.3 seconds=5 slowest=0.3"));
            Assert.That(
                TimeScaleArbitration.FormatSet(0.3f, 0.006f),
                Is.EqualTo("[TimeScale] set timeScale=0.3 fixedDeltaTime=0.006"));
            Assert.That(
                TimeScaleArbitration.FormatRestored(1f, 0.02f),
                Is.EqualTo("[TimeScale] restored timeScale=1 fixedDeltaTime=0.02"));
            Assert.That(
                TimeScaleArbitration.FormatExpired(new TimeScaleRequest("debug", 0.3f, 0f)),
                Is.EqualTo("[TimeScale] expired reason=debug"));
            Assert.That(TimeScaleArbitration.FormatPause(true, 1), Is.EqualTo("[TimeScale] paused; holding 1 request(s)"));
            Assert.That(TimeScaleArbitration.FormatPause(false, 1), Is.EqualTo("[TimeScale] resumed; 1 request(s) remain"));
        }
    }
}

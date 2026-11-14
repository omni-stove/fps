using System;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="TimeScaleArbiter"/> がルールとネット対戦で要求を断り、受けた要求を Unity の時間に書いて戻すこと。
    /// Edit Mode では常駐の Update が走らないので、<see cref="TimeScaleArbiter.Tick"/> を直接呼ぶ。
    /// </summary>
    public sealed class TimeScaleArbiterTests
    {
        private const string Reason = "test";

        private float _timeScaleBefore;
        private float _fixedDeltaTimeBefore;
        private Func<bool> _networkSessionBefore;
        private GameObject _owner;

        [SetUp]
        public void SetUp()
        {
            _timeScaleBefore = Time.timeScale;
            _fixedDeltaTimeBefore = Time.fixedDeltaTime;
            _networkSessionBefore = TimeScaleArbiter.NetworkSessionSource;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            TimeScaleArbiter.ResetState();
            TimeScaleArbiter.NetworkSessionSource = null;
            _owner = new GameObject("TimeScaleOwner");
        }

        [TearDown]
        public void TearDown()
        {
            TimeScaleArbiter.ResetState();
            TimeScaleArbiter.NetworkSessionSource = _networkSessionBefore;
            Time.timeScale = _timeScaleBefore;
            Time.fixedDeltaTime = _fixedDeltaTimeBefore;
            if (_owner != null)
            {
                Object.DestroyImmediate(_owner);
            }
        }

        [Test]
        public void ARequestIsRefusedAndLoggedWhileTheRuleIsOff()
        {
            LogAssert.Expect(LogType.Warning, "[TimeScale] refused reason=test scale=0.5 seconds=2 because=slowMotionOff");

            Assert.That(TimeScaleArbiter.Request(Reason, 0.5f, 2f), Is.EqualTo(TimeScaleRefusal.RuleOff));

            TimeScaleArbiter.Tick(0.1f, false);
            Assert.That(TimeScaleArbiter.Requests, Is.Empty);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void ARequestIsRefusedDuringANetworkSessionEvenWhenTheRuleIsOn()
        {
            TimeScaleArbiter.ApplyRules(SlowMotion.On, _owner);
            TimeScaleArbiter.NetworkSessionSource = () => true;
            LogAssert.Expect(LogType.Warning, "[TimeScale] refused reason=test scale=0.5 seconds=2 because=networkSession");

            Assert.That(TimeScaleArbiter.Request(Reason, 0.5f, 2f), Is.EqualTo(TimeScaleRefusal.NetworkSession));
            Assert.That(TimeScaleArbiter.Requests, Is.Empty);
        }

        [Test]
        public void InvalidRequestsThrowEvenWhenTheRuleIsOff()
        {
            Assert.Throws<ArgumentException>(() => TimeScaleArbiter.Request(null, 0.5f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => TimeScaleArbiter.Request(Reason, 0f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => TimeScaleArbiter.Request(Reason, 0.5f, -1f));
        }

        [Test]
        public void AnAcceptedRequestSlowsTimeAndRestoresItWhenItExpires()
        {
            TimeScaleArbiter.ApplyRules(SlowMotion.On, _owner);

            Assert.That(TimeScaleArbiter.Request(Reason, 0.5f, 2f), Is.EqualTo(TimeScaleRefusal.None));
            Assert.That(TimeScaleArbiter.Instance, Is.Null, "Edit Mode does not leave a resident object behind");

            LogAssert.Expect(LogType.Log, "[TimeScale] set timeScale=0.5 fixedDeltaTime=0.01");
            TimeScaleArbiter.Tick(0.5f, false);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.01f).Within(1e-6f));

            LogAssert.Expect(LogType.Log, "[TimeScale] expired reason=test");
            LogAssert.Expect(LogType.Log, "[TimeScale] restored timeScale=1 fixedDeltaTime=0.02");
            TimeScaleArbiter.Tick(1.5f, false);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.02f).Within(1e-6f));
            Assert.That(TimeScaleArbiter.Requests, Is.Empty);
        }

        [Test]
        public void PauseWinsAndTheRequestedScaleComesBackAfterResume()
        {
            TimeScaleArbiter.ApplyRules(SlowMotion.On, _owner);
            TimeScaleArbiter.Request(Reason, 0.5f, 2f);
            TimeScaleArbiter.Tick(0f, false);

            // PauseMenu (Freeze) と同じく、開くと 0 にし、閉じると開く前の値へ戻す。
            Time.timeScale = 0f;
            LogAssert.Expect(LogType.Log, "[TimeScale] paused; holding 1 request(s)");
            TimeScaleArbiter.Tick(30f, true);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "the pause keeps time stopped");
            Assert.That(TimeScaleArbiter.Requests[0].RemainingSeconds, Is.EqualTo(2f), "the clock does not run while paused");

            Time.timeScale = 0.5f;
            LogAssert.Expect(LogType.Log, "[TimeScale] resumed; 1 request(s) remain");
            TimeScaleArbiter.Tick(1f, false);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));

            TimeScaleArbiter.Tick(1f, false);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.02f).Within(1e-6f));
        }

        [Test]
        public void ARequestCancelledDuringPauseIsRestoredAfterResume()
        {
            TimeScaleArbiter.ApplyRules(SlowMotion.On, _owner);
            TimeScaleArbiter.Request(Reason, 0.5f, 2f);
            TimeScaleArbiter.Tick(0f, false);
            Time.timeScale = 0f;
            TimeScaleArbiter.Tick(0.1f, true);

            Assert.That(TimeScaleArbiter.Cancel(Reason), Is.True);
            Time.timeScale = 0.5f;
            TimeScaleArbiter.Tick(0.1f, false);

            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.02f).Within(1e-6f));
        }

        [Test]
        public void TurningTheRuleOffCancelsAndRestores()
        {
            TimeScaleArbiter.ApplyRules(SlowMotion.On, _owner);
            TimeScaleArbiter.Request(Reason, 0.5f, 2f);
            TimeScaleArbiter.Tick(0f, false);

            LogAssert.Expect(LogType.Log, "[TimeScale] cancelled reason=test because=slowMotionOff");
            TimeScaleArbiter.ApplyRules(SlowMotion.Off, _owner);
            TimeScaleArbiter.Tick(0f, false);

            Assert.That(TimeScaleArbiter.Requests, Is.Empty);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void TheRuleLapsesWhenItsOwnerIsDestroyed()
        {
            TimeScaleArbiter.ApplyRules(SlowMotion.On, _owner);
            TimeScaleArbiter.Request(Reason, 0.5f, 2f);
            TimeScaleArbiter.Tick(0f, false);

            Object.DestroyImmediate(_owner);
            Assert.That(TimeScaleArbiter.Rule, Is.EqualTo(SlowMotion.Off));

            LogAssert.Expect(LogType.Log, "[TimeScale] cancelled reason=test because=" + TimeScaleArbiter.RuleOwnerGoneReason);
            TimeScaleArbiter.Tick(0f, false);
            Assert.That(TimeScaleArbiter.Requests, Is.Empty);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void ANetworkSessionThatStartsLaterCancelsTheRequests()
        {
            TimeScaleArbiter.ApplyRules(SlowMotion.On, _owner);
            TimeScaleArbiter.Request(Reason, 0.5f, 2f);
            TimeScaleArbiter.Tick(0f, false);

            TimeScaleArbiter.NetworkSessionSource = () => true;
            TimeScaleArbiter.Tick(0f, false);

            Assert.That(TimeScaleArbiter.Requests, Is.Empty);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void ApplyRulesRejectsMissingOrUnknownValues()
        {
            Assert.Throws<ArgumentNullException>(() => TimeScaleArbiter.ApplyRules(SlowMotion.On, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => TimeScaleArbiter.ApplyRules((SlowMotion)99, _owner));
        }

        [Test]
        public void PlayerActionsHandTheRuleToTheArbiter()
        {
            PlayerActions.Attach(_owner, GameRules.Default.WithActions(ActionRules.Default.WithSlowMotion(SlowMotion.On)));
            Assert.That(TimeScaleArbiter.Rule, Is.EqualTo(SlowMotion.On));

            PlayerActions.Attach(_owner, GameRules.Default);
            Assert.That(TimeScaleArbiter.Rule, Is.EqualTo(SlowMotion.Off));
        }
    }
}

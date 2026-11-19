using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class RedeployTests
    {
        private static readonly int[] _expectedReadyOrder = { 2, 3, 4 };
        private static readonly int[] _expectedSolo = { 7 };

        [Test]
        public void TheDefaultsGiveOneRedeployUpToTheSecondCircleAfterFiveSeconds()
        {
            var spec = RedeploySpec.Default;

            Assert.That(spec.Charges, Is.EqualTo(1));
            Assert.That(spec.LastPhaseIndex, Is.EqualTo(1));
            Assert.That(spec.DelaySeconds, Is.EqualTo(5f));
            Assert.That(spec.IsEnabled, Is.True);
            Assert.That(RedeploySpec.None.IsEnabled, Is.False);
            Assert.That(RedeploySpec.None.Charges, Is.Zero);
        }

        [Test]
        public void TheSpecRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RedeploySpec(charges: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RedeploySpec(lastPhaseIndex: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RedeploySpec(delaySeconds: -0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RedeploySpec(delaySeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RedeploySpec(delaySeconds: float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => new Redeploy(null));
        }

        [Test]
        public void AWaitUsesAChargeAndIsReadyOnceTheDelayPasses()
        {
            var redeploy = new Redeploy(new RedeploySpec(charges: 2, lastPhaseIndex: 1, delaySeconds: 3f));

            Assert.That(redeploy.ChargesLeft(7), Is.EqualTo(2));
            Assert.That(redeploy.Begin(7), Is.EqualTo(RedeployVerdict.Waiting));

            Assert.That(redeploy.ChargesLeft(7), Is.EqualTo(1), "the charge is spent when the wait starts");
            Assert.That(redeploy.IsWaiting(7), Is.True);
            Assert.That(redeploy.IsReady(7), Is.False);
            Assert.That(redeploy.GetRemaining(7), Is.EqualTo(3f));
            Assert.That(redeploy.Land(7), Is.False, "cannot land before the wait is over");

            Assert.That(redeploy.Tick(1f), Is.Empty);
            Assert.That(redeploy.GetRemaining(7), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(redeploy.Tick(2.5f), Is.EqualTo(_expectedSolo));
            Assert.That(redeploy.GetRemaining(7), Is.EqualTo(0f), "the remaining time does not go below zero");
            Assert.That(redeploy.IsReady(7), Is.True);

            Assert.That(redeploy.Land(7), Is.True);
            Assert.That(redeploy.IsWaiting(7), Is.False);
            Assert.That(redeploy.GetRemaining(7), Is.Null);
            Assert.That(redeploy.WaitingCount, Is.Zero);
        }

        [Test]
        public void UsingEveryChargeEndsTheRedeploys()
        {
            var redeploy = new Redeploy(new RedeploySpec(charges: 1, delaySeconds: 0f));

            Assert.That(redeploy.Begin(1), Is.EqualTo(RedeployVerdict.Waiting));
            Assert.That(redeploy.IsReady(1), Is.True, "a zero-second wait is ready at once");
            Assert.That(redeploy.Land(1), Is.True);

            Assert.That(redeploy.Check(1), Is.EqualTo(RedeployVerdict.NoCharges));
            Assert.That(redeploy.Begin(1), Is.EqualTo(RedeployVerdict.NoCharges));
            Assert.That(redeploy.IsWaiting(1), Is.False);
            Assert.That(redeploy.ChargesLeft(1), Is.Zero);
            Assert.That(redeploy.ChargesLeft(2), Is.EqualTo(1), "charges are counted per player");
        }

        [Test]
        public void PastTheLastPhaseNoOneRedeploysAndThePhaseNeverGoesBack()
        {
            var redeploy = new Redeploy(new RedeploySpec(charges: 3, lastPhaseIndex: 1, delaySeconds: 2f));

            redeploy.SetPhase(1);
            Assert.That(redeploy.IsOpen, Is.True, "the last phase itself still allows a redeploy");
            Assert.That(redeploy.Begin(1), Is.EqualTo(RedeployVerdict.Waiting));

            redeploy.SetPhase(2);
            Assert.That(redeploy.IsOpen, Is.False);
            Assert.That(redeploy.Begin(2), Is.EqualTo(RedeployVerdict.PastPhase));
            Assert.That(redeploy.ChargesLeft(2), Is.EqualTo(3), "a refused redeploy spends nothing");
            Assert.That(redeploy.IsWaiting(1), Is.True, "a wait started in time carries on after the phase moves");

            redeploy.SetPhase(0);
            Assert.That(redeploy.PhaseIndex, Is.EqualTo(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => redeploy.SetPhase(-1));
        }

        [Test]
        public void ZeroChargesIsDisabled()
        {
            var redeploy = new Redeploy(RedeploySpec.None);

            Assert.That(redeploy.IsOpen, Is.False);
            Assert.That(redeploy.Begin(1), Is.EqualTo(RedeployVerdict.Disabled));
            Assert.That(redeploy.IsWaiting(1), Is.False);
        }

        [Test]
        public void ReadyPlayersComeOutLongestWaitingFirstThenByPlayerId()
        {
            var redeploy = new Redeploy(new RedeploySpec(charges: 1, delaySeconds: 2f));
            redeploy.Begin(3);
            redeploy.Begin(2);
            redeploy.Tick(1f);
            redeploy.Begin(4);
            redeploy.Begin(5);
            redeploy.Cancel(5);
            redeploy.Begin(5);
            Assert.That(redeploy.ChargesLeft(5), Is.Zero, "a cancelled wait does not hand the charge back");
            Assert.That(redeploy.Check(5), Is.EqualTo(RedeployVerdict.NoCharges));

            var ready = redeploy.Tick(2f);

            Assert.That(ready, Is.EqualTo(_expectedReadyOrder));
        }

        [Test]
        public void CancelAndResetClearTheWaitsAndResetGivesTheChargesBack()
        {
            var redeploy = new Redeploy(new RedeploySpec(charges: 1, lastPhaseIndex: 0, delaySeconds: 2f));
            redeploy.Begin(1);
            redeploy.Begin(2);

            redeploy.Cancel(1);
            Assert.That(redeploy.IsWaiting(1), Is.False);
            Assert.That(redeploy.IsWaiting(2), Is.True);

            redeploy.CancelAll();
            Assert.That(redeploy.WaitingCount, Is.Zero);
            Assert.That(redeploy.ChargesLeft(2), Is.Zero);

            redeploy.SetPhase(3);
            redeploy.Reset();

            Assert.That(redeploy.ChargesLeft(2), Is.EqualTo(1));
            Assert.That(redeploy.PhaseIndex, Is.Zero);
            Assert.That(redeploy.IsOpen, Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => redeploy.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => redeploy.Tick(float.NaN));
        }

        [Test]
        public void TheJsonReadsEveryItemAndLeavesOutTheRestAsDefaults()
        {
            var spec = RedeployJson.Parse("{ \"charges\": 2, \"lastPhaseIndex\": 3, \"delaySeconds\": 7.5 }", "test");

            Assert.That(spec.Charges, Is.EqualTo(2));
            Assert.That(spec.LastPhaseIndex, Is.EqualTo(3));
            Assert.That(spec.DelaySeconds, Is.EqualTo(7.5f));

            var empty = RedeployJson.Parse("{}", "test");
            Assert.That(empty.Charges, Is.EqualTo(RedeploySpec.DefaultCharges));
            Assert.That(empty.LastPhaseIndex, Is.EqualTo(RedeploySpec.DefaultLastPhaseIndex));
            Assert.That(empty.DelaySeconds, Is.EqualTo(RedeploySpec.DefaultDelaySeconds));
            Assert.That(RedeployJson.Parse("{ \"charges\": 0 }", "test").IsEnabled, Is.False);
        }

        [Test]
        public void TheJsonNamesTheItemItCannotRead()
        {
            var negative = Assert.Throws<TuningJsonException>(() => RedeployJson.Parse("{ \"charges\": -1 }", "test"));
            Assert.That(negative.Item, Is.EqualTo("charges"));

            var delay = Assert.Throws<TuningJsonException>(() => RedeployJson.Parse("{ \"delaySeconds\": -3 }", "test"));
            Assert.That(delay.Item, Is.EqualTo("delaySeconds"));

            var unknown = Assert.Throws<TuningJsonException>(() => RedeployJson.Parse("{ \"lives\": 2 }", "test"));
            Assert.That(unknown.Message, Does.Contain("lives"));
        }

        [Test]
        public void TheLogLineNamesTheVerdictPhaseChargesAndWait()
        {
            Assert.That(
                BattleRoyaleLog.Redeploy(3, RedeployVerdict.Waiting, 0, 1, 5f),
                Is.EqualTo("[Mode] redeploy player=3 verdict=Waiting phase=1 left=1 in=5.00"));
            Assert.That(
                BattleRoyaleLog.Redeploy(3, RedeployVerdict.PastPhase, 2, 1, null),
                Is.EqualTo("[Mode] redeploy player=3 verdict=PastPhase phase=3 left=1"));
        }
    }
}

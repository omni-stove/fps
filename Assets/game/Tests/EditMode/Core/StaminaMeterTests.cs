using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StaminaMeterTests
    {
        private const float _epsilon = 1e-4f;

        // 最大 100、走ると毎秒 20、滑ると 30、回避で 40、毎秒 25 戻る、待ち 1 秒、尽きたら 50 まで戻るまで使えない。
        private static StaminaSpec Spec() => new StaminaSpec(100f, 20f, 30f, 40f, 25f, 1f, 50f);

        [Test]
        public void StartsFull()
        {
            var meter = new StaminaMeter(Spec());

            Assert.That(meter.Current, Is.EqualTo(100f));
            Assert.That(meter.IsFull, Is.True);
            Assert.That(meter.IsDepleted, Is.False);
            Assert.That(meter.CanSprint, Is.True);
            Assert.That(meter.Status, Is.EqualTo(new StaminaStatus(100f, 100f, false)));
        }

        [Test]
        public void SprintingDrainsPerSecond()
        {
            var meter = new StaminaMeter(Spec());

            meter.Tick(1f, true);
            Assert.That(meter.Current, Is.EqualTo(80f).Within(_epsilon));

            meter.Tick(0.5f, true);
            Assert.That(meter.Current, Is.EqualTo(70f).Within(_epsilon));
            Assert.That(meter.IsFull, Is.False);
        }

        [Test]
        public void RunningOutStopsSprintingAndSlidingAndDodgingUntilTheRecoverAmount()
        {
            var meter = new StaminaMeter(Spec());

            meter.Tick(5f, true);
            Assert.That(meter.Current, Is.EqualTo(0f));
            Assert.That(meter.IsDepleted, Is.True);
            Assert.That(meter.CanSprint, Is.False);
            Assert.That(meter.Status, Is.EqualTo(new StaminaStatus(0f, 100f, true)));

            // 押したままでも減らさず、待ちを数える。
            meter.Tick(1f, true);
            Assert.That(meter.Current, Is.EqualTo(0f));

            meter.Tick(1.6f, false);
            Assert.That(meter.Current, Is.EqualTo(40f).Within(_epsilon));
            Assert.That(meter.IsDepleted, Is.True, "above the slide cost but still below the recover amount");
            Assert.That(meter.CanSprint, Is.False);
            Assert.That(meter.CanAfford(StaminaUse.Slide), Is.False);
            Assert.That(meter.TrySpend(StaminaUse.Slide), Is.False);
            Assert.That(meter.Current, Is.EqualTo(40f).Within(_epsilon));

            meter.Tick(0.4f, false);
            Assert.That(meter.Current, Is.EqualTo(50f).Within(_epsilon));
            Assert.That(meter.IsDepleted, Is.False);
            Assert.That(meter.CanSprint, Is.True);
            Assert.That(meter.CanAfford(StaminaUse.Dodge), Is.True);
        }

        [Test]
        public void RegenWaitsForTheDelayAfterSprinting()
        {
            var meter = new StaminaMeter(Spec());
            meter.Tick(1f, true);

            meter.Tick(0.5f, false);
            Assert.That(meter.Current, Is.EqualTo(80f).Within(_epsilon), "still waiting");

            // 待ちの残り 0.5 秒をまたぐ Tick は、またいだ後の 0.5 秒分だけ戻す。
            meter.Tick(1f, false);
            Assert.That(meter.Current, Is.EqualTo(92.5f).Within(_epsilon));

            meter.Tick(1f, false);
            Assert.That(meter.Current, Is.EqualTo(100f), "does not go past the max");
            Assert.That(meter.IsFull, Is.True);
        }

        [Test]
        public void SpendingRestartsTheDelay()
        {
            var meter = new StaminaMeter(Spec());
            meter.Tick(1f, true);
            meter.Tick(0.9f, false);

            Assert.That(meter.TrySpend(StaminaUse.Slide), Is.True);
            Assert.That(meter.Current, Is.EqualTo(50f).Within(_epsilon));

            meter.Tick(0.9f, false);
            Assert.That(meter.Current, Is.EqualTo(50f).Within(_epsilon), "the delay starts over from the slide");

            meter.Tick(0.2f, false);
            Assert.That(meter.Current, Is.EqualTo(52.5f).Within(_epsilon));
        }

        [Test]
        public void SlidesAndDodgesSpendTheirCostAndAreRefusedWhenShort()
        {
            var meter = new StaminaMeter(Spec());

            Assert.That(meter.TrySpend(StaminaUse.Dodge), Is.True);
            Assert.That(meter.Current, Is.EqualTo(60f).Within(_epsilon));
            Assert.That(meter.TrySpend(StaminaUse.Slide), Is.True);
            Assert.That(meter.Current, Is.EqualTo(30f).Within(_epsilon));

            Assert.That(meter.CanAfford(StaminaUse.Dodge), Is.False);
            Assert.That(meter.TrySpend(StaminaUse.Dodge), Is.False);
            Assert.That(meter.Current, Is.EqualTo(30f).Within(_epsilon), "a refused dodge spends nothing");
            Assert.That(meter.IsDepleted, Is.False);

            Assert.That(meter.TrySpend(StaminaUse.Slide), Is.True);
            Assert.That(meter.Current, Is.EqualTo(0f).Within(_epsilon));
            Assert.That(meter.IsDepleted, Is.True, "spending the last of it runs out");
        }

        [Test]
        public void AFreeActionIsNeverRefused()
        {
            var meter = new StaminaMeter(new StaminaSpec(100f, 20f, 0f, 40f, 25f, 1f, 50f));
            meter.Tick(5f, true);

            Assert.That(meter.IsDepleted, Is.True);
            Assert.That(meter.CanAfford(StaminaUse.Slide), Is.True);
            Assert.That(meter.TrySpend(StaminaUse.Slide), Is.True);
            Assert.That(meter.CanAfford(StaminaUse.Dodge), Is.False);
        }

        [Test]
        public void NoSprintCostNeverDrains()
        {
            var meter = new StaminaMeter(new StaminaSpec(100f, 0f, 30f, 40f, 25f, 1f, 50f));

            meter.Tick(10f, true);

            Assert.That(meter.Current, Is.EqualTo(100f));
            Assert.That(meter.CanSprint, Is.True);
        }

        [Test]
        public void RefillFillsAndClearsTheDepletedState()
        {
            var meter = new StaminaMeter(Spec());
            meter.Tick(5f, true);

            meter.Refill();

            Assert.That(meter.Current, Is.EqualTo(100f));
            Assert.That(meter.IsDepleted, Is.False);

            // 満タンにしたら待ちも済んだ扱いにする。
            meter.TrySpend(StaminaUse.Slide);
            meter.Refill();
            meter.Tick(0.1f, true);
            Assert.That(meter.Current, Is.EqualTo(98f).Within(_epsilon));
        }

        [Test]
        public void TickAndSpendRejectBadValues()
        {
            var meter = new StaminaMeter(Spec());

            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Tick(-0.1f, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Tick(float.NaN, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Tick(float.PositiveInfinity, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.TrySpend(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.CanAfford(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.TrySpend((StaminaUse)99));
            Assert.Throws<ArgumentNullException>(() => new StaminaMeter(null));
        }

        [Test]
        public void TheDefaultSpecRunsOutInAFewSecondsOfSprinting()
        {
            var spec = StaminaSpec.Default;

            Assert.That(spec.Max, Is.EqualTo(StaminaSpec.DefaultMax));
            Assert.That(spec.Max / spec.SprintCostPerSecond, Is.EqualTo(5f).Within(_epsilon));
            Assert.That(spec.CostOf(StaminaUse.Slide), Is.EqualTo(StaminaSpec.DefaultSlideCost));
            Assert.That(spec.CostOf(StaminaUse.Dodge), Is.EqualTo(StaminaSpec.DefaultDodgeCost));
            Assert.That(spec.RegenDelaySeconds, Is.EqualTo(StaminaSpec.DefaultRegenDelaySeconds));
            Assert.That(spec.RecoverAmount, Is.EqualTo(StaminaSpec.DefaultRecoverAmount));
        }

        [Test]
        public void SpecRejectsBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StaminaSpec(max: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StaminaSpec(max: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StaminaSpec(sprintCostPerSecond: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StaminaSpec(slideCost: 101f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StaminaSpec(dodgeCost: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StaminaSpec(regenPerSecond: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StaminaSpec(regenDelaySeconds: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StaminaSpec(recoverAmount: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StaminaSpec(recoverAmount: 101f));
            Assert.Throws<ArgumentOutOfRangeException>(() => StaminaSpec.Default.CostOf((StaminaUse)99));
        }

        [Test]
        public void LogLinesCarryTheValues()
        {
            var status = new StaminaStatus(12.346f, 100f, true);

            Assert.That(StaminaLog.FormatDepleted(status), Is.EqualTo("[Stamina] depleted current=12.35 max=100.00 depleted=true"));
            Assert.That(StaminaLog.FormatRecovered(new StaminaStatus(50f, 100f, false)), Is.EqualTo("[Stamina] recovered current=50.00 max=100.00 depleted=false"));
            Assert.That(StaminaLog.FormatFull(new StaminaStatus(100f, 100f, false)), Is.EqualTo("[Stamina] full current=100.00 max=100.00 depleted=false"));
            Assert.That(StaminaLog.FormatRefilled(new StaminaStatus(100f, 100f, false)), Is.EqualTo("[Stamina] refilled current=100.00 max=100.00 depleted=false"));
            Assert.That(
                StaminaLog.FormatSpent(StaminaUse.Dodge, 40f, new StaminaStatus(60f, 100f, false)),
                Is.EqualTo("[Stamina] spent use=Dodge cost=40.00 current=60.00 max=100.00 depleted=false"));
            Assert.That(
                StaminaLog.FormatRefused(StaminaUse.Slide, 30f, new StaminaStatus(10f, 100f, false)),
                Is.EqualTo("[Stamina] refused use=Slide cost=30.00 current=10.00 max=100.00 depleted=false"));
            Assert.That(
                StaminaLog.FormatConfigured(Stamina.On, Spec()),
                Is.EqualTo("[Stamina] rule=On max=100.00 sprintPerSecond=20.00 slide=30.00 dodge=40.00 regenPerSecond=25.00 regenDelay=1.00 recover=50.00"));
            Assert.Throws<ArgumentNullException>(() => StaminaLog.FormatConfigured(Stamina.Off, null));
        }
    }
}

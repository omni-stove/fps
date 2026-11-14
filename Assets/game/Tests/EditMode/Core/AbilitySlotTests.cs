using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AbilitySlotTests
    {
        [Test]
        public void SpecRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AbilitySpec(0, 1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AbilitySpec(1, -1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AbilitySpec(1, float.NaN, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AbilitySpec(1, 1f, float.PositiveInfinity, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AbilitySpec(1, 1f, 1f, -0.1f));
            Assert.Throws<ArgumentNullException>(() => new AbilitySlot(null));
        }

        [Test]
        public void TheDefaultSpecHasOneCharge()
        {
            Assert.That(AbilitySpec.Default.MaxCharges, Is.EqualTo(AbilitySpec.DefaultMaxCharges));
            Assert.That(AbilitySpec.Default.RechargeSeconds, Is.EqualTo(AbilitySpec.DefaultRechargeSeconds));
            Assert.That(AbilitySpec.Default.CooldownSeconds, Is.EqualTo(AbilitySpec.DefaultCooldownSeconds));
            Assert.That(AbilitySpec.Default.WindupSeconds, Is.EqualTo(AbilitySpec.DefaultWindupSeconds));
        }

        [Test]
        public void StartsFullAndReady()
        {
            var slot = new AbilitySlot(new AbilitySpec(2, 5f, 1f, 0f));

            Assert.That(slot.Charges, Is.EqualTo(2));
            Assert.That(slot.Phase, Is.EqualTo(AbilityPhase.Idle));
            Assert.That(slot.CanStart, Is.True);
            Assert.That(slot.SecondsUntilReady, Is.Zero);
        }

        [Test]
        public void UsingSpendsAChargeAndStartsTheCooldown()
        {
            var slot = new AbilitySlot(new AbilitySpec(2, 5f, 1f, 0f));

            Assert.That(slot.TryStart(), Is.True);

            Assert.That(slot.Charges, Is.EqualTo(1));
            Assert.That(slot.Phase, Is.EqualTo(AbilityPhase.Active), "no windup means the effect starts at once");
            Assert.That(slot.CooldownRemaining, Is.EqualTo(1f));
            Assert.That(slot.Block, Is.EqualTo(AbilityUseBlock.Running));
            Assert.That(slot.TryStart(), Is.False);
        }

        [Test]
        public void CannotUseAgainUntilTheCooldownEnds()
        {
            var slot = new AbilitySlot(new AbilitySpec(2, 5f, 1f, 0f));
            slot.TryStart();
            slot.End();

            slot.Tick(0.4f);
            Assert.That(slot.Block, Is.EqualTo(AbilityUseBlock.Cooldown));
            Assert.That(slot.SecondsUntilReady, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(slot.TryStart(), Is.False);
            Assert.That(slot.Charges, Is.EqualTo(1), "a refused use keeps the charge");

            slot.Tick(0.6f);
            Assert.That(slot.CanStart, Is.True);
            Assert.That(slot.TryStart(), Is.True);
            Assert.That(slot.Charges, Is.Zero);
        }

        [Test]
        public void WindupDelaysTheEffect()
        {
            var slot = new AbilitySlot(new AbilitySpec(1, 5f, 0f, 0.5f));
            slot.TryStart();

            Assert.That(slot.Phase, Is.EqualTo(AbilityPhase.WindingUp));
            Assert.That(slot.Tick(0.3f) & AbilitySlotStep.EffectStarted, Is.EqualTo(AbilitySlotStep.None));
            Assert.That(slot.WindupRemaining, Is.EqualTo(0.2f).Within(1e-5f));

            Assert.That(slot.Tick(0.3f) & AbilitySlotStep.EffectStarted, Is.EqualTo(AbilitySlotStep.EffectStarted));
            Assert.That(slot.Phase, Is.EqualTo(AbilityPhase.Active));
            Assert.That(slot.WindupRemaining, Is.Zero);
        }

        [Test]
        public void EndingDuringWindupNeverStartsTheEffect()
        {
            var slot = new AbilitySlot(new AbilitySpec(1, 5f, 0f, 0.5f));
            slot.TryStart();

            Assert.That(slot.End(), Is.True);
            Assert.That(slot.End(), Is.False);
            Assert.That(slot.Tick(1f) & AbilitySlotStep.EffectStarted, Is.EqualTo(AbilitySlotStep.None));
            Assert.That(slot.Phase, Is.EqualTo(AbilityPhase.Idle));
        }

        [Test]
        public void ChargesRecoverOneAtATime()
        {
            var slot = new AbilitySlot(new AbilitySpec(2, 2f, 0f, 0f));
            slot.TryStart();
            slot.End();
            slot.TryStart();
            slot.End();
            Assert.That(slot.Charges, Is.Zero);
            Assert.That(slot.Block, Is.EqualTo(AbilityUseBlock.NoCharges));
            Assert.That(slot.SecondsUntilReady, Is.EqualTo(2f));

            Assert.That(slot.Tick(1.5f), Is.EqualTo(AbilitySlotStep.None));
            Assert.That(slot.RechargeRemaining, Is.EqualTo(0.5f).Within(1e-5f));

            Assert.That(slot.Tick(0.5f), Is.EqualTo(AbilitySlotStep.Recharged));
            Assert.That(slot.Charges, Is.EqualTo(1));

            Assert.That(slot.Tick(5f), Is.EqualTo(AbilitySlotStep.Recharged));
            Assert.That(slot.Charges, Is.EqualTo(2), "never above the maximum");
            Assert.That(slot.RechargeElapsed, Is.Zero);
        }

        [Test]
        public void ChargesRecoverWhileTheAbilityIsRunning()
        {
            var slot = new AbilitySlot(new AbilitySpec(1, 1f, 0f, 0f));
            slot.TryStart();

            Assert.That(slot.Tick(1f), Is.EqualTo(AbilitySlotStep.Recharged));
            Assert.That(slot.Charges, Is.EqualTo(1));
            Assert.That(slot.Block, Is.EqualTo(AbilityUseBlock.Running));
        }

        [Test]
        public void ZeroRechargeNeverRecovers()
        {
            var slot = new AbilitySlot(new AbilitySpec(1, 0f, 0f, 0f));
            slot.TryStart();
            slot.End();

            Assert.That(slot.Tick(100f), Is.EqualTo(AbilitySlotStep.None));
            Assert.That(slot.Charges, Is.Zero);
            Assert.That(float.IsPositiveInfinity(slot.SecondsUntilReady), Is.True);
        }

        [Test]
        public void ResetRefillsAndClearsTheWait()
        {
            var slot = new AbilitySlot(new AbilitySpec(2, 5f, 3f, 1f));
            slot.TryStart();
            slot.Tick(0.5f);

            slot.Reset();

            Assert.That(slot.Charges, Is.EqualTo(2));
            Assert.That(slot.Phase, Is.EqualTo(AbilityPhase.Idle));
            Assert.That(slot.CooldownRemaining, Is.Zero);
            Assert.That(slot.WindupRemaining, Is.Zero);
            Assert.That(slot.RechargeElapsed, Is.Zero);
            Assert.That(slot.CanStart, Is.True);
        }

        [Test]
        public void TickRejectsInvalidTime()
        {
            var slot = new AbilitySlot(AbilitySpec.Default);

            Assert.Throws<ArgumentOutOfRangeException>(() => slot.Tick(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => slot.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => slot.Tick(float.PositiveInfinity));
        }

        [Test]
        public void HudShowsChargesWaitAndUse()
        {
            var slot = new AbilitySlot(new AbilitySpec(2, 4f, 1f, 0f));
            Assert.That(HudFormat.Ability(1, slot), Is.EqualTo(new LocalizedText("hud.ability", 1, 2, 2)));

            slot.TryStart();
            Assert.That(HudFormat.Ability(1, slot), Is.EqualTo(new LocalizedText("hud.ability.active", 1, 1, 2)));

            slot.End();
            slot.Tick(0.42f);
            Assert.That(HudFormat.Ability(2, slot), Is.EqualTo(new LocalizedText("hud.ability.wait", 2, 1, 2, "0.6")));

            slot.Tick(1f);
            Assert.That(HudFormat.Ability(2, slot), Is.EqualTo(new LocalizedText("hud.ability", 2, 1, 2)));
        }

        [Test]
        public void HudShowsOnlyTheCountWhenChargesNeverReturn()
        {
            var slot = new AbilitySlot(new AbilitySpec(1, 0f, 0f, 0f));
            slot.TryStart();
            slot.End();

            Assert.That(HudFormat.Ability(1, slot), Is.EqualTo(new LocalizedText("hud.ability", 1, 0, 1)));
        }

        [Test]
        public void HudRejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.Ability(0, new AbilitySlot(AbilitySpec.Default)));
            Assert.Throws<ArgumentNullException>(() => HudFormat.Ability(1, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.AbilityWaitSeconds(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.AbilityWaitSeconds(float.PositiveInfinity));
        }

        [Test]
        public void RefundGivesBackTheChargeAndClearsTheCooldown()
        {
            var slot = new AbilitySlot(new AbilitySpec(2, 5f, 3f, 0f));
            slot.TryStart();
            slot.Tick(1f);
            slot.End();

            slot.Refund();

            Assert.That(slot.Charges, Is.EqualTo(2));
            Assert.That(slot.CooldownRemaining, Is.Zero);
            Assert.That(slot.RechargeElapsed, Is.Zero, "full again, so nothing is recharging");
            Assert.That(slot.CanStart, Is.True);
        }

        [Test]
        public void RefundKeepsTheRechargeWhileStillShort()
        {
            var slot = new AbilitySlot(new AbilitySpec(3, 5f, 1f, 0f));
            slot.TryStart();
            slot.End();
            slot.Tick(1f);
            slot.TryStart();
            slot.Tick(1f);
            slot.End();

            slot.Refund();

            Assert.That(slot.Charges, Is.EqualTo(2));
            Assert.That(slot.RechargeElapsed, Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void RefundNeverGoesOverTheMax()
        {
            var slot = new AbilitySlot(new AbilitySpec(1, 5f, 1f, 0f));

            slot.Refund();

            Assert.That(slot.Charges, Is.EqualTo(1));
        }

        [Test]
        public void WaitSecondsRoundUpToATenth()
        {
            Assert.That(HudFormat.AbilityWaitSeconds(3.42f), Is.EqualTo("3.5"));
            Assert.That(HudFormat.AbilityWaitSeconds(3f), Is.EqualTo("3.0"));
            Assert.That(HudFormat.AbilityWaitSeconds(0.01f), Is.EqualTo("0.1"));
            Assert.That(HudFormat.AbilityWaitSeconds(0f), Is.EqualTo("0.0"));
        }
    }
}

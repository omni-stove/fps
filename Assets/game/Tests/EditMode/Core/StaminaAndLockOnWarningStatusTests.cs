using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StaminaAndLockOnWarningStatusTests
    {
        [Test]
        public void StaminaKeepsItsValuesAndTheFraction()
        {
            var status = new StaminaStatus(25f, 100f, false);

            Assert.That(status.Current, Is.EqualTo(25f));
            Assert.That(status.Max, Is.EqualTo(100f));
            Assert.That(status.Depleted, Is.False);
            Assert.That(status.Fraction, Is.EqualTo(0.25f));
            Assert.That(default(StaminaStatus).Fraction, Is.EqualTo(0f), "an unset status is empty, not NaN");
        }

        [Test]
        public void StaminaCanStayDepletedAboveZero()
        {
            var recovering = new StaminaStatus(10f, 100f, true);

            Assert.That(recovering.Depleted, Is.True);
            Assert.That(recovering, Is.Not.EqualTo(new StaminaStatus(10f, 100f, false)));
            Assert.That(recovering == new StaminaStatus(10f, 100f, true), Is.True);
        }

        [TestCase(0f, 0f)]
        [TestCase(0f, -1f)]
        [TestCase(0f, float.PositiveInfinity)]
        [TestCase(0f, float.NaN)]
        [TestCase(-1f, 100f)]
        [TestCase(101f, 100f)]
        [TestCase(float.NaN, 100f)]
        public void StaminaRejectsAnAmountOutsideZeroToMax(float current, float max)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StaminaStatus(current, max, false));
        }

        [TestCase(0f, 0f)]
        [TestCase(90f, 90f)]
        [TestCase(180f, 180f)]
        [TestCase(-180f, 180f)]
        [TestCase(270f, -90f)]
        [TestCase(-270f, 90f)]
        [TestCase(725f, 5f)]
        public void LockOnWarningBearingIsWrappedToOneTurn(float given, float expected)
        {
            var status = new LockOnWarningStatus(LockOnWarningStage.Tracking, given);

            Assert.That(status.BearingDegrees, Is.EqualTo(expected).Within(1e-4f));
            Assert.That(status.IsWarning, Is.True);
        }

        [Test]
        public void LockOnWarningWithoutAThreatHasNoBearing()
        {
            var none = new LockOnWarningStatus(LockOnWarningStage.None, 120f);

            Assert.That(none.IsWarning, Is.False);
            Assert.That(none.BearingDegrees, Is.EqualTo(0f), "a bearing without a threat would only cause extra change notices");
            Assert.That(none, Is.EqualTo(LockOnWarningStatus.None));
        }

        [Test]
        public void LockOnWarningTellsTheStagesApart()
        {
            var tracking = new LockOnWarningStatus(LockOnWarningStage.Tracking, 30f);
            var locked = new LockOnWarningStatus(LockOnWarningStage.Locked, 30f);

            Assert.That(locked.Stage, Is.EqualTo(LockOnWarningStage.Locked));
            Assert.That(tracking != locked, Is.True);
            Assert.That(locked, Is.EqualTo(new LockOnWarningStatus(LockOnWarningStage.Locked, 390f)), "the same direction one turn later");
        }

        [Test]
        public void LockOnWarningRejectsAnUnknownStageAndANonFiniteBearing()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LockOnWarningStatus((LockOnWarningStage)9, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LockOnWarningStatus(LockOnWarningStage.Locked, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LockOnWarningStatus(LockOnWarningStage.Locked, float.PositiveInfinity));
        }

        [Test]
        public void BailOutIsANewLeaveReasonAfterTheExistingOnes()
        {
            Assert.That((int)VehicleLeaveReason.RiderRemoved, Is.EqualTo(5));
            Assert.That((int)VehicleLeaveReason.BailOut, Is.EqualTo(6));
        }
    }
}

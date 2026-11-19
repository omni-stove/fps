using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class VehicleSeatTransitionTests
    {
        [Test]
        public void ANewTransitionIsIdleAndFinished()
        {
            var transition = new VehicleSeatTransition();

            Assert.That(transition.IsActive, Is.False);
            Assert.That(transition.Kind, Is.EqualTo(VehicleSeatTransitionKind.None));
            Assert.That(transition.Progress, Is.EqualTo(1f));
            Assert.That(transition.Advance(1f), Is.EqualTo(VehicleSeatTransitionKind.None));
        }

        [Test]
        public void ItReportsTheKindOnceTheSecondsHavePassed()
        {
            var transition = new VehicleSeatTransition();
            transition.Begin(VehicleSeatTransitionKind.Entering, 1f);

            Assert.That(transition.Advance(0.25f), Is.EqualTo(VehicleSeatTransitionKind.None));
            Assert.That(transition.Progress, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(transition.Advance(0.5f), Is.EqualTo(VehicleSeatTransitionKind.None));
            Assert.That(transition.Progress, Is.EqualTo(0.75f).Within(1e-5f));

            Assert.That(transition.Advance(0.5f), Is.EqualTo(VehicleSeatTransitionKind.Entering));
            Assert.That(transition.IsActive, Is.False, "a finished transition clears itself");
            Assert.That(transition.Advance(0.5f), Is.EqualTo(VehicleSeatTransitionKind.None), "it finishes only once");
        }

        [Test]
        public void ZeroSecondsFinishesOnTheNextStep()
        {
            var transition = new VehicleSeatTransition();
            transition.Begin(VehicleSeatTransitionKind.Exiting, 0f);

            Assert.That(transition.Progress, Is.EqualTo(1f));
            Assert.That(transition.Advance(0f), Is.EqualTo(VehicleSeatTransitionKind.Exiting));
        }

        [Test]
        public void ClearingDropsTheTransitionWithoutFinishingIt()
        {
            var transition = new VehicleSeatTransition();
            transition.Begin(VehicleSeatTransitionKind.Exiting, 1f);

            transition.Clear();

            Assert.That(transition.IsActive, Is.False);
            Assert.That(transition.Advance(2f), Is.EqualTo(VehicleSeatTransitionKind.None));
        }

        [Test]
        public void RejectsBadValues()
        {
            var transition = new VehicleSeatTransition();

            Assert.Throws<ArgumentOutOfRangeException>(() => transition.Begin(VehicleSeatTransitionKind.None, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => transition.Begin((VehicleSeatTransitionKind)9, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => transition.Begin(VehicleSeatTransitionKind.Entering, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => transition.Begin(VehicleSeatTransitionKind.Entering, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => transition.Advance(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => transition.Advance(float.PositiveInfinity));
        }

        [Test]
        public void TheLogNamesTheKind()
        {
            Assert.That(
                VehicleLog.TransitionBegin("Car", "Player", 0, VehicleSeatTransitionKind.Entering, 1f),
                Is.EqualTo("[Vehicle] 'Car' enter begin rider=Player seat=0 seconds=1.00"));
            Assert.That(
                VehicleLog.TransitionEnd("Car", "Player", 1, VehicleSeatTransitionKind.Exiting),
                Is.EqualTo("[Vehicle] 'Car' exit end rider=Player seat=1"));
            Assert.Throws<ArgumentOutOfRangeException>(() => VehicleLog.TransitionEnd("Car", "Player", 0, VehicleSeatTransitionKind.None));
        }
    }
}

using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class VehicleMotorInputTests
    {
        [Test]
        public void KeepsTheAxesItWasBuiltWith()
        {
            var input = new VehicleMotorInput(0.5f, -1f, 0.25f, 1f);

            Assert.That(input.Throttle, Is.EqualTo(0.5f));
            Assert.That(input.Steer, Is.EqualTo(-1f));
            Assert.That(input.Brake, Is.EqualTo(0.25f));
            Assert.That(input.Vertical, Is.EqualTo(1f));
            Assert.That(input.IsIdle, Is.False);
            Assert.That(input, Is.EqualTo(new VehicleMotorInput(0.5f, -1f, 0.25f, 1f)));
            Assert.That(input == new VehicleMotorInput(0.5f, -1f, 0.25f, 0f), Is.False);
            Assert.That(input.ToString(), Is.EqualTo("(throttle=0.5 steer=-1 brake=0.25 vertical=1)"));
        }

        [Test]
        public void IdleIsAllZero()
        {
            Assert.That(VehicleMotorInput.Idle.IsIdle, Is.True);
            Assert.That(VehicleMotorInput.Idle, Is.EqualTo(new VehicleMotorInput(0f, 0f, 0f, 0f)));
        }

        [Test]
        public void RejectsAxesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleMotorInput(1.1f, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleMotorInput(0f, -1.1f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleMotorInput(0f, 0f, -0.1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleMotorInput(0f, 0f, 1.1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleMotorInput(0f, 0f, 0f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleMotorInput(float.NaN, 0f, 0f, 0f));
        }
    }
}

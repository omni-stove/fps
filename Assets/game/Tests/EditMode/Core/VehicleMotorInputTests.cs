using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class VehicleMotorInputTests
    {
        [Test]
        public void KeepsTheAxesItWasBuiltWithAndLeavesTheRestAtZero()
        {
            var input = new VehicleMotorInput(0.5f, -1f, 0.25f, 1f);

            Assert.That(input[0], Is.EqualTo(0.5f));
            Assert.That(input[1], Is.EqualTo(-1f));
            Assert.That(input[2], Is.EqualTo(0.25f));
            Assert.That(input[3], Is.EqualTo(1f));
            Assert.That(input[4], Is.EqualTo(0f));
            Assert.That(input[7], Is.EqualTo(0f));
            Assert.That(input.IsIdle, Is.False);
            Assert.That(input, Is.EqualTo(new VehicleMotorInput(0.5f, -1f, 0.25f, 1f, 0f, 0f, 0f, 0f)));
            Assert.That(input == new VehicleMotorInput(0.5f, -1f, 0.25f, 0f), Is.False);
            Assert.That(input.ToString(), Is.EqualTo("(0.5 -1 0.25 1 0 0 0 0)"));
        }

        [Test]
        public void UsesEveryAxis()
        {
            var input = new VehicleMotorInput(0f, 0f, 0f, 0f, 0.1f, -0.2f, 0.3f, -1f);

            Assert.That(input[4], Is.EqualTo(0.1f));
            Assert.That(input[5], Is.EqualTo(-0.2f));
            Assert.That(input[6], Is.EqualTo(0.3f));
            Assert.That(input[7], Is.EqualTo(-1f));
            Assert.That(input, Is.Not.EqualTo(new VehicleMotorInput(0f, 0f, 0f, 0f, 0.1f, -0.2f, 0.3f, 1f)));
        }

        [Test]
        public void WithReplacesOneAxis()
        {
            var input = VehicleMotorInput.Idle.With(6, -0.5f).With(1, 1f);

            Assert.That(input, Is.EqualTo(new VehicleMotorInput(0f, 1f, 0f, 0f, 0f, 0f, -0.5f, 0f)));
            Assert.That(input.With(6, 0f).With(1, 0f).IsIdle, Is.True);
        }

        [Test]
        public void IdleIsAllZero()
        {
            Assert.That(VehicleMotorInput.Idle.IsIdle, Is.True);
            Assert.That(VehicleMotorInput.Idle, Is.EqualTo(new VehicleMotorInput()));
            Assert.That(VehicleMotorInput.Idle, Is.EqualTo(new VehicleMotorInput(0f, 0f, 0f, 0f)));
            Assert.That(VehicleMotorInput.Idle.ToString(), Is.EqualTo("(0 0 0 0 0 0 0 0)"));
        }

        [Test]
        public void RejectsAxesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleMotorInput(1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleMotorInput(0f, -1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleMotorInput(0f, 0f, 0f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleMotorInput(0f, 0f, 0f, 0f, 0f, 0f, 0f, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => VehicleMotorInput.Idle.With(2, 1.5f));
        }

        [Test]
        public void RejectsAxisNumbersOutsideTheInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = VehicleMotorInput.Idle[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = VehicleMotorInput.Idle[VehicleMotorInput.AxisCount]);
            Assert.Throws<ArgumentOutOfRangeException>(() => VehicleMotorInput.Idle.With(VehicleMotorInput.AxisCount, 0f));
        }
    }
}

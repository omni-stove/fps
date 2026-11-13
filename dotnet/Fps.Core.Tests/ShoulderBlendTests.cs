using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ShoulderBlendTests
    {
        private const float _seconds = ShoulderBlend.DefaultSeconds;

        [Test]
        public void StartsSettledOnTheGivenShoulder()
        {
            var right = new ShoulderBlend();
            Assert.That(right.Target, Is.EqualTo(ShoulderSide.Right));
            Assert.That(right.OffsetSign, Is.EqualTo(1f));
            Assert.That(right.IsBlending, Is.False);
            Assert.That(right.Seconds, Is.EqualTo(0.15f));

            var left = new ShoulderBlend(ShoulderSide.Left);
            Assert.That(left.Target, Is.EqualTo(ShoulderSide.Left));
            Assert.That(left.OffsetSign, Is.EqualTo(-1f));
            Assert.That(left.IsBlending, Is.False);
        }

        [Test]
        public void ToggleDoesNotJumpUntilTimePasses()
        {
            var blend = new ShoulderBlend();

            blend.Toggle();

            Assert.That(blend.Target, Is.EqualTo(ShoulderSide.Left));
            Assert.That(blend.IsBlending, Is.True);
            Assert.That(blend.OffsetSign, Is.EqualTo(1f), "the camera stays on the right shoulder until the blend advances");
        }

        [Test]
        public void ReachesTheLeftShoulderIn015Seconds()
        {
            var blend = new ShoulderBlend();
            blend.Toggle();

            Assert.That(blend.Tick(_seconds), Is.EqualTo(-1f));
            Assert.That(blend.IsBlending, Is.False);
            Assert.That(blend.Progress, Is.EqualTo(1f));
        }

        [Test]
        public void MovesSteadilyTowardTheOtherShoulderAndStopsThere()
        {
            var blend = new ShoulderBlend();
            blend.Toggle();

            var previous = blend.OffsetSign;
            for (var i = 0; i < 5; i++)
            {
                var sign = blend.Tick(_seconds / 6f);
                Assert.That(sign, Is.LessThan(previous).And.GreaterThan(-1f), "step " + i);
                previous = sign;
            }

            Assert.That(blend.Tick(_seconds), Is.EqualTo(-1f), "it does not overshoot");
            Assert.That(blend.Tick(_seconds), Is.EqualTo(-1f));
        }

        [Test]
        public void HalfwayIsBehindTheHead()
        {
            var blend = new ShoulderBlend();
            blend.Toggle();

            Assert.That(blend.Tick(_seconds / 2f), Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void TogglingMidwayTurnsBackFromWhereItIs()
        {
            var blend = new ShoulderBlend();
            blend.Toggle();
            var before = blend.Tick(_seconds * 0.3f);
            Assume.That(before, Is.LessThan(1f).And.GreaterThan(0f));

            blend.Toggle();

            Assert.That(blend.Target, Is.EqualTo(ShoulderSide.Right));
            Assert.That(blend.OffsetSign, Is.EqualTo(before).Within(1e-6f), "turning back must not jump");
            Assert.That(blend.Tick(_seconds * 0.1f), Is.GreaterThan(before), "it heads back to the right");
            Assert.That(blend.Tick(_seconds * 0.2f), Is.EqualTo(1f).Within(1e-6f), "going back takes as long as it had come");
        }

        [Test]
        public void SetTargetOnlyTogglesWhenTheSideChanges()
        {
            var blend = new ShoulderBlend();

            Assert.That(blend.SetTarget(ShoulderSide.Right), Is.False);
            Assert.That(blend.IsBlending, Is.False);

            Assert.That(blend.SetTarget(ShoulderSide.Left), Is.True);
            Assert.That(blend.Target, Is.EqualTo(ShoulderSide.Left));
            Assert.That(blend.IsBlending, Is.True);
        }

        [Test]
        public void ZeroSecondsSwapsAtTheNextTick()
        {
            var blend = new ShoulderBlend(ShoulderSide.Right, 0f);
            blend.Toggle();

            Assert.That(blend.Tick(0f), Is.EqualTo(-1f));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TickRejectsInvalidDeltaSeconds(float deltaSeconds)
        {
            var blend = new ShoulderBlend();
            blend.Toggle();

            Assert.Throws<ArgumentOutOfRangeException>(() => blend.Tick(deltaSeconds));
            Assert.That(blend.Progress, Is.Zero, "a rejected tick does not move the blend");
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidSeconds(float seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShoulderBlend(ShoulderSide.Right, seconds));
        }

        [Test]
        public void RejectsUnknownShoulder()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShoulderBlend((ShoulderSide)99));
            var blend = new ShoulderBlend();
            Assert.Throws<ArgumentOutOfRangeException>(() => blend.SetTarget((ShoulderSide)99));
            Assert.That(blend.Target, Is.EqualTo(ShoulderSide.Right));
        }
    }
}

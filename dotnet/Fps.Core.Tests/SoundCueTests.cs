using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class SoundCueTests
    {
        [TestCase(0f, SoundCueDirection.Front)]
        [TestCase(22.4f, SoundCueDirection.Front)]
        [TestCase(22.5f, SoundCueDirection.FrontRight)]
        [TestCase(45f, SoundCueDirection.FrontRight)]
        [TestCase(90f, SoundCueDirection.Right)]
        [TestCase(135f, SoundCueDirection.BackRight)]
        [TestCase(180f, SoundCueDirection.Back)]
        [TestCase(225f, SoundCueDirection.BackLeft)]
        [TestCase(270f, SoundCueDirection.Left)]
        [TestCase(315f, SoundCueDirection.FrontLeft)]
        [TestCase(337.4f, SoundCueDirection.FrontLeft)]
        [TestCase(337.5f, SoundCueDirection.Front)]
        [TestCase(359.9f, SoundCueDirection.Front)]
        [TestCase(-90f, SoundCueDirection.Left)]
        [TestCase(720f + 180f, SoundCueDirection.Back)]
        public void TheScreenAngleIsSplitIntoEightDirections(float degrees, SoundCueDirection expected)
        {
            Assert.That(SoundCue.DirectionOf(degrees), Is.EqualTo(expected));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ANonFiniteAngleHasNoDirection(float degrees)
        {
            Assert.That(SoundCue.DirectionOf(degrees), Is.EqualTo(SoundCueDirection.None));
        }

        [Test]
        public void ASoundAheadIsInFront()
        {
            Assert.That(
                SoundCue.TryLocate(SoundCueKind.Gunshot, Vector3.Zero, 0f, new Vector3(0f, 0f, 10f), 100f, out var cue),
                Is.True);

            Assert.That(cue.Kind, Is.EqualTo(SoundCueKind.Gunshot));
            Assert.That(cue.Direction, Is.EqualTo(SoundCueDirection.Front));
            Assert.That(cue.Distance, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(cue.IsFar, Is.False);
        }

        // +Z を向いて -X・-Z の方 (左後ろ) から撃たれる
        [Test]
        public void ASoundFromBehindOnTheLeftIsBackLeft()
        {
            SoundCue.TryLocate(SoundCueKind.Gunshot, Vector3.Zero, 0f, new Vector3(-5f, 1f, -5f), 100f, out var cue);

            Assert.That(cue.Direction, Is.EqualTo(SoundCueDirection.BackLeft));
        }

        // 向きは聞いている人の向きで決まる。+X を向けば -X の音は後ろ
        [Test]
        public void TheDirectionFollowsTheListenerYaw()
        {
            SoundCue.TryLocate(SoundCueKind.Footstep, Vector3.Zero, 90f, new Vector3(-8f, 0f, 0f), 30f, out var behind);
            SoundCue.TryLocate(SoundCueKind.Footstep, Vector3.Zero, 90f, new Vector3(0f, 0f, 8f), 30f, out var left);

            Assert.That(behind.Direction, Is.EqualTo(SoundCueDirection.Back));
            Assert.That(left.Direction, Is.EqualTo(SoundCueDirection.Left));
        }

        [Test]
        public void ASoundBeyondItsAudibleDistanceIsNotHeard()
        {
            Assert.That(
                SoundCue.TryLocate(SoundCueKind.Footstep, Vector3.Zero, 0f, new Vector3(0f, 0f, 30.5f), 30f, out _),
                Is.False);
            Assert.That(
                SoundCue.TryLocate(SoundCueKind.Footstep, Vector3.Zero, 0f, new Vector3(0f, 0f, 30f), 30f, out _),
                Is.True,
                "the edge is still heard");
        }

        [Test]
        public void AnUnlocatedSoundHasNoDirectionOrDistance()
        {
            Assert.That(SoundCue.TryLocate(SoundCueKind.Reload, Vector3.Zero, 0f, null, 0f, out var cue), Is.True);

            Assert.That(cue.Direction, Is.EqualTo(SoundCueDirection.None));
            Assert.That(float.IsNaN(cue.Distance), Is.True);
            Assert.That(cue.IsFar, Is.False);
        }

        [Test]
        public void ASoundStraightAboveHasNoDirection()
        {
            SoundCue.TryLocate(SoundCueKind.Explosion, Vector3.Zero, 0f, new Vector3(0f, 5f, 0f), 100f, out var cue);

            Assert.That(cue.Direction, Is.EqualTo(SoundCueDirection.None));
            Assert.That(cue.Distance, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void ANonFiniteSourceIsNotHeard()
        {
            Assert.That(
                SoundCue.TryLocate(SoundCueKind.Gunshot, Vector3.Zero, 0f, new Vector3(float.NaN, 0f, 0f), 100f, out _),
                Is.False);
        }

        [Test]
        public void ASoundFarAwayIsFar()
        {
            SoundCue.TryLocate(SoundCueKind.Gunshot, Vector3.Zero, 0f, new Vector3(0f, 0f, SoundCue.FarDistance), 120f, out var edge);
            SoundCue.TryLocate(SoundCueKind.Gunshot, Vector3.Zero, 0f, new Vector3(0f, 0f, SoundCue.FarDistance - 1f), 120f, out var near);

            Assert.That(edge.IsFar, Is.True);
            Assert.That(near.IsFar, Is.False);
        }
    }
}

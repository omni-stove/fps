using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SquadOrderButtonTests
    {
        [Test]
        public void AShortPressIsATapOnRelease()
        {
            var button = new SquadOrderButton(0.5f);

            Assert.That(button.Tick(true, 0f), Is.EqualTo(SquadOrderGesture.None), "nothing is decided on the press");
            Assert.That(button.Tick(true, 0.3f), Is.EqualTo(SquadOrderGesture.None));
            Assert.That(button.IsDown, Is.True);
            Assert.That(button.Tick(false, 0.4f), Is.EqualTo(SquadOrderGesture.Tap));
            Assert.That(button.Tick(false, 0.5f), Is.EqualTo(SquadOrderGesture.None));
        }

        [Test]
        public void HoldingFiresOnceAndTheReleaseIsNotATap()
        {
            var button = new SquadOrderButton(0.5f);

            button.Tick(true, 1f);
            Assert.That(button.Tick(true, 1.49f), Is.EqualTo(SquadOrderGesture.None));
            Assert.That(button.Tick(true, 1.5f), Is.EqualTo(SquadOrderGesture.Hold));
            Assert.That(button.Tick(true, 3f), Is.EqualTo(SquadOrderGesture.None), "only once per press");
            Assert.That(button.Tick(false, 3.1f), Is.EqualTo(SquadOrderGesture.None));

            button.Tick(true, 4f);
            Assert.That(button.Tick(false, 4.1f), Is.EqualTo(SquadOrderGesture.Tap), "the next press starts over");
        }

        [Test]
        public void ResetForgetsThePress()
        {
            var button = new SquadOrderButton(0.5f);
            button.Tick(true, 0f);

            button.Reset();

            Assert.That(button.IsDown, Is.False);
            Assert.That(button.Tick(false, 0.1f), Is.EqualTo(SquadOrderGesture.None));
        }

        [Test]
        public void BadValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadOrderButton(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadOrderButton(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadOrderButton(1f).Tick(true, float.PositiveInfinity));
        }
    }
}

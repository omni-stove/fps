using NUnit.Framework;
using UnityEngine;

namespace Fps.Net.Tests
{
    /// <summary>サーバーが受け取った入力のうち、どの変化を放置の時計を戻す操作とみなすか (<see cref="NetworkPlayerMotor.IsActivity"/>)。</summary>
    public sealed class NetIdleTests
    {
        private static readonly MotorInput _still = new MotorInput(
            new Vector2(0f, 1f), 1.25f, false, false, false, false, false, false, false, false);

        [Test]
        public void TheSameInputAgainIsNotActivity()
        {
            Assert.That(NetworkPlayerMotor.IsActivity(_still, _still), Is.False);
        }

        [Test]
        public void HoldingTheSameMoveIsNotActivity()
        {
            var held = new MotorInput(new Vector2(0f, 1f), 1.25f, false, false, false, false, false, false, false, false);

            Assert.That(NetworkPlayerMotor.IsActivity(_still, held), Is.False);
        }

        [Test]
        public void TurningTheViewIsActivity()
        {
            var turned = new MotorInput(new Vector2(0f, 1f), 1.3f, false, false, false, false, false, false, false, false);

            Assert.That(NetworkPlayerMotor.IsActivity(_still, turned), Is.True);
        }

        [Test]
        public void ChangingTheMoveIsActivity()
        {
            var moved = new MotorInput(new Vector2(1f, 0f), 1.25f, false, false, false, false, false, false, false, false);

            Assert.That(NetworkPlayerMotor.IsActivity(_still, moved), Is.True);
        }

        [TestCase(true, false, false, false, false, false)]
        [TestCase(false, true, false, false, false, false)]
        [TestCase(false, false, true, false, false, false)]
        [TestCase(false, false, false, true, false, false)]
        [TestCase(false, false, false, false, true, false)]
        [TestCase(false, false, false, false, false, true)]
        public void ButtonsAreActivity(bool sprint, bool jump, bool crouch, bool cover, bool aim, bool dodge)
        {
            var pressed = new MotorInput(new Vector2(0f, 1f), 1.25f, sprint, jump, crouch, false, cover, aim, dodge, false);

            Assert.That(NetworkPlayerMotor.IsActivity(_still, pressed), Is.True);
        }

        [Test]
        public void PressingJumpEveryTickStaysActivity()
        {
            var jumping = new MotorInput(new Vector2(0f, 1f), 1.25f, false, true, false, false, false, false, false, false);

            Assert.That(NetworkPlayerMotor.IsActivity(jumping, jumping), Is.True);
        }

        [Test]
        public void ValuesTheServerFillsInAreNotActivity()
        {
            var downed = new MotorInput(new Vector2(0f, 1f), 1.25f, false, false, false, true, false, false, false, true);

            Assert.That(NetworkPlayerMotor.IsActivity(_still, downed), Is.False);
        }
    }
}

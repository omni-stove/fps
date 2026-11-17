using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class FallenViewTests
    {
        private const float _tolerance = 1e-4f;

        // 1 秒で倒れ、足元から 0.2m の目の高さ、左へ 80 度傾き、10 度見下ろす。
        private static FallenView Spec() => new FallenView(1f, 0.2f, 80f, 10f);

        private static readonly Vector3 _deathEye = new Vector3(2f, 1.6f, -3f);

        [Test]
        public void DefaultsAreTheDocumentedValues()
        {
            var view = FallenView.Default;

            Assert.That(view.Seconds, Is.EqualTo(0.8f));
            Assert.That(view.EyeHeight, Is.EqualTo(0.25f));
            Assert.That(view.RollDegrees, Is.EqualTo(75f));
            Assert.That(view.PitchDegrees, Is.EqualTo(-10f));
        }

        [Test]
        public void StartsAtTheDeathEyeAndLooksAsBefore()
        {
            var view = Spec();

            Assert.That(view.Eye(_deathEye, 0f, 0f), Is.EqualTo(_deathEye));
            Assert.That(view.Pitch(-20f, 0f), Is.EqualTo(-20f));
            Assert.That(view.Roll(0f), Is.EqualTo(0f));
            Assert.That(view.IsSettled(0f), Is.False);
        }

        [Test]
        public void DropsSlowlyFirstAndTurnsQuicklyFirst()
        {
            var view = Spec();

            // 半分の時間で、落ちる高さは 0.5^2 = 0.25、傾きは 1 - 0.5^2 = 0.75 進む。
            Assert.That(view.DropProgress(0.5f), Is.EqualTo(0.25f).Within(_tolerance));
            Assert.That(view.TurnProgress(0.5f), Is.EqualTo(0.75f).Within(_tolerance));

            // 1.6 から 0.2 まで 1.4 落ちるうちの 0.25 で 1.25。
            var eye = view.Eye(_deathEye, 0f, 0.5f);
            Assert.That(eye.X, Is.EqualTo(2f));
            Assert.That(eye.Y, Is.EqualTo(1.25f).Within(_tolerance));
            Assert.That(eye.Z, Is.EqualTo(-3f));
            Assert.That(view.Roll(0.5f), Is.EqualTo(60f).Within(_tolerance));
            Assert.That(view.Pitch(-20f, 0.5f), Is.EqualTo(2.5f).Within(_tolerance));
        }

        [Test]
        public void SettlesNearTheGroundAndStays()
        {
            var view = Spec();

            foreach (var elapsed in new[] { 1f, 3f, float.PositiveInfinity })
            {
                var eye = view.Eye(_deathEye, 0.5f, elapsed);
                Assert.That(eye.Y, Is.EqualTo(0.7f).Within(_tolerance), "0.2m above the feet at 0.5");
                Assert.That(view.Roll(elapsed), Is.EqualTo(80f).Within(_tolerance));
                Assert.That(view.Pitch(-20f, elapsed), Is.EqualTo(10f).Within(_tolerance));
                Assert.That(view.IsSettled(elapsed), Is.True);
            }
        }

        [Test]
        public void EyeAlreadyBelowTheRestHeightDoesNotRise()
        {
            var view = Spec();
            var crouched = new Vector3(0f, 0.1f, 0f);

            Assert.That(view.Eye(crouched, 0f, 1f), Is.EqualTo(crouched));
        }

        [Test]
        public void ZeroSecondsFallsAtOnce()
        {
            var view = new FallenView(0f, 0.2f, 80f, 10f);

            Assert.That(view.IsSettled(0f), Is.True);
            Assert.That(view.Eye(_deathEye, 0f, 0f).Y, Is.EqualTo(0.2f).Within(_tolerance));
            Assert.That(view.Roll(0f), Is.EqualTo(80f));
        }

        [Test]
        public void NegativeOrNaNElapsedIsTheStart()
        {
            var view = Spec();

            Assert.That(view.Eye(_deathEye, 0f, -1f), Is.EqualTo(_deathEye));
            Assert.That(view.Roll(float.NaN), Is.EqualTo(0f));
            Assert.That(view.Pitch(float.NaN, 0f), Is.EqualTo(0f), "a broken start pitch is treated as level");
        }

        [Test]
        public void RejectsInvalidSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FallenView(-1f, 0.2f, 80f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FallenView(float.NaN, 0.2f, 80f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FallenView(1f, -0.1f, 80f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FallenView(1f, 0.2f, 91f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FallenView(1f, 0.2f, 80f, -91f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FallenView(1f, 0.2f, float.PositiveInfinity, 10f));
        }
    }
}

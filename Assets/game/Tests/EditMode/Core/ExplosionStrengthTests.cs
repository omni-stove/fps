using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ExplosionStrengthTests
    {
        private static readonly ExplosionDamage _explosion = new ExplosionDamage(100, 6f, 2f);

        [TestCase(0f, 1f)]
        [TestCase(2f, 1f)]
        [TestCase(3f, 0.75f)]
        [TestCase(5f, 0.25f)]
        [TestCase(6f, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        public void FallsOffLikeTheDamage(float distance, float expected)
        {
            Assert.That(_explosion.StrengthAt(distance), Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void IsNotRoundedLikeTheDamage()
        {
            var tiny = new ExplosionDamage(1, 4f, 0f);

            Assert.That(tiny.At(3f, obstructed: false), Is.Zero);
            Assert.That(tiny.StrengthAt(3f), Is.EqualTo(0.25f).Within(1e-5f));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        public void RejectsAnInvalidDistance(float distance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _explosion.StrengthAt(distance));
        }
    }
}

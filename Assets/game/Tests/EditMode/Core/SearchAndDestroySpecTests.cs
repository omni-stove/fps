using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SearchAndDestroySpecTests
    {
        [Test]
        public void TheBombRadiiDefaultToTheValuesTheRunnerUsedBefore()
        {
            var spec = new SearchAndDestroySpec();

            Assert.That(SearchAndDestroySpec.DefaultBombPickupRadius, Is.EqualTo(1.5f));
            Assert.That(SearchAndDestroySpec.DefaultDefuseRadius, Is.EqualTo(2f));
            Assert.That(spec.BombPickupRadius, Is.EqualTo(1.5f));
            Assert.That(spec.DefuseRadius, Is.EqualTo(2f));
        }

        [Test]
        public void TheBombRadiiKeepTheGivenValues()
        {
            var spec = new SearchAndDestroySpec(bombPickupRadius: 0.5f, defuseRadius: 4f);

            Assert.That(spec.BombPickupRadius, Is.EqualTo(0.5f));
            Assert.That(spec.DefuseRadius, Is.EqualTo(4f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ABombPickupRadiusThatIsNotPositiveAndFiniteIsRejected(float radius)
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => new SearchAndDestroySpec(bombPickupRadius: radius));

            Assert.That(error.ParamName, Is.EqualTo("bombPickupRadius"));
        }

        [TestCase(0f)]
        [TestCase(-2f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ADefuseRadiusThatIsNotPositiveAndFiniteIsRejected(float radius)
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => new SearchAndDestroySpec(defuseRadius: radius));

            Assert.That(error.ParamName, Is.EqualTo("defuseRadius"));
        }
    }
}

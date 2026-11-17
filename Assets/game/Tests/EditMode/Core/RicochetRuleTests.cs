using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class RicochetRuleTests
    {
        private static readonly RicochetSpec _twoBounces = new RicochetSpec(2, 20f, 0.5f);

        [Test]
        public void AShallowHitBouncesLikeAMirrorAndScalesTheDamage()
        {
            var direction = Toward(10f);

            Assert.That(RicochetRule.TryBounce(_twoBounces, 0, direction, Vector3.UnitY, out var bounced, out var multiplier), Is.True);

            var expected = Vector3.Normalize(new Vector3(direction.X, -direction.Y, direction.Z));
            Assert.That(Vector3.Distance(bounced, expected), Is.LessThan(1e-5f));
            Assert.That(bounced.Length(), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(multiplier, Is.EqualTo(0.5f));
        }

        [Test]
        public void ADeepHitStops()
        {
            var direction = Toward(30f);

            Assert.That(RicochetRule.TryBounce(_twoBounces, 0, direction, Vector3.UnitY, out var bounced, out var multiplier), Is.False);
            Assert.That(bounced, Is.EqualTo(direction));
            Assert.That(multiplier, Is.EqualTo(1f));
        }

        [TestCase(19.5f, true)]
        [TestCase(20.5f, false)]
        public void TheGrazingAngleIsMeasuredFromTheSurface(float degrees, bool bounces)
        {
            Assert.That(RicochetRule.TryBounce(_twoBounces, 0, Toward(degrees), Vector3.UnitY, out _, out _), Is.EqualTo(bounces));
        }

        [Test]
        public void TheBounceCountStopsTheRound()
        {
            var direction = Toward(10f);

            Assert.That(RicochetRule.TryBounce(_twoBounces, 1, direction, Vector3.UnitY, out _, out _), Is.True);
            Assert.That(RicochetRule.TryBounce(_twoBounces, 2, direction, Vector3.UnitY, out var bounced, out var multiplier), Is.False);
            Assert.That(bounced, Is.EqualTo(direction));
            Assert.That(multiplier, Is.EqualTo(1f));
        }

        [Test]
        public void ARoundWithoutRicochetNeverBounces()
        {
            Assert.That(RicochetRule.TryBounce(null, 0, Toward(1f), Vector3.UnitY, out _, out var multiplier), Is.False);
            Assert.That(multiplier, Is.EqualTo(1f));
        }

        [Test]
        public void ARoundLeavingTheBackOfTheSurfaceDoesNotBounce()
        {
            var leaving = new Vector3(Toward(10f).X, -Toward(10f).Y, 0f);

            Assert.That(RicochetRule.TryBounce(_twoBounces, 0, leaving, Vector3.UnitY, out _, out _), Is.False);
            Assert.That(RicochetRule.TryBounce(_twoBounces, 0, Vector3.UnitX, Vector3.UnitY, out _, out _), Is.False);
        }

        [Test]
        public void TheLengthsOfTheVectorsDoNotMatter()
        {
            var direction = Toward(10f);

            Assert.That(RicochetRule.TryBounce(_twoBounces, 0, direction * 7f, Vector3.UnitY * 0.2f, out var bounced, out _), Is.True);
            Assert.That(RicochetRule.TryBounce(_twoBounces, 0, direction, Vector3.UnitY, out var unit, out _), Is.True);
            Assert.That(Vector3.Distance(bounced, unit), Is.LessThan(1e-5f));
        }

        [Test]
        public void BadArgumentsThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RicochetRule.TryBounce(_twoBounces, -1, Toward(10f), Vector3.UnitY, out _, out _));
            Assert.Throws<ArgumentException>(() => RicochetRule.TryBounce(_twoBounces, 0, Vector3.Zero, Vector3.UnitY, out _, out _));
            Assert.Throws<ArgumentException>(() => RicochetRule.TryBounce(_twoBounces, 0, Toward(10f), Vector3.Zero, out _, out _));
            Assert.Throws<ArgumentException>(
                () => RicochetRule.TryBounce(_twoBounces, 0, new Vector3(float.NaN, 0f, 1f), Vector3.UnitY, out _, out _));
        }

        private static Vector3 Toward(float degrees)
        {
            var radians = degrees * (MathF.PI / 180f);
            return new Vector3(MathF.Cos(radians), -MathF.Sin(radians), 0f);
        }
    }
}

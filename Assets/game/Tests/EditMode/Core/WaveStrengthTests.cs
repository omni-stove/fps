using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WaveStrengthTests
    {
        // 視認 20m、攻撃 12m、見失って 2 秒、Search 6 秒、反応 1 秒、散らばり 5 度から 3 発で 1 度へ。
        private static EnemySpec Spec() => new EnemySpec(20f, 12f, 2f, 6f, 1f, 5f, 1f, 3, 1.5f, 2f, 0.5f);

        [Test]
        public void OneKeepsTheSpecAsIs()
        {
            var spec = WaveStrength.Apply(Spec(), 1f);

            Assert.That(spec.ReactionSeconds, Is.EqualTo(1f));
            Assert.That(spec.OpeningSpreadDegrees, Is.EqualTo(5f));
            Assert.That(spec.SettledSpreadDegrees, Is.EqualTo(1f));
            Assert.That(WaveStrength.Health(100, 1f), Is.EqualTo(100));
        }

        [Test]
        public void StrongerReactsFasterAndAimsTighter()
        {
            var spec = WaveStrength.Apply(Spec(), 2f);

            Assert.That(spec.ReactionSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.OpeningSpreadDegrees, Is.EqualTo(2.5f));
            Assert.That(spec.SettledSpreadDegrees, Is.EqualTo(0.5f));
        }

        [Test]
        public void RangesAndCoverBehaviourDoNotChange()
        {
            var original = Spec();
            var spec = WaveStrength.Apply(original, 3f);

            Assert.That(spec.SightRange, Is.EqualTo(original.SightRange));
            Assert.That(spec.AttackRange, Is.EqualTo(original.AttackRange));
            Assert.That(spec.LoseSightSeconds, Is.EqualTo(original.LoseSightSeconds));
            Assert.That(spec.SearchSeconds, Is.EqualTo(original.SearchSeconds));
            Assert.That(spec.OpeningShots, Is.EqualTo(original.OpeningShots));
            Assert.That(spec.CoverHideSeconds, Is.EqualTo(original.CoverHideSeconds));
            Assert.That(spec.CoverPeekSeconds, Is.EqualTo(original.CoverPeekSeconds));
            Assert.That(spec.CoverHealthFraction, Is.EqualTo(original.CoverHealthFraction));
        }

        [Test]
        public void WeakerSpreadIsCappedAtTheMaximum()
        {
            var spec = WaveStrength.Apply(Spec(), 0.01f);

            Assert.That(spec.OpeningSpreadDegrees, Is.EqualTo(EnemySpec.MaxSpreadDegrees));
            Assert.That(spec.ReactionSeconds, Is.EqualTo(100f).Within(1e-3f));
        }

        [Test]
        public void TinyMultiplierKeepsTheReactionFinite()
        {
            var spec = WaveStrength.Apply(Spec(), float.Epsilon);

            Assert.That(float.IsInfinity(spec.ReactionSeconds), Is.False);
        }

        [Test]
        public void HealthScalesAndRoundsHalfAwayFromZero()
        {
            Assert.That(WaveStrength.Health(100, 1.25f), Is.EqualTo(125));
            Assert.That(WaveStrength.Health(3, 1.5f), Is.EqualTo(5), "4.5 rounds up");
            Assert.That(WaveStrength.Health(100, 0.001f), Is.EqualTo(1), "never below 1");
            Assert.That(WaveStrength.Health(int.MaxValue, 2f), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => WaveStrength.Apply(null, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaveStrength.Apply(Spec(), 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaveStrength.Apply(Spec(), float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaveStrength.Apply(Spec(), float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaveStrength.Health(0, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaveStrength.Health(100, -1f));
        }
    }
}

using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EnemySpecDefaultTests
    {
        [Test]
        public void DefaultHoldsTheDefaultValues()
        {
            var spec = EnemySpec.Default;

            Assert.That(spec.SightRange, Is.EqualTo(EnemySpec.DefaultSightRange));
            Assert.That(spec.AttackRange, Is.EqualTo(EnemySpec.DefaultAttackRange));
            Assert.That(spec.LoseSightSeconds, Is.EqualTo(EnemySpec.DefaultLoseSightSeconds));
            Assert.That(spec.SearchSeconds, Is.EqualTo(EnemySpec.DefaultSearchSeconds));
            Assert.That(spec.ReactionSeconds, Is.EqualTo(EnemySpec.DefaultReactionSeconds));
            Assert.That(spec.OpeningSpreadDegrees, Is.EqualTo(EnemySpec.DefaultOpeningSpreadDegrees));
            Assert.That(spec.SettledSpreadDegrees, Is.EqualTo(EnemySpec.DefaultSettledSpreadDegrees));
            Assert.That(spec.OpeningShots, Is.EqualTo(EnemySpec.DefaultOpeningShots));
            Assert.That(spec.CoverHideSeconds, Is.EqualTo(EnemySpec.DefaultCoverHideSeconds));
            Assert.That(spec.CoverPeekSeconds, Is.EqualTo(EnemySpec.DefaultCoverPeekSeconds));
            Assert.That(spec.CoverHealthFraction, Is.EqualTo(EnemySpec.DefaultCoverHealthFraction));
            Assert.That(spec.StaggerResistance, Is.EqualTo(EnemySpec.DefaultStaggerResistance));
            Assert.That(spec.Archetype, Is.EqualTo(EnemyArchetype.Rifleman));
            Assert.That(spec.Shield, Is.SameAs(ShieldSpec.Default));
            Assert.That(spec.Sniper, Is.SameAs(SniperSpec.Default));
            Assert.That(spec.Charger, Is.SameAs(ChargerSpec.Default));
            Assert.That(spec.Bomber, Is.SameAs(BomberSpec.Default));
        }
    }
}

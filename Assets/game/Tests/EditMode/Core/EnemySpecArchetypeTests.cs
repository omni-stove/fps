using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EnemySpecArchetypeTests
    {
        [Test]
        public void DefaultsToRifleman()
        {
            Assert.That(new EnemySpec(20f, 10f, 1f, 2f, 0.5f).Archetype, Is.EqualTo(EnemyArchetype.Rifleman));
        }

        [Test]
        public void KeepsTheGivenArchetype()
        {
            var spec = new EnemySpec(20f, 10f, 1f, 2f, 0.5f, archetype: EnemyArchetype.Rifleman);

            Assert.That(spec.Archetype, Is.EqualTo(EnemyArchetype.Rifleman));
        }

        [Test]
        public void KeepsTheShieldAndSniperArchetypes()
        {
            Assert.That(new EnemySpec(20f, 10f, 1f, 2f, 0.5f, archetype: EnemyArchetype.Shield).Archetype, Is.EqualTo(EnemyArchetype.Shield));
            Assert.That(new EnemySpec(20f, 10f, 1f, 2f, 0.5f, archetype: EnemyArchetype.Sniper).Archetype, Is.EqualTo(EnemyArchetype.Sniper));
        }

        [Test]
        public void RejectsAnUndefinedArchetype()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _ = new EnemySpec(20f, 10f, 1f, 2f, 0.5f, archetype: (EnemyArchetype)99));
        }
    }
}

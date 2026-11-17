using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class FireAreaTests
    {
        [Test]
        public void AnAreaThatHurtsBurnsWithItsOwnIntervalAndDamage()
        {
            var area = new AreaEffectSpec(4f, 8f, 0.5f, 5, visualId: "fire");

            var burn = FireArea.Affliction(area);

            Assert.That(burn, Is.Not.Null);
            Assert.That(burn.KindId, Is.EqualTo(StatusEffectKinds.Burn));
            Assert.That(burn.TickSeconds, Is.EqualTo(0.5f));
            Assert.That(burn.DamagePerTick, Is.EqualTo(5));
            Assert.That(burn.DurationSeconds, Is.EqualTo(FireArea.DefaultBurnSeconds));
            Assert.That(FireArea.Affliction(area), Is.SameAs(burn), "the same area reuses its burn");
            Assert.That(FireArea.IsFire(area), Is.True);
        }

        [Test]
        public void AWrittenStatusEffectIsUsedAsIs()
        {
            var written = new StatusEffectSpec(6f, 1f, 3);
            var area = new AreaEffectSpec(4f, 8f, 0.5f, 5, written, "fire");

            Assert.That(FireArea.Affliction(area), Is.SameAs(written));
        }

        [Test]
        public void SmokeDoesNotBurn()
        {
            var smoke = new AreaEffectSpec(5f, 15f, 0.5f, 0, visualId: "smoke");

            Assert.That(FireArea.Affliction(smoke), Is.Null);
            Assert.That(FireArea.IsFire(smoke), Is.False);
            Assert.That(FireArea.Affliction(null), Is.Null);
        }

        [Test]
        public void AnotherKindOfStatusIsNotFire()
        {
            var gas = new AreaEffectSpec(4f, 8f, 0.5f, 0, new StatusEffectSpec(2f, 0.5f, 2, kindId: "poison"));

            Assert.That(FireArea.Affliction(gas), Is.Not.Null);
            Assert.That(FireArea.IsFire(gas), Is.False);
        }

        [Test]
        public void AnIntervalShorterThanAStatusAllowsStillBurns()
        {
            var area = new AreaEffectSpec(4f, 8f, StatusEffectSpec.MinTickSeconds / 2f, 1);

            Assert.That(FireArea.Affliction(area).TickSeconds, Is.EqualTo(StatusEffectSpec.MinTickSeconds));
        }

        [Test]
        public void ItPulsesRightAwayAndThenOncePerInterval()
        {
            var area = new AreaEffectSpec(4f, 2f, 0.5f, 5);
            var pulses = new List<float>();
            var elapsed = 0f;
            const float step = 1f / 60f;
            while (elapsed < area.DurationSeconds)
            {
                if (FireArea.Pulses(area, elapsed, step))
                {
                    pulses.Add(elapsed);
                }

                elapsed += step;
            }

            Assert.That(pulses.Count, Is.EqualTo(4), "at 0, 0.5, 1 and 1.5 seconds");
            Assert.That(pulses[0], Is.Zero);
        }

        [Test]
        public void OneLongStepPulsesOnce()
        {
            var area = new AreaEffectSpec(4f, 8f, 0.5f, 5);

            Assert.That(FireArea.Pulses(area, 1f, 3f), Is.True);
            Assert.That(FireArea.Pulses(area, 0.1f, 0.1f), Is.False);
        }
    }
}

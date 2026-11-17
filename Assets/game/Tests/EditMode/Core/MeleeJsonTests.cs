using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MeleeJsonTests
    {
        private static readonly string[] _expectedItemNames =
        {
            "damage", "reach", "arcDegrees", "swingSeconds", "cooldownSeconds", "impactSeconds", "lungeDistance",
            "finisherSeconds", "finisherHealth", "finisherAmmo", "finisherInterruptible",
            "takedownSeconds", "takedownInterruptible", "takedownWitnessAwareness",
            "alignSeconds", "standDistance", "behindArcDegrees",
        };

        [Test]
        public void TheItemNamesFollowTheMeleeSpecConstructor()
        {
            Assert.That(MeleeJson.ItemNames, Is.EqualTo(_expectedItemNames));
        }

        [Test]
        public void AnEmptyObjectGivesTheDefaults()
        {
            var spec = MeleeJson.Parse("{}", "melee.json");

            Assert.That(spec.Damage, Is.EqualTo(50));
            Assert.That(spec.Reach, Is.EqualTo(2f));
            Assert.That(spec.ArcDegrees, Is.EqualTo(90f));
            Assert.That(spec.SwingSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.CooldownSeconds, Is.EqualTo(0.4f));
            Assert.That(spec.ImpactSeconds, Is.EqualTo(0.2f));
            Assert.That(spec.LungeDistance, Is.EqualTo(0.75f));
            Assert.That(spec.FinisherSeconds, Is.EqualTo(1.2f));
            Assert.That(spec.FinisherHealth, Is.EqualTo(25));
            Assert.That(spec.FinisherAmmo, Is.EqualTo(30));
            Assert.That(spec.FinisherInterruptible, Is.True);
            Assert.That(spec.TakedownSeconds, Is.EqualTo(1.5f));
            Assert.That(spec.TakedownInterruptible, Is.True);
            Assert.That(spec.TakedownWitnessAwareness, Is.EqualTo(1f));
            Assert.That(spec.AlignSeconds, Is.EqualTo(0.15f));
            Assert.That(spec.StandDistance, Is.EqualTo(1.2f));
            Assert.That(spec.BehindArcDegrees, Is.EqualTo(90f));
        }

        [Test]
        public void TheNewSpecValuesDefaultToTheFormerConstants()
        {
            Assert.That(MeleeSpec.Default.AlignSeconds, Is.EqualTo(0.15f));
            Assert.That(MeleeSpec.Default.StandDistance, Is.EqualTo(1.2f));
            Assert.That(MeleeSpec.Default.BehindArcDegrees, Is.EqualTo(ContextMeleeSelector.DefaultBehindArcDegrees));
        }

        [Test]
        public void ReadsEveryItem()
        {
            var spec = MeleeJson.Parse(
                "{\"damage\": 100, \"reach\": 3, \"arcDegrees\": 120, \"swingSeconds\": 0.8, \"cooldownSeconds\": 1,"
                + " \"impactSeconds\": 0.3, \"lungeDistance\": 1.5, \"finisherSeconds\": 2, \"finisherHealth\": 10,"
                + " \"finisherAmmo\": 5, \"finisherInterruptible\": false, \"takedownSeconds\": 2.5,"
                + " \"takedownInterruptible\": false, \"takedownWitnessAwareness\": 0.5,"
                + " \"alignSeconds\": 0.3, \"standDistance\": 1.5, \"behindArcDegrees\": 120}",
                "melee.json");

            Assert.That(spec.Damage, Is.EqualTo(100));
            Assert.That(spec.Reach, Is.EqualTo(3f));
            Assert.That(spec.ArcDegrees, Is.EqualTo(120f));
            Assert.That(spec.SwingSeconds, Is.EqualTo(0.8f));
            Assert.That(spec.CooldownSeconds, Is.EqualTo(1f));
            Assert.That(spec.ImpactSeconds, Is.EqualTo(0.3f));
            Assert.That(spec.LungeDistance, Is.EqualTo(1.5f));
            Assert.That(spec.FinisherSeconds, Is.EqualTo(2f));
            Assert.That(spec.FinisherHealth, Is.EqualTo(10));
            Assert.That(spec.FinisherAmmo, Is.EqualTo(5));
            Assert.That(spec.FinisherInterruptible, Is.False);
            Assert.That(spec.TakedownSeconds, Is.EqualTo(2.5f));
            Assert.That(spec.TakedownInterruptible, Is.False);
            Assert.That(spec.TakedownWitnessAwareness, Is.EqualTo(0.5f));
            Assert.That(spec.AlignSeconds, Is.EqualTo(0.3f));
            Assert.That(spec.StandDistance, Is.EqualTo(1.5f));
            Assert.That(spec.BehindArcDegrees, Is.EqualTo(120f));
        }

        [Test]
        public void AnOmittedItemKeepsItsDefault()
        {
            var spec = MeleeJson.Parse("{\"damage\": 100}", "melee.json");

            Assert.That(spec.Damage, Is.EqualTo(100));
            Assert.That(spec.Reach, Is.EqualTo(MeleeSpec.DefaultReach));
            Assert.That(spec.StandDistance, Is.EqualTo(MeleeSpec.DefaultStandDistance));
        }

        [Test]
        public void TheMovesTakeTheirAlignAndStandFromTheSpec()
        {
            var spec = MeleeJson.Parse("{\"alignSeconds\": 0.4, \"standDistance\": 1.6}", "melee.json");

            var finisher = ContextMeleeMove.Finisher(spec);
            var takedown = ContextMeleeMove.Takedown(spec);

            Assert.That(finisher.AlignSeconds, Is.EqualTo(0.4f));
            Assert.That(finisher.StandDistance, Is.EqualTo(1.6f));
            Assert.That(takedown.AlignSeconds, Is.EqualTo(0.4f));
            Assert.That(takedown.StandDistance, Is.EqualTo(1.6f));
            Assert.That(takedown.Priority, Is.EqualTo(ContextMeleeMove.TakedownPriority));
        }

        [Test]
        public void AnInvalidValueNamesTheItem()
        {
            var e = Assert.Throws<TuningJsonException>(() => MeleeJson.Parse("{\"behindArcDegrees\": 0}", "melee.json"));

            Assert.That(e.SourceName, Is.EqualTo("melee.json"));
            Assert.That(e.Item, Is.EqualTo("behindArcDegrees"));
            Assert.That(e.Message, Does.StartWith("melee JSON 'melee.json' item 'behindArcDegrees' is invalid: "));
            Assert.That(e.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ImpactLongerThanTheSwingNamesTheImpactItem()
        {
            var e = Assert.Throws<TuningJsonException>(() => MeleeJson.Parse("{\"impactSeconds\": 0.6}", "melee.json"));

            Assert.That(e.Item, Is.EqualTo("impactSeconds"));
        }

        [Test]
        public void ANegativeStandDistanceNamesTheItem()
        {
            Assert.That(
                Assert.Throws<TuningJsonException>(() => MeleeJson.Parse("{\"standDistance\": -1}", "m")).Item,
                Is.EqualTo("standDistance"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => MeleeJson.Parse("{\"alignSeconds\": -0.1}", "m")).Item,
                Is.EqualTo("alignSeconds"));
        }

        [Test]
        public void AnUnknownItemIsRejectedWithTheKnownNames()
        {
            var e = Assert.Throws<TuningJsonException>(() => MeleeJson.Parse("{\"dmg\": 10}", "melee.json"));

            Assert.That(e.Item, Is.EqualTo("dmg"));
            Assert.That(e.Message, Does.Contain("unknown item 'dmg'"));
            Assert.That(e.Message, Does.Contain("behindArcDegrees"));
        }

        [Test]
        public void AWrongTypeNamesTheItem()
        {
            Assert.That(
                Assert.Throws<TuningJsonException>(() => MeleeJson.Parse("{\"damage\": 12.5}", "m")).Item,
                Is.EqualTo("damage"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => MeleeJson.Parse("{\"finisherInterruptible\": 1}", "m")).Item,
                Is.EqualTo("finisherInterruptible"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => MeleeJson.Parse("{\"reach\": \"far\"}", "m")).Item,
                Is.EqualTo("reach"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => MeleeJson.Parse("{\"reach\": null}", "m")).Item,
                Is.EqualTo("reach"));
        }

        [Test]
        public void BrokenJsonIsRejectedWithoutAnItem()
        {
            var e = Assert.Throws<TuningJsonException>(() => MeleeJson.Parse("{", null));

            Assert.That(e.Item, Is.Null);
            Assert.That(e.SourceName, Is.EqualTo("(unnamed melee JSON)"));
            Assert.Throws<ArgumentNullException>(() => MeleeJson.Parse(null, "m"));
        }

        [Test]
        public void TheSpecRejectsOutOfRangeNewValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Spec(alignSeconds: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => Spec(standDistance: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => Spec(behindArcDegrees: 361f));
            Assert.That(Spec(alignSeconds: 0f, standDistance: 0f, behindArcDegrees: 360f).BehindArcDegrees, Is.EqualTo(360f));
        }

        private static MeleeSpec Spec(float alignSeconds = 0.15f, float standDistance = 1.2f, float behindArcDegrees = 90f)
        {
            return new MeleeSpec(
                50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, alignSeconds: alignSeconds, standDistance: standDistance, behindArcDegrees: behindArcDegrees);
        }
    }
}

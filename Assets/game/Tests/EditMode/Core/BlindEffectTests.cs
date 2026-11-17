using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BlindEffectTests
    {
        private static readonly BlindSpec _spec = new BlindSpec(10f, 4f, 0.25f);
        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);
        private static readonly Vector3 _flash = _eye + new Vector3(0f, 0f, 5f);

        [Test]
        public void LookingStraightAtTheFlashBlindsForTheDurationLessByDistance()
        {
            Assert.That(BlindEffect.Seconds(_spec, _flash, _eye, Vector3.UnitZ), Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void TurningAwayShortensItDownToTheBackFraction()
        {
            var toward = BlindEffect.Seconds(_spec, _flash, _eye, Vector3.UnitZ);
            var side = BlindEffect.Seconds(_spec, _flash, _eye, Vector3.UnitX);
            var away = BlindEffect.Seconds(_spec, _flash, _eye, -Vector3.UnitZ);

            Assert.That(side, Is.LessThan(toward));
            Assert.That(away, Is.LessThan(side));
            Assert.That(away, Is.EqualTo(toward * _spec.BackFraction).Within(1e-4f));
            Assert.That(side, Is.EqualTo(toward * (_spec.BackFraction + 1f) / 2f).Within(1e-4f));
        }

        [Test]
        public void FartherFlashesBlindLessAndNoneAtTheRadius()
        {
            var near = BlindEffect.Seconds(_spec, _eye + new Vector3(0f, 0f, 2f), _eye, Vector3.UnitZ);
            var far = BlindEffect.Seconds(_spec, _eye + new Vector3(0f, 0f, 8f), _eye, Vector3.UnitZ);

            Assert.That(far, Is.LessThan(near));
            Assert.That(BlindEffect.Seconds(_spec, _eye + new Vector3(0f, 0f, _spec.Radius), _eye, Vector3.UnitZ), Is.Zero);
            Assert.That(BlindEffect.Seconds(_spec, _eye + new Vector3(0f, 0f, 50f), _eye, Vector3.UnitZ), Is.Zero);
        }

        [Test]
        public void AFlashInTheEyesOrAnUnknownFacingCountsAsLookingAtIt()
        {
            Assert.That(BlindEffect.Seconds(_spec, _eye, _eye, -Vector3.UnitZ), Is.EqualTo(_spec.DurationSeconds).Within(1e-4f));
            Assert.That(BlindEffect.Seconds(_spec, _flash, _eye, Vector3.Zero), Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void TheFacingNeedNotBeNormalized()
        {
            Assert.That(
                BlindEffect.Seconds(_spec, _flash, _eye, Vector3.UnitZ * 7f),
                Is.EqualTo(BlindEffect.Seconds(_spec, _flash, _eye, Vector3.UnitZ)).Within(1e-4f));
        }

        [Test]
        public void TheStatusEffectIsAHarmlessBlindOfThatLength()
        {
            var effect = BlindEffect.StatusEffect(1.5f);

            Assert.That(effect.KindId, Is.EqualTo(StatusEffectKinds.Blind));
            Assert.That(effect.DurationSeconds, Is.EqualTo(1.5f));
            Assert.That(effect.DamagePerTick, Is.Zero);

            var effects = new StatusEffects();
            var ticks = new System.Collections.Generic.List<StatusEffectTick>();
            var ended = new System.Collections.Generic.List<string>();
            effects.Afflict(effect, 1, "grenade");
            effects.Tick(1.6f, ticks, ended);
            Assert.That(ticks, Is.Empty);
            Assert.That(ended, Is.EqualTo(new[] { StatusEffectKinds.Blind }));
        }

        [Test]
        public void BadValuesAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => BlindEffect.Seconds(null, _flash, _eye, Vector3.UnitZ));
            Assert.Throws<ArgumentOutOfRangeException>(() => BlindEffect.StatusEffect(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BlindSpec(0f, 4f, 0.25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BlindSpec(10f, -1f, 0.25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BlindSpec(10f, 4f, 1.5f));
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StatusEffectsTests
    {
        private const string _weaponId = "78afe32d-bf3f-459c-98b8-8c238290913f";

        private readonly List<StatusEffectTick> _ticks = new List<StatusEffectTick>();
        private readonly List<string> _ended = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _ticks.Clear();
            _ended.Clear();
        }

        [Test]
        public void ItDealsTheDamageOncePerTickForTheWholeDuration()
        {
            var effects = new StatusEffects();
            effects.Afflict(new StatusEffectSpec(3f, 1f, 7), 2, _weaponId);

            Step(effects, 0.9f, 1f / 60f);
            Assert.That(_ticks, Is.Empty, "the first damage comes after one interval");

            Step(effects, 3f, 1f / 60f);

            Assert.That(_ticks, Has.Count.EqualTo(3));
            foreach (var tick in _ticks)
            {
                Assert.That(tick.Damage, Is.EqualTo(7));
                Assert.That(tick.KindId, Is.EqualTo(StatusEffectKinds.Burn));
                Assert.That(tick.SourceId, Is.EqualTo(2));
                Assert.That(tick.WeaponId, Is.EqualTo(_weaponId));
            }
        }

        [Test]
        public void AnIntervalTooShortToAdvanceIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatusEffectSpec(1f, 5e-10f, 1));
            Assert.That(new StatusEffectSpec(1f, StatusEffectSpec.MinTickSeconds, 1).TickSeconds, Is.EqualTo(StatusEffectSpec.MinTickSeconds));
        }

        [Test]
        public void TheShortestIntervalStillFinishesTheStep()
        {
            var effects = new StatusEffects();
            effects.Afflict(new StatusEffectSpec(1f, StatusEffectSpec.MinTickSeconds, 1), null, null);

            effects.Tick(0.05f, _ticks, _ended);

            Assert.That(_ticks.Count, Is.InRange(4, 5));
            Assert.That(effects.Has(StatusEffectKinds.Burn), Is.True);
        }

        [Test]
        public void ItEndsWhenTheTimeRunsOut()
        {
            var effects = new StatusEffects();
            effects.Afflict(new StatusEffectSpec(2f, 0.5f, 1), null, null);

            effects.Tick(1.5f, _ticks, _ended);
            Assert.That(effects.Has(StatusEffectKinds.Burn), Is.True);
            Assert.That(effects.RemainingSeconds(StatusEffectKinds.Burn), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(_ended, Is.Empty);

            effects.Tick(1f, _ticks, _ended);

            Assert.That(effects.Has(StatusEffectKinds.Burn), Is.False);
            Assert.That(effects.Count, Is.Zero);
            Assert.That(_ended, Is.EqualTo(new[] { StatusEffectKinds.Burn }));
            Assert.That(_ticks, Has.Count.EqualTo(4), "a long step past the end still deals only the ticks inside the duration");
        }

        [Test]
        public void AfflictingTheSameKindAgainExtendsItInsteadOfStacking()
        {
            var effects = new StatusEffects();
            effects.Afflict(new StatusEffectSpec(3f, 1f, 5), 1, _weaponId);
            effects.Tick(2.5f, _ticks, _ended);

            var again = effects.Afflict(new StatusEffectSpec(3f, 1f, 5), 1, _weaponId);

            Assert.That(effects.Count, Is.EqualTo(1));
            Assert.That(again.RemainingSeconds, Is.EqualTo(3f));
            _ticks.Clear();
            effects.Tick(0.5f, _ticks, _ended);
            Assert.That(_ticks, Has.Count.EqualTo(1), "the time toward the next tick carries over, so repeated hits keep it burning");
        }

        [Test]
        public void ExtendingNeverShortensALongerBurn()
        {
            var effects = new StatusEffects();
            effects.Afflict(new StatusEffectSpec(10f, 1f, 1), 1, _weaponId);

            effects.Afflict(new StatusEffectSpec(2f, 1f, 1), 1, _weaponId);

            Assert.That(effects.RemainingSeconds(StatusEffectKinds.Burn), Is.EqualTo(10f));
        }

        [Test]
        public void TheLatestAfflictionTakesTheCreditAndTheDamage()
        {
            var effects = new StatusEffects();
            effects.Afflict(new StatusEffectSpec(3f, 1f, 2), 1, "a");
            effects.Afflict(new StatusEffectSpec(3f, 1f, 9), 4, "b");

            effects.Tick(1f, _ticks, _ended);

            Assert.That(_ticks, Has.Count.EqualTo(1));
            Assert.That(_ticks[0].SourceId, Is.EqualTo(4));
            Assert.That(_ticks[0].WeaponId, Is.EqualTo("b"));
            Assert.That(_ticks[0].Damage, Is.EqualTo(9));
        }

        [Test]
        public void DifferentKindsRunSideBySide()
        {
            var effects = new StatusEffects();
            effects.Afflict(new StatusEffectSpec(1f, 1f, 1), 1, _weaponId);
            effects.Afflict(new StatusEffectSpec(3f, 1f, 0, kindId: "blind"), 1, _weaponId);

            effects.Tick(1f, _ticks, _ended);

            Assert.That(_ended, Is.EqualTo(new[] { StatusEffectKinds.Burn }));
            Assert.That(effects.Has("blind"), Is.True);
            Assert.That(_ticks, Has.Count.EqualTo(1), "a kind without damage deals none");
        }

        [Test]
        public void ClearingEndsEveryKind()
        {
            var effects = new StatusEffects();
            effects.Afflict(new StatusEffectSpec(3f, 1f, 1), 1, _weaponId);
            effects.Afflict(new StatusEffectSpec(3f, 1f, 1, kindId: "blind"), 1, _weaponId);

            effects.Clear(_ended);

            Assert.That(effects.Count, Is.Zero);
            Assert.That(_ended, Is.EquivalentTo(new[] { StatusEffectKinds.Burn, "blind" }));
        }

        [Test]
        public void AnEmptyKindIsBurn()
        {
            Assert.That(new StatusEffectSpec(1f, 1f, 1).KindId, Is.EqualTo(StatusEffectKinds.Burn));
            Assert.That(new StatusEffectSpec(1f, 1f, 1, kindId: "  ").KindId, Is.EqualTo(StatusEffectKinds.Burn));
            Assert.That(new StatusEffectSpec(1f, 1f, 1, kindId: " blind ").KindId, Is.EqualTo("blind"));
        }

        private void Step(StatusEffects effects, float seconds, float step)
        {
            for (var elapsed = 0f; elapsed < seconds - 1e-6f; elapsed += step)
            {
                effects.Tick(step, _ticks, _ended);
            }
        }
    }
}

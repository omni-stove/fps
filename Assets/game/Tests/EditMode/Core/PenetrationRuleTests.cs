using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PenetrationRuleTests
    {
        private static readonly PenetrationSpec _round = new PenetrationSpec(
            2,
            0.5f,
            0.3f,
            new[] { new PenetrationMaterialSpec("metal", 0f), new PenetrationMaterialSpec("wood", 2f) });

        [Test]
        public void AThinEnoughSurfaceIsPassedWithTheMultiplierForThatPass()
        {
            Assert.That(PenetrationRule.TryPassThrough(_round, 0, "concrete", 0.3f, out var multiplier), Is.True);
            Assert.That(multiplier, Is.EqualTo(0.5f));
            Assert.That(PenetrationRule.TryPassThrough(_round, 1, "concrete", 0f, out multiplier), Is.True);
            Assert.That(multiplier, Is.EqualTo(0.5f));
        }

        [Test]
        public void TheRoundStopsAfterItsMaxPenetrations()
        {
            Assert.That(PenetrationRule.TryPassThrough(_round, 2, "concrete", 0f, out var multiplier), Is.False);
            Assert.That(multiplier, Is.EqualTo(1f));
        }

        [Test]
        public void ASurfaceThickerThanTheMaxStopsTheRound()
        {
            Assert.That(PenetrationRule.TryPassThrough(_round, 0, "concrete", 0.31f, out _), Is.False);
            Assert.That(PenetrationRule.TryPassThrough(_round, 0, "concrete", float.PositiveInfinity, out _), Is.False);
        }

        [Test]
        public void EachMaterialScalesTheThicknessTheRoundGoesThrough()
        {
            Assert.That(PenetrationRule.MaxThicknessFor(_round, "wood"), Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(PenetrationRule.MaxThicknessFor(_round, "concrete"), Is.EqualTo(0.3f));
            Assert.That(PenetrationRule.TryPassThrough(_round, 0, "wood", 0.6f, out _), Is.True);
            Assert.That(PenetrationRule.TryPassThrough(_round, 0, "wood", 0.61f, out _), Is.False);
        }

        [Test]
        public void AMaterialThatCannotBePenetratedStopsTheRoundHoweverThin()
        {
            Assert.That(PenetrationRule.MaxThicknessFor(_round, "metal"), Is.Zero);
            Assert.That(PenetrationRule.TryPassThrough(_round, 0, "metal", 0f, out var multiplier), Is.False);
            Assert.That(multiplier, Is.EqualTo(1f));
        }

        [Test]
        public void TheMultipliersCompoundToCutTheDamageOverPasses()
        {
            var damage = 100f;
            for (var passed = 0; PenetrationRule.TryPassThrough(_round, passed, "default", 0.1f, out var multiplier); passed++)
            {
                damage *= multiplier;
            }

            Assert.That(damage, Is.EqualTo(25f));
        }

        [Test]
        public void ARoundWithoutPenetrationStopsAtTheFirstHit()
        {
            Assert.That(PenetrationRule.TryPassThrough(null, 0, "default", 0f, out var multiplier), Is.False);
            Assert.That(multiplier, Is.EqualTo(1f));
            Assert.That(PenetrationRule.MaxThicknessFor(null, "default"), Is.Zero);
        }

        [Test]
        public void OnlyAThinRoundPenetrates()
        {
            Assert.That(PenetrationRule.For(new RoundSpec(penetration: _round)), Is.SameAs(_round));
            Assert.That(PenetrationRule.For(new RoundSpec(radius: 0.2f, penetration: _round)), Is.Null);
            Assert.That(PenetrationRule.For(RoundSpec.Default), Is.Null);
            Assert.Throws<ArgumentNullException>(() => PenetrationRule.For(null));
        }

        [Test]
        public void RejectsANegativeCountOrThickness()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PenetrationRule.TryPassThrough(_round, -1, "default", 0f, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => PenetrationRule.TryPassThrough(_round, 0, "default", -0.1f, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => PenetrationRule.TryPassThrough(_round, 0, "default", float.NaN, out _));
        }

        [Test]
        public void TheMaterialsAreCheckedWhenTheSpecIsMade()
        {
            Assert.That(new PenetrationSpec(1, 0.5f, 0.3f).Materials, Is.Empty);
            Assert.That(new PenetrationMaterialSpec(" wood ", 1f).MaterialId, Is.EqualTo("wood"));
            Assert.Throws<ArgumentException>(() => new PenetrationMaterialSpec(" ", 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PenetrationMaterialSpec("wood", -1f));
            Assert.Throws<ArgumentException>(() => new PenetrationSpec(1, 0.5f, 0.3f, new PenetrationMaterialSpec[] { null }));
            Assert.Throws<ArgumentException>(
                () => new PenetrationSpec(1, 0.5f, 0.3f, new[] { new PenetrationMaterialSpec("wood", 1f), new PenetrationMaterialSpec("wood", 2f) }));
        }
    }
}

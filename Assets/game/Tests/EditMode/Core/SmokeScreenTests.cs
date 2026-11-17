using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SmokeScreenTests
    {
        private static readonly Vector3 _center = new Vector3(0f, 1f, 10f);

        [Test]
        public void ALineThroughTheSmokeIsBlocked()
        {
            Assert.That(SmokeScreen.Blocks(_center, 3f, new Vector3(0f, 1.6f, 0f), new Vector3(0f, 1f, 20f)), Is.True);
            Assert.That(SmokeScreen.Blocks(_center, 3f, new Vector3(-10f, 1f, 12f), new Vector3(10f, 1f, 8f)), Is.True);
        }

        [Test]
        public void ALinePassingBesideOrStoppingShortOfTheSmokeIsNotBlocked()
        {
            Assert.That(SmokeScreen.Blocks(_center, 3f, new Vector3(4f, 1f, 0f), new Vector3(4f, 1f, 20f)), Is.False, "passes beside");
            Assert.That(SmokeScreen.Blocks(_center, 3f, new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 6f)), Is.False, "ends before the smoke");
            Assert.That(SmokeScreen.Blocks(_center, 3f, new Vector3(0f, 1f, 14f), new Vector3(0f, 1f, 30f)), Is.False, "starts past the smoke");
        }

        [Test]
        public void SeeingFromOrIntoTheSmokeIsBlocked()
        {
            Assert.That(SmokeScreen.Blocks(_center, 3f, _center, new Vector3(0f, 1f, 30f)), Is.True, "from inside");
            Assert.That(SmokeScreen.Blocks(_center, 3f, new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 9f)), Is.True, "into it");
            Assert.That(SmokeScreen.Blocks(_center, 3f, _center, _center), Is.True, "a point inside");
        }

        [Test]
        public void TheRadiusMustBePositiveAndFinite()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SmokeScreen.Blocks(_center, 0f, Vector3.Zero, Vector3.UnitZ));
            Assert.Throws<ArgumentOutOfRangeException>(() => SmokeScreen.Blocks(_center, float.NaN, Vector3.Zero, Vector3.UnitZ));
            Assert.Throws<ArgumentOutOfRangeException>(() => SmokeScreen.Blocks(_center, float.PositiveInfinity, Vector3.Zero, Vector3.UnitZ));
        }

        [Test]
        public void OnlyAnAreaThatNeitherHurtsNorAfflictsIsSmoke()
        {
            Assert.That(SmokeScreen.IsSmoke(new AreaEffectSpec(5f, 15f, 0.5f, 0, visualId: "smoke")), Is.True);
            Assert.That(SmokeScreen.IsSmoke(new AreaEffectSpec(4f, 8f, 0.5f, 5, visualId: "fire")), Is.False);
            Assert.That(SmokeScreen.IsSmoke(null), Is.False);
        }
    }
}

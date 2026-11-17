using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StealthNoiseJsonTests
    {
        private const float _tolerance = 1e-5f;

        private static readonly string[] _expectedItemNames =
        {
            "footstepNoiseRadius", "walkStride", "sprintStride", "crouchStride", "walkVolume", "sprintVolume", "crouchVolume",
            "minLandingFallSpeed", "hardLandingFallSpeed", "minLandingVolume",
        };

        [Test]
        public void TheItemNamesAreTheSpecArgumentNames()
        {
            Assert.That(StealthNoiseJson.ItemNames, Is.EqualTo(_expectedItemNames));
        }

        [Test]
        public void TheDefaultsAreTheFormerConstants()
        {
            var spec = StealthNoiseSpec.Default;

            Assert.That(spec.FootstepNoiseRadius, Is.EqualTo(12f));
            Assert.That(spec.WalkStride, Is.EqualTo(2.2f));
            Assert.That(spec.SprintStride, Is.EqualTo(2.6f));
            Assert.That(spec.CrouchStride, Is.EqualTo(1.5f));
            Assert.That(spec.WalkVolume, Is.EqualTo(0.6f));
            Assert.That(spec.SprintVolume, Is.EqualTo(0.85f));
            Assert.That(spec.CrouchVolume, Is.EqualTo(0.2f));
            Assert.That(spec.MinLandingFallSpeed, Is.EqualTo(2.5f));
            Assert.That(spec.HardLandingFallSpeed, Is.EqualTo(12f));
            Assert.That(spec.MinLandingVolume, Is.EqualTo(0.35f));
        }

        [Test]
        public void ReadsEveryItem()
        {
            var spec = StealthNoiseJson.Parse(
                "{\"footstepNoiseRadius\": 30, \"walkStride\": 2, \"sprintStride\": 3, \"crouchStride\": 1,"
                + " \"walkVolume\": 0.5, \"sprintVolume\": 0.9, \"crouchVolume\": 0.1,"
                + " \"minLandingFallSpeed\": 3, \"hardLandingFallSpeed\": 15, \"minLandingVolume\": 0.4}",
                "stealth.json");

            Assert.That(spec.FootstepNoiseRadius, Is.EqualTo(30f));
            Assert.That(spec.WalkStride, Is.EqualTo(2f));
            Assert.That(spec.SprintStride, Is.EqualTo(3f));
            Assert.That(spec.CrouchStride, Is.EqualTo(1f));
            Assert.That(spec.WalkVolume, Is.EqualTo(0.5f));
            Assert.That(spec.SprintVolume, Is.EqualTo(0.9f));
            Assert.That(spec.CrouchVolume, Is.EqualTo(0.1f));
            Assert.That(spec.MinLandingFallSpeed, Is.EqualTo(3f));
            Assert.That(spec.HardLandingFallSpeed, Is.EqualTo(15f));
            Assert.That(spec.MinLandingVolume, Is.EqualTo(0.4f));
        }

        [Test]
        public void OmittedItemsUseTheDefaults()
        {
            var spec = StealthNoiseJson.Parse("{\"footstepNoiseRadius\": 30}", "stealth.json");

            Assert.That(spec.FootstepNoiseRadius, Is.EqualTo(30f));
            Assert.That(spec.WalkStride, Is.EqualTo(FootstepCadence.DefaultWalkStride));
            Assert.That(spec.CrouchVolume, Is.EqualTo(FootstepCadence.DefaultCrouchVolume));
            Assert.That(spec.MinLandingFallSpeed, Is.EqualTo(FootstepCadence.DefaultMinLandingFallSpeed));

            var empty = StealthNoiseJson.Parse("{}", "stealth.json");
            Assert.That(empty.FootstepNoiseRadius, Is.EqualTo(StealthNoiseSpec.DefaultFootstepNoiseRadius));
        }

        [Test]
        public void ARadiusOfZeroIsSilent()
        {
            Assert.That(StealthNoiseJson.Parse("{\"footstepNoiseRadius\": 0}", "stealth.json").FootstepNoiseRadius, Is.Zero);
        }

        [TestCase("{\"footstepNoiseRadius\": -1}", "footstepNoiseRadius")]
        [TestCase("{\"walkStride\": 0}", "walkStride")]
        [TestCase("{\"sprintStride\": -2}", "sprintStride")]
        [TestCase("{\"crouchVolume\": 1.5}", "crouchVolume")]
        [TestCase("{\"minLandingFallSpeed\": 0}", "minLandingFallSpeed")]
        [TestCase("{\"hardLandingFallSpeed\": 2}", "hardLandingFallSpeed")]
        [TestCase("{\"minLandingVolume\": -0.1}", "minLandingVolume")]
        [TestCase("{\"walkVolume\": \"loud\"}", "walkVolume")]
        [TestCase("{\"walkStride\": null}", "walkStride")]
        public void InvalidItemsAreNamed(string json, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => StealthNoiseJson.Parse(json, "stealth.json"));

            Assert.That(e.SourceName, Is.EqualTo("stealth.json"));
            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("stealth JSON 'stealth.json'"));
            Assert.That(e.Message, Does.Contain(item));
        }

        [Test]
        public void UnknownItemsAndBrokenJsonAreRejected()
        {
            var unknown = Assert.Throws<TuningJsonException>(() => StealthNoiseJson.Parse("{\"stride\": 2}", "stealth.json"));
            Assert.That(unknown.Item, Is.EqualTo("stride"));
            Assert.That(unknown.Message, Does.Contain("footstepNoiseRadius"), "lists the known items");

            var broken = Assert.Throws<TuningJsonException>(() => StealthNoiseJson.Parse("{\"walkStride\": ", "stealth.json"));
            Assert.That(broken.Item, Is.Null);

            var array = Assert.Throws<TuningJsonException>(() => StealthNoiseJson.Parse("[]", null));
            Assert.That(array.SourceName, Is.EqualTo("(unnamed stealth JSON)"));
        }

        [Test]
        public void TheCadenceUsesTheSpecStridesVolumesAndLanding()
        {
            var cadence = new StealthNoiseSpec(
                walkStride: 1f, sprintStride: 3f, crouchStride: 0.5f, walkVolume: 0.5f, sprintVolume: 1f, crouchVolume: 0.1f,
                minLandingFallSpeed: 4f, hardLandingFallSpeed: 8f, minLandingVolume: 0.5f).CreateCadence();

            Assert.That(cadence.StrideFor(false, false), Is.EqualTo(1f));
            Assert.That(cadence.StrideFor(false, true), Is.EqualTo(3f));
            Assert.That(cadence.StrideFor(true, false), Is.EqualTo(0.5f));
            Assert.That(cadence.VolumeFor(false, false), Is.EqualTo(0.5f));
            Assert.That(cadence.VolumeFor(false, true), Is.EqualTo(1f));
            Assert.That(cadence.VolumeFor(true, false), Is.EqualTo(0.1f));
            Assert.That(cadence.LandingVolume(3.9f), Is.Zero, "below the configured threshold, though above the default 2.5");
            Assert.That(cadence.LandingVolume(4f), Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(cadence.LandingVolume(6f), Is.EqualTo(0.75f).Within(_tolerance));
            Assert.That(cadence.LandingVolume(8f), Is.EqualTo(1f).Within(_tolerance));
            Assert.That(cadence.LandingVolume(20f), Is.EqualTo(1f).Within(_tolerance));
        }

        [Test]
        public void EachCadenceStartsFresh()
        {
            var spec = new StealthNoiseSpec(walkStride: 1f);
            var first = spec.CreateCadence();
            first.Advance(0.3f, 0f, 0.02f, true, false, false);

            var second = spec.CreateCadence();

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.Progress, Is.EqualTo(FootstepCadence.FirstStepProgress));
        }

        [Test]
        public void TheCadenceRejectsAHardLandingNotAboveTheMinimum()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(minLandingFallSpeed: 5f, hardLandingFallSpeed: 5f));
            Assert.That(e.ParamName, Is.EqualTo("hardLandingFallSpeed"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(minLandingFallSpeed: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(hardLandingFallSpeed: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(minLandingVolume: 1.1f));
        }
    }
}

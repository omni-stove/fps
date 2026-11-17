using Shooter.Core;
using NUnit.Framework;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class AimAssistTuningFileTests
    {
        [Test]
        public void TheBundledFileHoldsTheDefaults()
        {
            var asset = Resources.Load<TextAsset>("Tuning/aim-assist");
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/aim-assist.json");

            var spec = AimAssistJson.Parse(asset.text, "Resources/Tuning/aim-assist.json");

            Assert.That(spec.SlowdownDegrees, Is.EqualTo(AimAssistSpec.DefaultSlowdownDegrees));
            Assert.That(spec.SlowdownScale, Is.EqualTo(AimAssistSpec.DefaultSlowdownScale));
            Assert.That(spec.MagnetismDegrees, Is.EqualTo(AimAssistSpec.DefaultMagnetismDegrees));
            Assert.That(spec.MagnetismDegreesPerSecond, Is.EqualTo(AimAssistSpec.DefaultMagnetismDegreesPerSecond));
            Assert.That(spec.MagnetismSeconds, Is.EqualTo(AimAssistSpec.DefaultMagnetismSeconds));
            Assert.That(spec.MaxDistance, Is.EqualTo(AimAssistSpec.DefaultMaxDistance));
        }
    }
}

using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class RumbleTuningFileTests
    {
        [Test]
        public void TheBundledFileHoldsTheDefaults()
        {
            var asset = Resources.Load<TextAsset>(GamepadRumble.TuningResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/rumble.json");

            var spec = RumbleJson.Parse(asset.text, "Resources/Tuning/rumble.json");

            Assert.That(spec.Fire, Is.EqualTo(RumbleSpec.Default.Fire));
            Assert.That(spec.Damage, Is.EqualTo(RumbleSpec.Default.Damage));
            Assert.That(spec.Explosion, Is.EqualTo(RumbleSpec.Default.Explosion));
            Assert.That(spec.ExplosionRadius, Is.EqualTo(RumbleSpec.DefaultExplosionRadius));
        }
    }
}

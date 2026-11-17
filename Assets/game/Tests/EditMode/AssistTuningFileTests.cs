using Shooter.Core;
using NUnit.Framework;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class AssistTuningFileTests
    {
        [Test]
        public void TheBundledFileHoldsTheDefaults()
        {
            var asset = Resources.Load<TextAsset>("Tuning/assist");
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/assist.json");

            var spec = AssistJson.Parse(asset.text, "Resources/Tuning/assist.json");

            Assert.That(spec.WindowSeconds, Is.EqualTo(AssistSpec.DefaultWindowSeconds));
            Assert.That(spec.MinDamage, Is.EqualTo(AssistSpec.DefaultMinDamage));
        }
    }
}

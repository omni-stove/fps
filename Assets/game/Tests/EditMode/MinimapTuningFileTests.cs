using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class MinimapTuningFileTests
    {
        [Test]
        public void TheBundledFileHoldsTheDefaults()
        {
            var asset = Resources.Load<TextAsset>(MinimapJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/minimap.json");

            var lines = new List<string>();
            var spec = MinimapJson.Parse(asset.text, "Resources/Tuning/minimap.json", lines.Add);

            Assert.That(spec.Radius, Is.EqualTo(MinimapSpec.DefaultRadius));
            Assert.That(spec.Scale, Is.EqualTo(MinimapSpec.DefaultScale));
            Assert.That(spec.UpdateIntervalSeconds, Is.EqualTo(MinimapSpec.DefaultUpdateIntervalSeconds));
            Assert.That(spec.SpottedSeconds, Is.EqualTo(MinimapSpec.DefaultSpottedSeconds));
            Assert.That(spec.RotateWithPlayer, Is.EqualTo(MinimapSpec.DefaultRotateWithPlayer));
            Assert.That(lines, Is.Empty);
        }
    }
}

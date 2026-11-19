using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class ReplayTuningFileTests
    {
        [TearDown]
        public void TearDown()
        {
            KitTuning.Reload();
        }

        [Test]
        public void TheBundledFileHoldsTheDefaults()
        {
            var asset = Resources.Load<TextAsset>(ReplayJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/replay.json");

            var spec = ReplayJson.Parse(asset.text, "Resources/Tuning/replay.json");

            Assert.That(spec.FreeSpeed, Is.EqualTo(ReplaySpec.DefaultFreeSpeed));
        }

        [Test]
        public void KitTuningReadsTheBundledFile()
        {
            KitTuning.Reload();

            Assert.That(KitTuning.Replay.FreeSpeed, Is.EqualTo(6f));
        }
    }
}

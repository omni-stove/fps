using Shooter.Core;
using NUnit.Framework;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class SkillTuningFileTests
    {
        [Test]
        public void TheBundledFileHoldsTheDefaults()
        {
            var asset = Resources.Load<TextAsset>("Tuning/skill");
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/skill.json");

            var spec = SkillJson.Parse(asset.text, "Resources/Tuning/skill.json");

            Assert.That(spec.InitialRating, Is.EqualTo(SkillSpec.DefaultInitialRating));
            Assert.That(spec.KFactor, Is.EqualTo(SkillSpec.DefaultKFactor));
            Assert.That(spec.MinRating, Is.EqualTo(SkillSpec.DefaultMinRating));
            Assert.That(spec.MaxRating, Is.EqualTo(SkillSpec.DefaultMaxRating));
        }
    }
}

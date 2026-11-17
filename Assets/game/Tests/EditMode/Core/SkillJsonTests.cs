using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SkillJsonTests
    {
        private static readonly string[] _expectedItemNames = { "initialRating", "kFactor", "minRating", "maxRating" };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = SkillJson.Parse("{\"initialRating\": 1500, \"kFactor\": 24.5, \"minRating\": 100, \"maxRating\": 4000}", "skill.json");

            Assert.That(spec.InitialRating, Is.EqualTo(1500));
            Assert.That(spec.KFactor, Is.EqualTo(24.5f));
            Assert.That(spec.MinRating, Is.EqualTo(100));
            Assert.That(spec.MaxRating, Is.EqualTo(4000));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var empty = SkillJson.Parse("{}", "skill.json");
            var partial = SkillJson.Parse("{\"kFactor\": 16}", "skill.json");

            Assert.That(empty.InitialRating, Is.EqualTo(1000));
            Assert.That(empty.KFactor, Is.EqualTo(32f));
            Assert.That(empty.MinRating, Is.EqualTo(0));
            Assert.That(empty.MaxRating, Is.EqualTo(3000));
            Assert.That(partial.InitialRating, Is.EqualTo(1000));
            Assert.That(partial.KFactor, Is.EqualTo(16f));
        }

        [Test]
        public void TheDefaultSpecHoldsTheDefaultConstants()
        {
            Assert.That(SkillSpec.Default.InitialRating, Is.EqualTo(SkillSpec.DefaultInitialRating));
            Assert.That(SkillSpec.Default.KFactor, Is.EqualTo(SkillSpec.DefaultKFactor));
            Assert.That(SkillSpec.Default.MinRating, Is.EqualTo(SkillSpec.DefaultMinRating));
            Assert.That(SkillSpec.Default.MaxRating, Is.EqualTo(SkillSpec.DefaultMaxRating));
            Assert.That(SkillSpec.Default.ToString(), Is.EqualTo("initial=1000 k=32 min=0 max=3000"));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(SkillJson.ItemNames, Is.EqualTo(_expectedItemNames));
        }

        [TestCase("{\"kFactor\": 0}", "kFactor")]
        [TestCase("{\"kFactor\": -4}", "kFactor")]
        [TestCase("{\"minRating\": -1}", "minRating")]
        [TestCase("{\"minRating\": 3000}", "maxRating")]
        [TestCase("{\"maxRating\": 0}", "maxRating")]
        [TestCase("{\"initialRating\": 3001}", "initialRating")]
        [TestCase("{\"initialRating\": 50, \"minRating\": 100}", "initialRating")]
        public void AnOutOfRangeValueNamesItsItem(string json, string item)
        {
            var error = Assert.Throws<TuningJsonException>(() => SkillJson.Parse(json, "skill.json"));

            Assert.That(error.SourceName, Is.EqualTo("skill.json"));
            Assert.That(error.Item, Is.EqualTo(item));
            Assert.That(error.Message, Does.StartWith("skill JSON 'skill.json' item '" + item + "' is invalid: "));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void AnUnknownItemAWrongTypeOrBrokenJsonIsRejected()
        {
            var unknown = Assert.Throws<TuningJsonException>(() => SkillJson.Parse("{\"k\": 2}", "skill.json"));
            var wrongType = Assert.Throws<TuningJsonException>(() => SkillJson.Parse("{\"initialRating\": \"high\"}", "skill.json"));
            var broken = Assert.Throws<TuningJsonException>(() => SkillJson.Parse("{\"kFactor\": ", "skill.json"));

            Assert.That(unknown.Item, Is.EqualTo("k"));
            Assert.That(unknown.Message, Does.Contain("kFactor"), "lists the known items");
            Assert.That(wrongType.Item, Is.EqualTo("initialRating"));
            Assert.That(broken.Item, Is.Null);
            Assert.Throws<ArgumentNullException>(() => SkillJson.Parse(null, "skill.json"));
        }

        [Test]
        public void TheSpecChecksItsValues()
        {
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new SkillSpec(kFactor: float.NaN)).ParamName, Is.EqualTo("kFactor"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new SkillSpec(kFactor: float.PositiveInfinity)).ParamName, Is.EqualTo("kFactor"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new SkillSpec(1000, 32f, 1000, 1000)).ParamName, Is.EqualTo("maxRating"));
            Assert.That(new SkillSpec(1000, 32f, 1000, 1001).InitialRating, Is.EqualTo(1000), "the initial rating may sit on the floor");
            Assert.That(new SkillSpec(3000, 32f, 0, 3000).InitialRating, Is.EqualTo(3000), "the initial rating may sit on the ceiling");
        }
    }
}

using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StashSaveTests
    {
        private const string _ammo = StashTests.Ammo;
        private const string _bandage = StashTests.Bandage;

        [Test]
        public void WritesTheVersionAndEachStacksIdAndCount()
        {
            var json = StashSave.ToJson(new[] { new StashEntry(_ammo, 30), new StashEntry(_bandage, 2) });

            Assert.That(
                json,
                Is.EqualTo("{\"version\":1,\"stacks\":[{\"itemId\":\"" + _ammo + "\",\"count\":30},{\"itemId\":\"" + _bandage + "\",\"count\":2}]}"));
            Assert.That(StashSave.ToJson(Array.Empty<StashEntry>()), Is.EqualTo("{\"version\":1,\"stacks\":[]}"));
        }

        [Test]
        public void ReadsBackWhatItWrote()
        {
            var json = StashSave.ToJson(new[] { new StashEntry(_ammo, 30), new StashEntry(_bandage, 2) });

            var stacks = StashSave.FromJson(json, out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(stacks.Count, Is.EqualTo(2));
            Assert.That(stacks[0].ItemId, Is.EqualTo(_ammo));
            Assert.That(stacks[0].Count, Is.EqualTo(30));
            Assert.That(stacks[1].ItemId, Is.EqualTo(_bandage));
            Assert.That(stacks[1].Count, Is.EqualTo(2));
        }

        [TestCase("", "the saved stash is empty")]
        [TestCase("{not json", "the saved stash is not valid JSON")]
        [TestCase("[]", "the saved stash is not a JSON object")]
        [TestCase("{\"version\":2,\"stacks\":[]}", "the saved stash has an unknown version")]
        [TestCase("{\"version\":1}", "the saved stash has no stacks list")]
        [TestCase("{\"version\":1,\"stacks\":[3]}", "stack 0 of the saved stash is not a JSON object")]
        [TestCase("{\"version\":1,\"stacks\":[{\"count\":1}]}", "stack 0 of the saved stash has no item id")]
        [TestCase("{\"version\":1,\"stacks\":[{\"itemId\":\"" + _ammo + "\",\"count\":0}]}", "stack 0 of the saved stash has no positive count")]
        [TestCase("{\"version\":1,\"stacks\":[{\"itemId\":\"" + _ammo + "\",\"count\":\"1\"}]}", "stack 0 of the saved stash has no positive count")]
        [TestCase("{\"version\":1,\"stacks\":[{\"itemId\":\"" + _ammo + "\",\"count\":2147483648}]}", "stack 0 of the saved stash has no positive count")]
        [TestCase("{\"version\":1,\"stacks\":[{\"itemId\":\"" + _ammo + "\",\"count\":1.5}]}", "the saved stash is not valid JSON")]
        [TestCase("{\"version\":1,\"stacks\":[{\"itemId\":\"Ammo\",\"count\":1}]}", "stack 0 of the saved stash: itemId must be a lowercase hyphenated UUID")]
        public void ReadsNothingFromABrokenSave(string text, string expectedProblem)
        {
            var stacks = StashSave.FromJson(text, out var problem);

            Assert.That(stacks, Is.Null);
            Assert.That(problem, Does.StartWith(expectedProblem));
        }

        [Test]
        public void RejectsMissingStacks()
        {
            Assert.Throws<ArgumentNullException>(() => StashSave.ToJson(null));
        }
    }
}

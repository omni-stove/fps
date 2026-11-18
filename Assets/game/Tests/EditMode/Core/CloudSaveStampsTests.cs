using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CloudSaveStampsTests
    {
        static readonly string[] ExpectedSlotOrder = { "campaign-Mission02", "profile" };

        [Test]
        public void EmptyStampsAreWrittenWithNoSlots()
        {
            Assert.That(new CloudSaveStamps().ToJson(), Is.EqualTo("{\"version\":1,\"slots\":{}}"));
        }

        [Test]
        public void SlotsAreWrittenInNameOrder()
        {
            var stamps = new CloudSaveStamps();
            stamps.Set("profile", new CloudSaveStamp(20, false));
            stamps.Set("campaign-Mission02", new CloudSaveStamp(10, true));

            Assert.That(
                stamps.ToJson(),
                Is.EqualTo("{\"version\":1,\"slots\":{\"campaign-Mission02\":{\"savedAt\":10,\"deleted\":true},"
                    + "\"profile\":{\"savedAt\":20,\"deleted\":false}}}"));
            Assert.That(stamps.Slots, Is.EqualTo(ExpectedSlotOrder));
            Assert.That(stamps.Count, Is.EqualTo(2));
        }

        [Test]
        public void StampsAreReadBack()
        {
            var stamps = new CloudSaveStamps();
            stamps.Set("profile", new CloudSaveStamp(20, false));
            stamps.Set("campaign", new CloudSaveStamp(10, true));

            var read = CloudSaveStamps.FromJson(stamps.ToJson());

            Assert.That(read.TryGet("profile", out var profile), Is.True);
            Assert.That(profile, Is.EqualTo(new CloudSaveStamp(20, false)));
            Assert.That(read.TryGet("campaign", out var campaign), Is.True);
            Assert.That(campaign, Is.EqualTo(new CloudSaveStamp(10, true)));
            Assert.That(read.TryGet("campaign-Other", out _), Is.False);
        }

        [Test]
        public void SettingASlotAgainReplacesIt()
        {
            var stamps = new CloudSaveStamps();
            stamps.Set("profile", new CloudSaveStamp(20, false));
            stamps.Set("profile", new CloudSaveStamp(30, true));

            Assert.That(stamps.TryGet("profile", out var stamp), Is.True);
            Assert.That(stamp, Is.EqualTo(new CloudSaveStamp(30, true)));
            Assert.That(stamps.Count, Is.EqualTo(1));
        }

        [TestCase("{}")]
        [TestCase("{\"version\":2,\"slots\":{}}")]
        [TestCase("{\"version\":1}")]
        [TestCase("{\"version\":1,\"slots\":[]}")]
        [TestCase("{\"version\":1,\"slots\":{\"profile\":{\"savedAt\":-1,\"deleted\":false}}}")]
        [TestCase("{\"version\":1,\"slots\":{\"profile\":{\"savedAt\":1}}}")]
        [TestCase("{\"version\":1,\"slots\":{\"profile\":{\"savedAt\":1,\"deleted\":false,\"extra\":0}}}")]
        [TestCase("{\"version\":1,\"slots\":{\"\":{\"savedAt\":1,\"deleted\":false}}}")]
        public void BrokenDataIsRejected(string json)
        {
            Assert.Throws<FormatException>(() => CloudSaveStamps.FromJson(json));
        }

        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => CloudSaveStamps.FromJson(null));
            Assert.Throws<ArgumentNullException>(() => new CloudSaveStamps().TryGet(null, out _));
            Assert.Throws<ArgumentException>(() => new CloudSaveStamps().Set("", new CloudSaveStamp(1, false)));
        }
    }
}

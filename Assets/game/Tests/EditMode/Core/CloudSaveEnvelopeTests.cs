using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CloudSaveEnvelopeTests
    {
        [Test]
        public void ASavedSlotIsWrittenWithItsTimeAndText()
        {
            var envelope = CloudSaveEnvelope.Saved("profile", 1700000000123, "{\"xp\":5}");

            Assert.That(
                envelope.ToJson(),
                Is.EqualTo("{\"version\":1,\"slot\":\"profile\",\"savedAt\":1700000000123,\"deleted\":false,\"text\":\"{\\\"xp\\\":5}\"}"));
        }

        [Test]
        public void ADeletedSlotIsWrittenWithoutText()
        {
            var envelope = CloudSaveEnvelope.Removed("campaign-Mission02", 42);

            Assert.That(
                envelope.ToJson(),
                Is.EqualTo("{\"version\":1,\"slot\":\"campaign-Mission02\",\"savedAt\":42,\"deleted\":true,\"text\":null}"));
        }

        [Test]
        public void ASavedSlotIsReadBack()
        {
            const string text = "line1\nline2 \"quoted\" \\ end";
            var read = CloudSaveEnvelope.FromJson(CloudSaveEnvelope.Saved("profile", 99, text).ToJson());

            Assert.That(read.Slot, Is.EqualTo("profile"));
            Assert.That(read.SavedAt, Is.EqualTo(99));
            Assert.That(read.Deleted, Is.False);
            Assert.That(read.Text, Is.EqualTo(text));
            Assert.That(read.Stamp, Is.EqualTo(new CloudSaveStamp(99, false)));
        }

        [Test]
        public void ADeletedSlotIsReadBack()
        {
            var read = CloudSaveEnvelope.FromJson(CloudSaveEnvelope.Removed("campaign", 7).ToJson());

            Assert.That(read.Slot, Is.EqualTo("campaign"));
            Assert.That(read.Deleted, Is.True);
            Assert.That(read.Text, Is.Null);
            Assert.That(read.Stamp, Is.EqualTo(new CloudSaveStamp(7, true)));
        }

        [Test]
        public void ANewerVersionIsRejected()
        {
            var error = Assert.Throws<FormatException>(() => CloudSaveEnvelope.FromJson(
                "{\"version\":2,\"slot\":\"profile\",\"savedAt\":1,\"deleted\":false,\"text\":\"x\"}"));

            Assert.That(error.Message, Is.EqualTo("unsupported cloud save version (expected 1)"));
        }

        [TestCase("[]")]
        [TestCase("{\"version\":1}")]
        [TestCase("{\"version\":1,\"slot\":\"\",\"savedAt\":1,\"deleted\":false,\"text\":\"x\"}")]
        [TestCase("{\"version\":1,\"slot\":\"profile\",\"savedAt\":-1,\"deleted\":false,\"text\":\"x\"}")]
        [TestCase("{\"version\":1,\"slot\":\"profile\",\"savedAt\":1,\"deleted\":\"no\",\"text\":\"x\"}")]
        [TestCase("{\"version\":1,\"slot\":\"profile\",\"savedAt\":1,\"deleted\":false,\"text\":null}")]
        [TestCase("{\"version\":1,\"slot\":\"profile\",\"savedAt\":1,\"deleted\":true,\"text\":\"x\"}")]
        [TestCase("{\"version\":1,\"slot\":\"profile\",\"savedAt\":1,\"deleted\":false,\"text\":\"x\",\"extra\":1}")]
        public void BrokenDataIsRejected(string json)
        {
            Assert.Throws<FormatException>(() => CloudSaveEnvelope.FromJson(json));
        }

        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => CloudSaveEnvelope.FromJson(null));
            Assert.Throws<ArgumentNullException>(() => CloudSaveEnvelope.Saved("profile", 1, null));
            Assert.Throws<ArgumentException>(() => CloudSaveEnvelope.Saved("", 1, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => CloudSaveEnvelope.Removed("profile", -1));
        }
    }
}

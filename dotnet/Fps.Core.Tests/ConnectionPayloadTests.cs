using System;
using System.Text;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ConnectionPayloadTests
    {
        private const string Key = "0123456789abcdef0123456789abcdef";

        [Test]
        public void RoundTripsVersionAndKey()
        {
            var payload = ConnectionPayload.Encode("0.2.0-3-gabc1234", Key);

            Assert.That(Encoding.ASCII.GetString(payload), Is.EqualTo("0.2.0-3-gabc1234/" + Key));
            Assert.That(ConnectionPayload.TryDecode(payload, out var version, out var key), Is.True);
            Assert.That(version, Is.EqualTo("0.2.0-3-gabc1234"));
            Assert.That(key, Is.EqualTo(Key));
        }

        [Test]
        public void KeepsVersionCaseButFoldsKeyCase()
        {
            Assert.That(ConnectionPayload.TryDecode(Encoding.ASCII.GetBytes("Beta_1/ABC"), out var version, out var key), Is.True);
            Assert.That(version, Is.EqualTo("Beta_1"));
            Assert.That(key, Is.EqualTo("abc"));
        }

        [Test]
        public void TheLongestPayloadFits()
        {
            var payload = ConnectionPayload.Encode(new string('1', BuildVersion.MaxLength), new string('a', RejoinKey.MaxLength));

            Assert.That(payload, Has.Length.EqualTo(ConnectionPayload.MaxLength));
            Assert.That(ConnectionPayload.TryDecode(payload, out _, out _), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0.1.0")]
        [TestCase("/abc")]
        [TestCase("0.1.0/")]
        [TestCase("0.1.0/abc/def")]
        [TestCase("v0.1.0/abc")]
        [TestCase("0.1.0+meta/abc")]
        [TestCase(".1/abc")]
        [TestCase("0.1.0/has space")]
        public void RejectsMalformedPayloads(string text)
        {
            var payload = text == null ? null : Encoding.ASCII.GetBytes(text);

            Assert.That(ConnectionPayload.TryDecode(payload, out var version, out var key), Is.False);
            Assert.That(version, Is.Null);
            Assert.That(key, Is.Null);
        }

        [Test]
        public void RejectsOversizedAndNonAsciiPayloads()
        {
            Assert.That(ConnectionPayload.TryDecode(new byte[ConnectionPayload.MaxLength + 1], out _, out _), Is.False);
            Assert.That(ConnectionPayload.TryDecode(new byte[] { 0x31, 0xff, (byte)'/', 0x61 }, out _, out _), Is.False);
        }

        [Test]
        public void ALegacyKeyOnlyPayloadIsUnreadable()
        {
            Assert.That(ConnectionPayload.TryDecode(RejoinKey.Encode(Key), out _, out _), Is.False);
        }

        [Test]
        public void EncodeRejectsUnnormalizedVersionsAndInvalidKeys()
        {
            Assert.Throws<ArgumentException>(() => ConnectionPayload.Encode(null, Key));
            Assert.Throws<ArgumentException>(() => ConnectionPayload.Encode("v0.1.0", Key));
            Assert.Throws<ArgumentException>(() => ConnectionPayload.Encode(" 0.1.0", Key));
            Assert.Throws<ArgumentException>(() => ConnectionPayload.Encode("0.1/0", Key));
            Assert.Throws<ArgumentException>(() => ConnectionPayload.Encode("0.1.0", ""));
            Assert.Throws<ArgumentException>(() => ConnectionPayload.Encode("0.1.0", "a/b"));
        }

        [Test]
        public void ReplacingTheKeyKeepsTheVersion()
        {
            var payload = ConnectionPayload.Encode("0.2.0", "device");

            Assert.That(ConnectionPayload.TryReplaceRejoinKey(payload, Key, out var replaced), Is.True);
            Assert.That(ConnectionPayload.TryDecode(replaced, out var version, out var key), Is.True);
            Assert.That(version, Is.EqualTo("0.2.0"));
            Assert.That(key, Is.EqualTo(Key));
        }

        [Test]
        public void ReplacingTheKeyFailsOnUnreadablePayloadsOrKeys()
        {
            var payload = ConnectionPayload.Encode("0.2.0", "device");

            Assert.That(ConnectionPayload.TryReplaceRejoinKey(RejoinKey.Encode("device"), Key, out var replaced), Is.False);
            Assert.That(replaced, Is.Null);
            Assert.That(ConnectionPayload.TryReplaceRejoinKey(null, Key, out _), Is.False);
            Assert.That(ConnectionPayload.TryReplaceRejoinKey(payload, "not a key", out _), Is.False);
        }
    }
}

using System;
using System.Text;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class ConnectionPayloadTests
    {
        public const string Key = "0123456789abcdef0123456789abcdef";

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
            var extras = new ConnectionExtras(
                new string('p', ConnectionExtras.MaxPartyIdLength),
                new string('C', ConnectionExtras.MaxJoinCodeLength),
                int.MinValue,
                new string('u', ConnectionExtras.MaxProductUserIdLength));
            var payload = ConnectionPayload.Encode(new string('1', BuildVersion.MaxLength), new string('a', RejoinKey.MaxLength), extras);

            Assert.That(payload, Has.Length.EqualTo(ConnectionPayload.MaxLength));
            Assert.That(ConnectionPayload.TryDecode(payload, out _, out _, out var decoded), Is.True);
            Assert.That(decoded.SkillRating, Is.EqualTo(int.MinValue));
        }

        [Test]
        public void WithoutExtrasThePayloadIsTheSameAsBefore()
        {
            var payload = ConnectionPayload.Encode("0.2.0", Key, ConnectionExtras.None);

            Assert.That(Encoding.ASCII.GetString(payload), Is.EqualTo("0.2.0/" + Key));
            Assert.That(ConnectionPayload.TryDecode(payload, out _, out _, out var extras), Is.True);
            Assert.That(extras.IsEmpty, Is.True);
            Assert.That(new ConnectionExtras("", "", null, "").IsEmpty, Is.True);
        }

        [Test]
        public void RoundTripsExtras()
        {
            var payload = ConnectionPayload.Encode("0.2.0", Key, new ConnectionExtras("Party-1", "AB12cd", 1500, Key));

            Assert.That(Encoding.ASCII.GetString(payload), Is.EqualTo("0.2.0/" + Key + "/Party-1/AB12cd/1500/" + Key));
            Assert.That(ConnectionPayload.TryDecode(payload, out var version, out var key, out var extras), Is.True);
            Assert.That(version, Is.EqualTo("0.2.0"));
            Assert.That(key, Is.EqualTo(Key));
            Assert.That(extras.PartyId, Is.EqualTo("Party-1"));
            Assert.That(extras.JoinCode, Is.EqualTo("AB12cd"));
            Assert.That(extras.SkillRating, Is.EqualTo(1500));
            Assert.That(extras.ProductUserId, Is.EqualTo(Key));
        }

        [Test]
        public void MissingExtrasAreEmptyFields()
        {
            var payload = ConnectionPayload.Encode("0.2.0", Key, new ConnectionExtras(null, null, -3, null));

            Assert.That(Encoding.ASCII.GetString(payload), Is.EqualTo("0.2.0/" + Key + "///-3/"));
            Assert.That(ConnectionPayload.TryDecode(payload, out _, out _, out var extras), Is.True);
            Assert.That(extras.PartyId, Is.Null);
            Assert.That(extras.JoinCode, Is.Null);
            Assert.That(extras.SkillRating, Is.EqualTo(-3));
            Assert.That(extras.ProductUserId, Is.Null);
        }

        [Test]
        public void TheShortDecodeReadsAPayloadWithExtras()
        {
            var payload = Encoding.ASCII.GetBytes("0.2.0/ABC/party//0/");

            Assert.That(ConnectionPayload.TryDecode(payload, out var version, out var key), Is.True);
            Assert.That(version, Is.EqualTo("0.2.0"));
            Assert.That(key, Is.EqualTo("abc"));
        }

        [TestCase("0.2.0/abc/p/c/1")]
        [TestCase("0.2.0/abc/p/c/1/u/x")]
        [TestCase("0.2.0/abc/p q/c/1/u")]
        [TestCase("0.2.0/abc/p/c.d/1/u")]
        [TestCase("0.2.0/abc/p/c/01/u")]
        [TestCase("0.2.0/abc/p/c/-0/u")]
        [TestCase("0.2.0/abc/p/c/+1/u")]
        [TestCase("0.2.0/abc/p/c/-/u")]
        [TestCase("0.2.0/abc/p/c/2147483648/u")]
        [TestCase("0.2.0/abc/p/c/1x/u")]
        [TestCase("0.2.0/abc/p/c/1/u:1")]
        [TestCase("0.2.0//p/c/1/u")]
        [TestCase("0.2.0/abc/p/ABCDEFGHIJKLMNOPQ/1/u")]
        public void RejectsMalformedExtras(string text)
        {
            Assert.That(ConnectionPayload.TryDecode(Encoding.ASCII.GetBytes(text), out var version, out var key, out var extras), Is.False);
            Assert.That(version, Is.Null);
            Assert.That(key, Is.Null);
            Assert.That(extras, Is.Null);
        }

        [TestCase("0", 0)]
        [TestCase("-1", -1)]
        [TestCase("2147483647", int.MaxValue)]
        [TestCase("-2147483648", int.MinValue)]
        public void ParsesSkillRatings(string text, int expected)
        {
            Assert.That(ConnectionPayload.TryParseSkillRating(text, out var value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        public void ExtrasRejectInvalidFields()
        {
            Assert.Throws<ArgumentException>(() => new ConnectionExtras("a/b", null, null, null));
            Assert.Throws<ArgumentException>(() => new ConnectionExtras(null, new string('c', ConnectionExtras.MaxJoinCodeLength + 1), null, null));
            Assert.Throws<ArgumentException>(() => new ConnectionExtras(null, null, null, new string('u', ConnectionExtras.MaxProductUserIdLength + 1)));
            Assert.Throws<ArgumentException>(() => new ConnectionExtras(null, "ぱ", null, null));
            Assert.Throws<ArgumentNullException>(() => ConnectionPayload.Encode("0.2.0", Key, null));
        }

        [Test]
        public void ReplacingTheKeyKeepsTheExtras()
        {
            var payload = ConnectionPayload.Encode("0.2.0", "device", new ConnectionExtras("party", "CODE", 7, null));

            Assert.That(ConnectionPayload.TryReplaceRejoinKey(payload, Key, out var replaced), Is.True);
            Assert.That(Encoding.ASCII.GetString(replaced), Is.EqualTo("0.2.0/" + Key + "/party/CODE/7/"));
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

using System;
using System.Text;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class RejoinKeyTests
    {
        private const string _productUserId = "0123456789abcdef0123456789abcdef";

        [Test]
        public void RoundTripsAProductUserId()
        {
            Assert.That(RejoinKey.TryDecode(RejoinKey.Encode(_productUserId), out var key), Is.True);
            Assert.That(key, Is.EqualTo(_productUserId));
        }

        [Test]
        public void DeviceKeysAreValidAndDistinct()
        {
            var first = RejoinKey.NewDeviceKey();
            var second = RejoinKey.NewDeviceKey();

            Assert.That(RejoinKey.IsValid(first), Is.True);
            Assert.That(first, Has.Length.EqualTo(32));
            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void DecodingFoldsUpperCase()
        {
            Assert.That(RejoinKey.TryDecode(Encoding.ASCII.GetBytes("ABCdef-_09"), out var key), Is.True);
            Assert.That(key, Is.EqualTo("abcdef-_09"));
            Assert.That(Encoding.ASCII.GetString(RejoinKey.Encode("ABC")), Is.EqualTo("abc"));
        }

        [Test]
        public void RejectsEmptyOversizedAndForeignPayloads()
        {
            Assert.That(RejoinKey.TryDecode(null, out var key), Is.False);
            Assert.That(key, Is.Null);
            Assert.That(RejoinKey.TryDecode(Array.Empty<byte>(), out _), Is.False);
            Assert.That(RejoinKey.TryDecode(new byte[RejoinKey.MaxLength + 1], out _), Is.False);
            Assert.That(RejoinKey.TryDecode(Encoding.ASCII.GetBytes("has space"), out _), Is.False);
            Assert.That(RejoinKey.TryDecode(new byte[] { 0x61, 0xff }, out _), Is.False);
            Assert.That(RejoinKey.TryDecode(Encoding.ASCII.GetBytes(new string('a', RejoinKey.MaxLength)), out _), Is.True);
        }

        [Test]
        public void EncodeRejectsInvalidKeys()
        {
            Assert.Throws<ArgumentException>(() => RejoinKey.Encode(null));
            Assert.Throws<ArgumentException>(() => RejoinKey.Encode(""));
            Assert.Throws<ArgumentException>(() => RejoinKey.Encode("a/b"));
            Assert.Throws<ArgumentException>(() => RejoinKey.Encode(new string('a', RejoinKey.MaxLength + 1)));
        }
    }
}

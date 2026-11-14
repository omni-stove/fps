using System;
using System.Text;
using NUnit.Framework;

namespace Fps.Protocol.Tests
{
    public sealed class TransportReasonTests
    {
        [Test]
        public void ReasonSurvivesEncodeAndDecode()
        {
            var bytes = TransportReason.Encode("fps:rejected:server-draining");

            Assert.That(TransportReason.Decode(bytes), Is.EqualTo("fps:rejected:server-draining"));
        }

        [Test]
        public void MissingReasonEncodesToNothing()
        {
            Assert.That(TransportReason.Encode(null), Is.Empty);
            Assert.That(TransportReason.Encode(""), Is.Empty);
            Assert.That(TransportReason.Decode(ReadOnlySpan<byte>.Empty), Is.Empty);
        }

        [Test]
        public void ReasonUpToTheLimitIsAccepted()
        {
            var longest = new string('a', TransportEvent.MaxReasonBytes);

            Assert.That(TransportReason.Decode(TransportReason.Encode(longest)), Is.EqualTo(longest));
        }

        [Test]
        public void EncodeRejectsTooLongOrBrokenReasons()
        {
            Assert.Throws<ArgumentException>(() => TransportReason.Encode(new string('a', TransportEvent.MaxReasonBytes + 1)));
            Assert.Throws<ArgumentException>(() => TransportReason.Encode("\ud800"));
        }

        [Test]
        public void DecodeTreatsTooLongOrBrokenBytesAsNoReason()
        {
            var tooLong = Encoding.UTF8.GetBytes(new string('a', TransportEvent.MaxReasonBytes + 1));

            Assert.That(TransportReason.Decode(tooLong), Is.Empty);
            Assert.That(TransportReason.Decode(new byte[] { 0xff, 0xfe }), Is.Empty);
        }
    }
}

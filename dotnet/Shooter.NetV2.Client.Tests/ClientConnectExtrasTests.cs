using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientConnectExtrasTests
    {
        private const string _userId = "0123456789abcdef0123456789abcdef";

        [Test]
        public void EveryFieldGoesIntoTheExtras()
        {
            var log = new List<string>();

            var extras = ClientConnectExtras.Compose("party-1", "ABC234", 1500, _userId, log.Add);

            Assert.That(extras.PartyId, Is.EqualTo("party-1"));
            Assert.That(extras.JoinCode, Is.EqualTo("ABC234"));
            Assert.That(extras.SkillRating, Is.EqualTo(1500));
            Assert.That(extras.ProductUserId, Is.EqualTo(_userId));
            Assert.That(log, Is.Empty);
        }

        [Test]
        public void MissingFieldsStayEmptyAndTheExtrasCanBeEmpty()
        {
            var extras = ClientConnectExtras.Compose(null, "", null, null, _ => { });

            Assert.That(extras.IsEmpty, Is.True);
        }

        [Test]
        public void AFieldThatDoesNotFitIsDroppedAndTheRestAreKept()
        {
            var log = new List<string>();

            var extras = ClientConnectExtras.Compose("party 1", "ABC234", 900, new string('a', 33), log.Add);

            Assert.That(extras.PartyId, Is.Null);
            Assert.That(extras.ProductUserId, Is.Null);
            Assert.That(extras.JoinCode, Is.EqualTo("ABC234"));
            Assert.That(extras.SkillRating, Is.EqualTo(900));
            Assert.That(log, Is.EqualTo(new[]
            {
                ConnectLog.FieldDropped(ConnectLog.PartyIdField, 7, ConnectionExtras.MaxPartyIdLength),
                ConnectLog.FieldDropped(ConnectLog.ProductUserIdField, 33, ConnectionExtras.MaxProductUserIdLength),
            }));
            Assert.That(log[0], Is.EqualTo(
                "[Net] dropped the connect request field partyId (length 7): it must be 1-64 characters of [0-9A-Za-z_-]"));
        }

        [Test]
        public void TheComposedExtrasSurviveTheConnectionPayload()
        {
            var extras = ClientConnectExtras.Compose("party-1", "ABC234", -20, _userId, _ => { });

            var payload = ConnectionPayload.Encode("0.3.0", RejoinKey.NewDeviceKey(), extras);

            Assert.That(ConnectionPayload.TryDecode(payload, out var version, out _, out var decoded), Is.True);
            Assert.That(version, Is.EqualTo("0.3.0"));
            Assert.That(decoded.PartyId, Is.EqualTo("party-1"));
            Assert.That(decoded.JoinCode, Is.EqualTo("ABC234"));
            Assert.That(decoded.SkillRating, Is.EqualTo(-20));
            Assert.That(decoded.ProductUserId, Is.EqualTo(_userId));
        }

        [Test]
        public void ALogIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => ClientConnectExtras.Compose(null, null, null, null, null));
        }
    }
}

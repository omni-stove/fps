using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerLobbyTests
    {
        private const string _ownerId = "0123456789abcdef0123456789abcdef";

        private static LobbyInfo Lobby(string endpoint)
        {
            var attributes = endpoint == null
                ? Array.Empty<LobbyAttribute>()
                : new[] { LobbyAttribute.OfText(ServerLobby.ServerKey, endpoint) };
            return new LobbyInfo("lobby-1", _ownerId, NetMatchSettings.DefaultMaxPlayers, new[] { _ownerId }, attributes);
        }

        private static Dictionary<string, LobbyAttribute> ByKey(IReadOnlyList<LobbyAttribute> attributes)
        {
            var byKey = new Dictionary<string, LobbyAttribute>();
            foreach (var attribute in attributes)
            {
                byKey.Add(attribute.Key, attribute);
            }

            return byKey;
        }

        [Test]
        public void AttributesCarryTheAddressModeVersionAndPlayers()
        {
            var attributes = ByKey(ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 2));

            Assert.That(attributes.Count, Is.EqualTo(4));
            Assert.That(attributes["server"].Text, Is.EqualTo("203.0.113.10:7777"));
            Assert.That(attributes["mode"].Text, Is.EqualTo("tdm"));
            Assert.That(attributes["version"].Text, Is.EqualTo("0.3.0"));
            Assert.That(attributes["players"].IsText, Is.False);
            Assert.That(attributes["players"].Number, Is.EqualTo(2));
        }

        [Test]
        public void VersionMatchesTheBucket()
        {
            var attributes = ByKey(ServerLobby.Attributes("203.0.113.10:7777", " ", 0));

            Assert.That(attributes["version"].Text, Is.EqualTo("0"));
            Assert.That(ServerLobby.BucketId(" "), Is.EqualTo("shooter-server:0"));
            Assert.That(ServerLobby.BucketId("0.3.0"), Is.EqualTo("shooter-server:0.3.0"));
        }

        [Test]
        public void AUdpQuickMatchConnectsToTheServerAddress()
        {
            var net = ServerLobby.ConnectOptions(Lobby("203.0.113.10:7790"));

            Assert.That(net.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(net.Transport, Is.EqualTo(NetTransportKind.Udp));
            Assert.That(net.Address, Is.EqualTo("203.0.113.10"));
            Assert.That(net.Port, Is.EqualTo(7790));
            Assert.That(net.EosServerId, Is.Null);
        }

        [TestCase(null)]
        [TestCase("203.0.113.10")]
        [TestCase("0.0.0.0:7777")]
        [TestCase("server.example:7777")]
        public void AUdpQuickMatchRejectsAServerAddressThatIsNotIPv4AndPort(string endpoint)
        {
            Assert.Throws<ArgumentException>(() => ServerLobby.ConnectOptions(Lobby(endpoint)));
            Assert.Throws<ArgumentNullException>(() => ServerLobby.ConnectOptions(null));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentException>(() => ServerLobby.Attributes(" ", "0.3.0", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerLobby.PlayersAttribute(-1));
        }
    }
}

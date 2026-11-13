using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerLobbyTests
    {
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
            Assert.That(ServerLobby.BucketId(" "), Is.EqualTo("fps-server:0"));
            Assert.That(ServerLobby.BucketId("0.3.0"), Is.EqualTo("fps-server:0.3.0"));
        }

        [Test]
        public void ServerBucketIsSeparateFromHostLobbies()
        {
            Assert.That(ServerLobby.BucketId("0.3.0"), Is.Not.EqualTo(LobbyLaunchOptions.BucketId("0.3.0")));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentException>(() => ServerLobby.Attributes(" ", "0.3.0", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerLobby.PlayersAttribute(-1));
        }

        [Test]
        public void MaxPlayersFitsALobby()
        {
            Assert.That(ServerLobby.MaxPlayers, Is.InRange(LobbyLaunchOptions.MinMaxPlayers, LobbyLaunchOptions.MaxMaxPlayers));
        }
    }
}

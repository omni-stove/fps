using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerListingTests
    {
        private const string _ownerId = "0123456789abcdef0123456789abcdef";
        private const string _version = "0.3.0";

        private static LobbyInfo Lobby(
            string id, int players, string version = _version, NetMatchSettings match = null, string region = null, string map = null)
        {
            return new LobbyInfo(
                id,
                _ownerId,
                ServerLobby.MaxMembers(NetMatchSettings.DefaultMaxPlayers),
                new[] { _ownerId },
                ServerLobby.Attributes("203.0.113.10:7777", version, players, match ?? NetMatchSettings.Default, null, region, map));
        }

        [Test]
        public void APublicServerIsListedWithItsRegionModeMapAndPlayers()
        {
            Assert.That(ServerListing.TryFrom(Lobby("l1", 3, region: "europe", map: "harbor"), _version, out var listing), Is.True);

            Assert.That(listing.Id, Is.EqualTo("l1"));
            Assert.That(listing.Endpoint, Is.EqualTo("203.0.113.10:7777"));
            Assert.That(listing.Address, Is.EqualTo("203.0.113.10"));
            Assert.That(listing.Port, Is.EqualTo(7777));
            Assert.That(listing.Region, Is.EqualTo("europe"));
            Assert.That(listing.Mode, Is.EqualTo(ServerLobby.TeamDeathmatchMode));
            Assert.That(listing.Map, Is.EqualTo("harbor"));
            Assert.That(listing.Players, Is.EqualTo(3));
            Assert.That(listing.Capacity, Is.EqualTo(NetMatchSettings.DefaultMaxPlayers), "the server's own voice member is not a player slot");
            Assert.That(listing.FreeSlots, Is.EqualTo(5));
            Assert.That(listing.PingMilliseconds, Is.Null, "the ping is measured after the search");
            Assert.That(listing.IsFull, Is.False);
            Assert.That(listing.IsEmpty, Is.False);
        }

        [Test]
        public void AServerWithoutARegionOrMapIsListedWithout()
        {
            Assert.That(ServerListing.TryFrom(Lobby("l1", 0), _version, out var listing), Is.True);

            Assert.That(listing.Region, Is.Null);
            Assert.That(listing.Map, Is.Null);
            Assert.That(listing.IsEmpty, Is.True);
        }

        [Test]
        public void AFullServerIsListedButCannotTakeAnyone()
        {
            Assert.That(ServerListing.TryFrom(Lobby("l1", NetMatchSettings.DefaultMaxPlayers), _version, out var listing), Is.True);

            Assert.That(listing.IsFull, Is.True);
            Assert.That(listing.CanTake(1), Is.False);
        }

        [Test]
        public void APartyNeedsRoomForEveryone()
        {
            ServerListing.TryFrom(Lobby("l1", 6), _version, out var listing);

            Assert.That(listing.CanTake(2), Is.True);
            Assert.That(listing.CanTake(3), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => listing.CanTake(0));
        }

        [Test]
        public void PrivateOtherVersionAndBrokenServersAreNotListed()
        {
            var privateMatch = new NetMatchSettings(privateMatch: true, joinCode: "ABC234");
            Assert.That(ServerListing.TryFrom(Lobby("l1", 0, match: privateMatch), _version, out var listing), Is.False);
            Assert.That(listing, Is.Null);
            Assert.That(ServerListing.TryFrom(Lobby("l2", 0, version: "0.2.0"), _version, out _), Is.False);

            var noAddress = new LobbyInfo("l3", _ownerId, 9, new[] { _ownerId }, new List<LobbyAttribute>
            {
                LobbyAttribute.OfText(ServerLobby.VersionKey, _version),
                LobbyAttribute.OfNumber(ServerLobby.PlayersKey, 0),
            });
            Assert.That(ServerListing.TryFrom(noAddress, _version, out _), Is.False);

            var noPlayers = new LobbyInfo("l4", _ownerId, 9, new[] { _ownerId }, new List<LobbyAttribute>
            {
                LobbyAttribute.OfText(ServerLobby.ServerKey, "203.0.113.10:7777"),
                LobbyAttribute.OfText(ServerLobby.VersionKey, _version),
            });
            Assert.That(ServerListing.TryFrom(noPlayers, _version, out _), Is.False);
        }

        [Test]
        public void WithPingKeepsTheRestAndCompares()
        {
            ServerListing.TryFrom(Lobby("l1", 2, region: "asia"), _version, out var listing);

            var pinged = listing.WithPing(42);

            Assert.That(pinged.PingMilliseconds, Is.EqualTo(42));
            Assert.That(pinged.Region, Is.EqualTo("asia"));
            Assert.That(pinged.Players, Is.EqualTo(2));
            Assert.That(pinged, Is.Not.EqualTo(listing));
            Assert.That(pinged.WithPing(null), Is.EqualTo(listing));
            Assert.That(pinged.ToString(), Is.EqualTo("id=l1 server=203.0.113.10:7777 region=asia mode=tdm map=none players=2/8 ping=42ms"));
        }

        [Test]
        public void RejectsInconsistentValues()
        {
            Assert.Throws<ArgumentException>(() => new ServerListing(" ", "203.0.113.10:7777", null, null, null, 0, 8, null));
            Assert.Throws<ArgumentException>(() => new ServerListing("l1", "localhost", null, null, null, 0, 8, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerListing("l1", "203.0.113.10:7777", null, null, null, -1, 8, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerListing("l1", "203.0.113.10:7777", null, null, null, 0, 0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerListing("l1", "203.0.113.10:7777", null, null, null, 0, 8, -1));
            Assert.Throws<ArgumentNullException>(() => ServerListing.TryFrom(null, _version, out _));
        }

        [Test]
        public void TheMapAttributeIsAddedOnlyWhenGivenAndMustBeAMapName()
        {
            var withMap = Lobby("l1", 0, map: "harbor");
            Assert.That(withMap.Map, Is.EqualTo("harbor"));
            Assert.That(withMap.Mode, Is.EqualTo(ServerLobby.TeamDeathmatchMode));
            Assert.That(Lobby("l2", 0).Map, Is.Null);

            Assert.That(ServerLobby.MapAttribute("quarry").Key, Is.EqualTo(ServerLobby.MapKey));
            Assert.That(ServerLobby.MapAttribute("quarry").Text, Is.EqualTo("quarry"));
            Assert.Throws<ArgumentException>(() => ServerLobby.MapAttribute("harbor at night"));
            Assert.Throws<ArgumentException>(() => ServerLobby.MapAttribute(null));
            Assert.Throws<ArgumentException>(
                () => ServerLobby.Attributes("203.0.113.10:7777", _version, 0, NetMatchSettings.Default, null, null, "../harbor"));
        }

        [Test]
        public void IsListableTakesFullButNotPrivateServers()
        {
            Assert.That(LobbySelector.IsListable(Lobby("l1", NetMatchSettings.DefaultMaxPlayers), _version), Is.True);
            Assert.That(LobbySelector.IsListable(Lobby("l2", 0, version: "9.9.9"), _version), Is.False);
            Assert.That(
                LobbySelector.IsListable(Lobby("l3", 0, match: new NetMatchSettings(privateMatch: true, joinCode: "ABC234")), _version),
                Is.False);
            Assert.Throws<ArgumentNullException>(() => LobbySelector.IsListable(null, _version));
        }

        [Test]
        public void TheStatusCopiesTheListAndFindsById()
        {
            ServerListing.TryFrom(Lobby("l1", 1), _version, out var first);
            ServerListing.TryFrom(Lobby("l2", 2), _version, out var second);
            var source = new List<ServerListing> { first, null, second };

            var status = new ServerBrowserStatus(ServerBrowserStage.Ready, source);
            source.Clear();

            Assert.That(status.Servers, Has.Count.EqualTo(2));
            Assert.That(status.Find("l2"), Is.SameAs(second));
            Assert.That(status.Find("l9"), Is.Null);
            Assert.That(status, Is.EqualTo(new ServerBrowserStatus(ServerBrowserStage.Ready, new[] { first, second })));
            Assert.That(status, Is.Not.EqualTo(new ServerBrowserStatus(ServerBrowserStage.Loading, new[] { first, second })));
            Assert.That(default(ServerBrowserStatus).Servers, Is.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerBrowserStatus((ServerBrowserStage)9, null));
        }
    }
}

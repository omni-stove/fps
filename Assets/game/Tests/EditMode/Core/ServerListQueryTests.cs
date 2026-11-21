using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerListQueryTests
    {
        private static ServerListing Server(
            string id, int players, int? ping, string region = "europe", string mode = "tdm", string map = "harbor", int capacity = 8)
        {
            return new ServerListing(id, "203.0.113.1:7777", region, mode, map, players, capacity, ping);
        }

        private static List<string> Ids(IReadOnlyList<ServerListing> listings)
        {
            var ids = new List<string>();
            foreach (var listing in listings)
            {
                ids.Add(listing.Id);
            }

            return ids;
        }

        private static readonly ServerListing[] _servers =
        {
            Server("a", 3, 80, "europe", "tdm", "harbor"),
            Server("b", 8, 20, "asia", "tdm", "quarry"),
            Server("c", 0, 45, "europe", "ctf", "quarry"),
            Server("d", 5, null, null, "tdm", null),
            Server("e", 5, 45, "us-east", "ctf", "harbor"),
        };

        private static readonly string[] _idsByPing = { "b", "e", "c", "a", "d" };
        private static readonly string[] _idsByPingReversed = { "a", "e", "c", "b", "d" };
        private static readonly string[] _idsByPlayers = { "b", "e", "d", "a", "c" };
        private static readonly string[] _idsByPlayersReversed = { "c", "a", "e", "d", "b" };
        private static readonly string[] _idsByRegion = { "b", "c", "a", "e", "d" };
        private static readonly string[] _idsByRegionReversed = { "e", "c", "a", "b", "d" };
        private static readonly string[] _idsByMode = { "e", "c", "b", "a", "d" };
        private static readonly string[] _idsByMap = { "e", "a", "b", "c", "d" };
        private static readonly string[] _idsXY = { "x", "y" };
        private static readonly string[] _idsEurope = { "c", "a" };
        private static readonly string[] _idsCtf = { "e", "c" };
        private static readonly string[] _idsQuarry = { "b", "c" };
        private static readonly string[] _idsEuropeCtfQuarry = { "c" };
        private static readonly string[] _idsNotFull = { "e", "c", "a", "d" };
        private static readonly string[] _idsNotEmpty = { "b", "e", "a", "d" };
        private static readonly string[] _idsNotFullNorEmpty = { "e", "a", "d" };
        private static readonly string[] _idsUnder45Ping = { "b", "e", "c", "d" };
        private static readonly string[] _idsPingZero = { "d" };
        private static readonly string[] _regionChoices = { "asia", "europe", "us-east" };
        private static readonly string[] _modeChoices = { "ctf", "tdm" };
        private static readonly string[] _mapChoices = { "harbor", "quarry" };
        private static readonly string[] _idsA = { "a" };

        [Test]
        public void TheDefaultShowsEveryServerByLowestPingWithUnmeasuredLast()
        {
            var shown = ServerListQuery.Default.Apply(_servers);

            // c と e は同じ 45ms なので、人数の多い e が先
            Assert.That(Ids(shown), Is.EqualTo(_idsByPing));
        }

        [Test]
        public void ReversingThePingKeepsTheUnmeasuredServerLast()
        {
            var shown = ServerListQuery.Default.WithSort(ServerListSort.Ping, true).Apply(_servers);

            Assert.That(Ids(shown), Is.EqualTo(_idsByPingReversed));
        }

        [Test]
        public void PlayersSortTheBusiestFirstAndBreakTiesByPing()
        {
            var shown = ServerListQuery.Default.WithSort(ServerListSort.Players, false).Apply(_servers);

            // d と e は同じ 5 人なので、ping の測れた e が先
            Assert.That(Ids(shown), Is.EqualTo(_idsByPlayers));
        }

        [Test]
        public void ReversedPlayersSortTheEmptiestFirst()
        {
            var shown = ServerListQuery.Default.WithSort(ServerListSort.Players, true).Apply(_servers);

            Assert.That(Ids(shown), Is.EqualTo(_idsByPlayersReversed));
        }

        [Test]
        public void RegionSortsByNameWithServersWithoutOneLast()
        {
            var shown = ServerListQuery.Default.WithSort(ServerListSort.Region, false).Apply(_servers);

            Assert.That(Ids(shown), Is.EqualTo(_idsByRegion));

            var reversed = ServerListQuery.Default.WithSort(ServerListSort.Region, true).Apply(_servers);

            Assert.That(Ids(reversed), Is.EqualTo(_idsByRegionReversed));
        }

        [Test]
        public void ModeAndMapSortByNameThenPing()
        {
            Assert.That(Ids(ServerListQuery.Default.WithSort(ServerListSort.Mode, false).Apply(_servers)),
                Is.EqualTo(_idsByMode));
            Assert.That(Ids(ServerListQuery.Default.WithSort(ServerListSort.Map, false).Apply(_servers)),
                Is.EqualTo(_idsByMap));
        }

        [Test]
        public void TheSameValuesKeepAStableOrderWhateverTheSearchReturned()
        {
            var first = new[] { Server("y", 2, 30), Server("x", 2, 30) };
            var second = new[] { Server("x", 2, 30), Server("y", 2, 30) };

            Assert.That(Ids(ServerListQuery.Default.Apply(first)), Is.EqualTo(_idsXY));
            Assert.That(Ids(ServerListQuery.Default.Apply(second)), Is.EqualTo(_idsXY));
        }

        [Test]
        public void FiltersByRegionModeAndMap()
        {
            Assert.That(Ids(ServerListQuery.Default.WithRegion("europe").Apply(_servers)), Is.EqualTo(_idsEurope));
            Assert.That(Ids(ServerListQuery.Default.WithMode("ctf").Apply(_servers)), Is.EqualTo(_idsCtf));
            Assert.That(Ids(ServerListQuery.Default.WithMap("quarry").Apply(_servers)), Is.EqualTo(_idsQuarry));
            Assert.That(Ids(ServerListQuery.Default.WithRegion("europe").WithMode("ctf").WithMap("quarry").Apply(_servers)),
                Is.EqualTo(_idsEuropeCtfQuarry));
            Assert.That(ServerListQuery.Default.WithRegion("africa").Apply(_servers), Is.Empty);
        }

        [Test]
        public void ABlankFilterMeansAny()
        {
            var query = ServerListQuery.Default.WithRegion("  ").WithMode(string.Empty).WithMap(null);

            Assert.That(query.Region, Is.Null);
            Assert.That(query.Mode, Is.Null);
            Assert.That(query.Map, Is.Null);
            Assert.That(query.Apply(_servers), Has.Count.EqualTo(_servers.Length));
            Assert.That(query, Is.EqualTo(ServerListQuery.Default));
        }

        [Test]
        public void HidesFullAndEmptyServers()
        {
            Assert.That(Ids(ServerListQuery.Default.WithHideFull(true).Apply(_servers)), Is.EqualTo(_idsNotFull));
            Assert.That(Ids(ServerListQuery.Default.WithHideEmpty(true).Apply(_servers)), Is.EqualTo(_idsNotEmpty));
            Assert.That(Ids(ServerListQuery.Default.WithHideFull(true).WithHideEmpty(true).Apply(_servers)),
                Is.EqualTo(_idsNotFullNorEmpty));
        }

        [Test]
        public void TheMaxPingKeepsUnmeasuredServers()
        {
            var shown = ServerListQuery.Default.WithMaxPing(45).Apply(_servers);

            Assert.That(Ids(shown), Is.EqualTo(_idsUnder45Ping));
            Assert.That(Ids(ServerListQuery.Default.WithMaxPing(0).Apply(_servers)), Is.EqualTo(_idsPingZero));
        }

        [Test]
        public void TogglingTheSameSortReversesItAndAnotherSortStartsInItsUsualDirection()
        {
            var byPlayers = ServerListQuery.Default.ToggleSort(ServerListSort.Players);
            Assert.That(byPlayers.Sort, Is.EqualTo(ServerListSort.Players));
            Assert.That(byPlayers.Reversed, Is.False);

            var reversed = byPlayers.ToggleSort(ServerListSort.Players);
            Assert.That(reversed.Reversed, Is.True);

            var byMap = reversed.ToggleSort(ServerListSort.Map);
            Assert.That(byMap.Sort, Is.EqualTo(ServerListSort.Map));
            Assert.That(byMap.Reversed, Is.False);

            Assert.That(reversed.ToggleSort(ServerListSort.Players).Reversed, Is.False);
        }

        [Test]
        public void WithKeepsTheOtherConditions()
        {
            var query = new ServerListQuery("europe", "tdm", "harbor", true, true, 100, ServerListSort.Map, true).WithRegion("asia");

            Assert.That(query.Region, Is.EqualTo("asia"));
            Assert.That(query.Mode, Is.EqualTo("tdm"));
            Assert.That(query.Map, Is.EqualTo("harbor"));
            Assert.That(query.HideFull, Is.True);
            Assert.That(query.HideEmpty, Is.True);
            Assert.That(query.MaxPingMilliseconds, Is.EqualTo(100));
            Assert.That(query.Sort, Is.EqualTo(ServerListSort.Map));
            Assert.That(query.Reversed, Is.True);
            Assert.That(query.ToString(), Is.EqualTo("region=asia mode=tdm map=harbor hide-full hide-empty max-ping=100ms sort=Map reversed"));
        }

        [Test]
        public void TheChoicesAreTheDistinctNamesInOrder()
        {
            Assert.That(ServerListQuery.Regions(_servers), Is.EqualTo(_regionChoices));
            Assert.That(ServerListQuery.Modes(_servers), Is.EqualTo(_modeChoices));
            Assert.That(ServerListQuery.Maps(_servers), Is.EqualTo(_mapChoices));
            Assert.That(ServerListQuery.Regions(Array.Empty<ServerListing>()), Is.Empty);
        }

        [Test]
        public void ApplySkipsMissingRowsAndRejectsBadArguments()
        {
            Assert.That(Ids(ServerListQuery.Default.Apply(new[] { null, Server("a", 1, 10) })), Is.EqualTo(_idsA));
            Assert.Throws<ArgumentNullException>(() => ServerListQuery.Default.Apply(null));
            Assert.Throws<ArgumentNullException>(() => ServerListQuery.Default.Matches(null));
            Assert.Throws<ArgumentNullException>(() => ServerListQuery.Regions(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerListQuery(maxPingMilliseconds: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerListQuery(sort: (ServerListSort)99));
        }
    }
}

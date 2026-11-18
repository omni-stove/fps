using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LeaderboardJsonTests
    {
        private const string _source = "leaderboards.json";

        private static readonly string[] _expectedItemNames = { "boards", "pageSize", "refreshSeconds" };
        private static readonly string[] _expectedBoardItemNames = { "id", "nameKey", "stat", "aggregation", "statName" };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = LeaderboardJson.Parse(
                "{\"boards\": [{\"id\": \"best-match\", \"nameKey\": \"lb.best\", \"stat\": \"Kills\", \"aggregation\": \"Max\", \"statName\": \"KILLS_MATCH\"},"
                + " {\"id\": \"headshots\", \"stat\": \"Headshots\"}], \"pageSize\": 100, \"refreshSeconds\": 30}",
                _source);

            Assert.That(spec.Boards.Count, Is.EqualTo(2));
            Assert.That(spec.Boards[0].Id, Is.EqualTo("best-match"));
            Assert.That(spec.Boards[0].DisplayNameKey, Is.EqualTo("lb.best"));
            Assert.That(spec.Boards[0].Stat, Is.EqualTo(PlayerStat.Kills));
            Assert.That(spec.Boards[0].Aggregation, Is.EqualTo(LeaderboardAggregation.Max));
            Assert.That(spec.Boards[0].StatName, Is.EqualTo("KILLS_MATCH"));
            Assert.That(spec.Boards[1].Aggregation, Is.EqualTo(LeaderboardAggregation.Sum));
            Assert.That(spec.Boards[1].StatName, Is.EqualTo("Headshots"), "the stat name falls back to the stat");
            Assert.That(spec.PageSize, Is.EqualTo(100));
            Assert.That(spec.RefreshSeconds, Is.EqualTo(30f));
            Assert.That(spec.Find("headshots"), Is.SameAs(spec.Boards[1]));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var spec = LeaderboardJson.Parse("{}", _source);

            Assert.That(spec.Boards, Is.SameAs(LeaderboardSpec.DefaultBoards));
            Assert.That(spec.Boards.Count, Is.EqualTo(2));
            Assert.That(spec.Boards[0].Id, Is.EqualTo("kills"));
            Assert.That(spec.Boards[0].DisplayNameKey, Is.EqualTo("leaderboard.kills"));
            Assert.That(spec.Boards[0].Stat, Is.EqualTo(PlayerStat.Kills));
            Assert.That(spec.Boards[0].Aggregation, Is.EqualTo(LeaderboardAggregation.Sum));
            Assert.That(spec.Boards[0].StatName, Is.EqualTo("Kills"));
            Assert.That(spec.Boards[1].Id, Is.EqualTo("wins"));
            Assert.That(spec.Boards[1].Stat, Is.EqualTo(PlayerStat.MatchesWon));
            Assert.That(spec.Boards[1].StatName, Is.EqualTo("MatchesWon"));
            Assert.That(spec.PageSize, Is.EqualTo(50));
            Assert.That(spec.RefreshSeconds, Is.EqualTo(60f));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(LeaderboardJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(LeaderboardJson.BoardItemNames, Is.EqualTo(_expectedBoardItemNames));
            Assert.That(LeaderboardJson.ResourcePath, Is.EqualTo("Tuning/leaderboards"));
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var pageSize = Reject("{\"pageSize\": 0}");
            var bigPage = Reject("{\"pageSize\": 1001}");
            var refresh = Reject("{\"refreshSeconds\": 0}");
            var aggregation = Reject("{\"boards\": [{\"id\": \"a\", \"stat\": \"Kills\", \"aggregation\": \"Average\"}]}");
            var noId = Reject("{\"boards\": [{\"stat\": \"Kills\"}]}");
            var twice = Reject("{\"boards\": [{\"id\": \"a\", \"stat\": \"Kills\"}, {\"id\": \"a\", \"stat\": \"Deaths\"}]}");

            Assert.That(pageSize.Item, Is.EqualTo("pageSize"));
            Assert.That(pageSize.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(bigPage.Item, Is.EqualTo("pageSize"));
            Assert.That(bigPage.Message, Does.Contain("between 1 and 1000"));
            Assert.That(refresh.Item, Is.EqualTo("refreshSeconds"));
            Assert.That(aggregation.Item, Is.EqualTo("boards[0].aggregation"));
            Assert.That(aggregation.Message, Does.Contain("Sum, Max, Min, Latest"));
            Assert.That(noId.Item, Is.EqualTo("boards[0].id"));
            Assert.That(twice.Item, Is.EqualTo("boards"));
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => LeaderboardJson.Parse(json, _source));
        }
    }
}

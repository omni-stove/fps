using Fps.Core;
using NUnit.Framework;

namespace Fps.Net.Tests
{
    /// <summary>試合の同期に使う構造体 (NetworkVariable / NetworkList の中身) の値の扱い。</summary>
    public sealed class NetMatchSyncTypesTests
    {
        [Test]
        public void MatchStatusRoundTripsWinner()
        {
            Assert.That(new NetMatchStatus(GameModeState.Ended, Team.Alpha).Winner, Is.EqualTo(Team.Alpha));
            Assert.That(new NetMatchStatus(GameModeState.Ended, Team.Bravo).Winner, Is.EqualTo(Team.Bravo));
            Assert.That(new NetMatchStatus(GameModeState.Ended, null).Winner, Is.Null);
            Assert.That(new NetMatchStatus(GameModeState.Waiting, null).WinnerCode, Is.EqualTo(NetMatchStatus.NoWinner));
        }

        [Test]
        public void MatchStatusEqualityUsesStateAndWinner()
        {
            var playing = new NetMatchStatus(GameModeState.Playing, null);

            Assert.That(playing.Equals(new NetMatchStatus(GameModeState.Playing, null)), Is.True);
            Assert.That(playing.Equals(new NetMatchStatus(GameModeState.Ended, null)), Is.False);
            Assert.That(
                new NetMatchStatus(GameModeState.Ended, Team.Alpha).Equals(new NetMatchStatus(GameModeState.Ended, Team.Bravo)),
                Is.False);
        }

        [Test]
        public void PlayerStatsEqualityUsesAllFields()
        {
            var stats = new NetPlayerStats(3, Team.Bravo, 2, 1);

            Assert.That(stats.Equals(new NetPlayerStats(3, Team.Bravo, 2, 1)), Is.True);
            Assert.That(stats.GetHashCode(), Is.EqualTo(new NetPlayerStats(3, Team.Bravo, 2, 1).GetHashCode()));
            Assert.That(stats.Equals(new NetPlayerStats(4, Team.Bravo, 2, 1)), Is.False);
            Assert.That(stats.Equals(new NetPlayerStats(3, Team.Alpha, 2, 1)), Is.False);
            Assert.That(stats.Equals(new NetPlayerStats(3, Team.Bravo, 3, 1)), Is.False);
            Assert.That(stats.Equals(new NetPlayerStats(3, Team.Bravo, 2, 2)), Is.False);
        }

        [TestCase(NetSpawnPoint.SpawnTeam.Any, null)]
        [TestCase(NetSpawnPoint.SpawnTeam.Alpha, Team.Alpha)]
        [TestCase(NetSpawnPoint.SpawnTeam.Bravo, Team.Bravo)]
        public void SpawnTeamMapsToCoreTeam(NetSpawnPoint.SpawnTeam spawnTeam, Team? expected)
        {
            Assert.That(NetSpawnPoint.ToCoreTeam(spawnTeam), Is.EqualTo(expected));
        }
    }
}

using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientFreeForAllStatusTests
    {
        [Test]
        public void AnotherObjectiveHasNoFreeForAllStatus()
        {
            var snapshot = Snapshot(MatchObjective.None, pointsPerKill: 1, (1, 3));

            Assert.That(ClientFreeForAllStatus.From(snapshot, 1), Is.Null);
        }

        [Test]
        public void ScoresEveryPlayerAsTheirOwnSideInIdOrder()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll, pointsPerKill: 2, (7, 1), (3, 4), (5, 0));
            snapshot.Match.RemainingSeconds = 42;

            var status = ClientFreeForAllStatus.From(snapshot, 5);

            Assert.That(status.RemainingSeconds, Is.EqualTo(42f));
            Assert.That(status.SideScores, Is.EqualTo(new[]
            {
                new SideScore(new Side(3), 8d),
                new SideScore(new Side(5), 0d),
                new SideScore(new Side(7), 2d),
            }));
            Assert.That(status.OwnSide, Is.EqualTo(new Side(5)));
            Assert.That(status.RespawnSeconds, Is.Null, "the snapshot carries no owner part");
            Assert.That(status.IsEnded, Is.False);
            Assert.That(status.Winner, Is.Null);
            Assert.That(status.Detail, Is.EqualTo(new FreeForAllStanding(3, 3, 0, -8)));
        }

        [Test]
        public void TheLeaderSeesTheMarginOverSecond()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll, pointsPerKill: 1, (1, 5), (2, 3), (3, 1));

            var status = ClientFreeForAllStatus.From(snapshot, 1);

            Assert.That(status.Detail, Is.EqualTo(new FreeForAllStanding(1, 3, 5, 2)));
        }

        [Test]
        public void TiedPlayersShareTheRank()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll, pointsPerKill: 1, (1, 2), (2, 2), (3, 1));

            var status = ClientFreeForAllStatus.From(snapshot, 3);

            Assert.That(((FreeForAllStanding)status.Detail).Rank, Is.EqualTo(3));
            Assert.That(ClientFreeForAllStatus.From(snapshot, 1).Detail, Is.EqualTo(new FreeForAllStanding(1, 3, 2, 0)));
        }

        [Test]
        public void AloneThereIsNoMargin()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll, pointsPerKill: 1, (4, 0));

            var status = ClientFreeForAllStatus.From(snapshot, 4);

            Assert.That(status.Detail, Is.EqualTo(new FreeForAllStanding(1, 1, 0, null)));
        }

        [Test]
        public void ASpectatorHasNoOwnSideOrStanding()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll, pointsPerKill: 1, (1, 2), (2, 0));

            var status = ClientFreeForAllStatus.From(snapshot, 9);

            Assert.That(status.OwnSide, Is.Null);
            Assert.That(status.Detail, Is.Null);
            Assert.That(status.SideScores.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheSoleLeaderWinsAnEndedMatch()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll, pointsPerKill: 1, (1, 15), (2, 9));
            snapshot.Match.State = GameModeState.Ended;

            var status = ClientFreeForAllStatus.From(snapshot, 2);

            Assert.That(status.IsEnded, Is.True);
            Assert.That(status.Winner, Is.EqualTo(new Side(1)));
            Assert.That(status.Detail, Is.EqualTo(new FreeForAllStanding(2, 2, 9, -6)));
        }

        [Test]
        public void AnEndedMatchWithATiedLeadIsADraw()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll, pointsPerKill: 1, (1, 4), (2, 4), (3, 1));
            snapshot.Match.State = GameModeState.Ended;

            var status = ClientFreeForAllStatus.From(snapshot, 3);

            Assert.That(status.IsEnded, Is.True);
            Assert.That(status.Winner, Is.Null);
        }

        [Test]
        public void APlayingMatchHasNoWinnerYet()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll, pointsPerKill: 1, (1, 4), (2, 1));
            snapshot.Match.State = GameModeState.Playing;

            Assert.That(ClientFreeForAllStatus.From(snapshot, 1).Winner, Is.Null);
        }

        [Test]
        public void AMissingPointsPerKillCountsKillsAsPoints()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll, pointsPerKill: 0, (1, 3));

            var status = ClientFreeForAllStatus.From(snapshot, 1);

            Assert.That(ClientFreeForAllStatus.PointsPerKill(snapshot.Match), Is.EqualTo(1));
            Assert.That(status.SideScores[0].Score, Is.EqualTo(3d));
        }

        [Test]
        public void TheScoreStopsAtTheLargestInt()
        {
            Assert.That(ClientFreeForAllStatus.ScoreOf(int.MaxValue, 3), Is.EqualTo(int.MaxValue));
            Assert.That(ClientFreeForAllStatus.ScoreOf(4, 3), Is.EqualTo(12));
            Assert.That(ClientFreeForAllStatus.ScoreOf(-1, 3), Is.Zero);
        }

        [Test]
        public void TheOwnersRespawnCountdownIsTheRespawnSeconds()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll, pointsPerKill: 1, (1, 2), (2, 0));
            snapshot.HasOwner = true;
            snapshot.Owner.RespawnSeconds = 3;

            var status = ClientFreeForAllStatus.From(snapshot, 2);

            Assert.That(status.RespawnSeconds, Is.EqualTo(3f));
        }

        [Test]
        public void AZeroCountdownIsStillShown()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll, pointsPerKill: 1, (1, 2));
            snapshot.HasOwner = true;
            snapshot.Owner.RespawnSeconds = 0;

            Assert.That(ClientFreeForAllStatus.From(snapshot, 1).RespawnSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void ALivingPlayerHasNoRespawnSeconds()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll, pointsPerKill: 1, (1, 2));
            snapshot.HasOwner = true;
            snapshot.Owner.RespawnSeconds = null;

            Assert.That(ClientFreeForAllStatus.From(snapshot, 1).RespawnSeconds, Is.Null);
        }

        [Test]
        public void ASpectatorHasNoRespawnSeconds()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll, pointsPerKill: 1, (1, 2));
            snapshot.HasOwner = true;
            snapshot.Owner.RespawnSeconds = 4;

            Assert.That(ClientFreeForAllStatus.From(snapshot, 9).RespawnSeconds, Is.Null);
        }

        [Test]
        public void ANegativeCountdownIsShownAsZero()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll, pointsPerKill: 1, (1, 2));
            snapshot.HasOwner = true;
            snapshot.Owner.RespawnSeconds = -2;

            Assert.That(ClientFreeForAllStatus.From(snapshot, 1).RespawnSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void TheSnapshotIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => ClientFreeForAllStatus.From(null, 1));
        }

        private static SnapshotMessage Snapshot(MatchObjective objective, int pointsPerKill, params (int PlayerId, int Kills)[] players)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.Objective = objective;
            snapshot.Match.AlphaPoints = pointsPerKill;
            snapshot.Match.BravoPoints = FreeForAllMode.DefaultScoreLimit;
            snapshot.Match.State = GameModeState.Playing;
            foreach (var player in players)
            {
                ref var row = ref snapshot.Players.Add();
                row.PlayerId = player.PlayerId;
                row.Kills = player.Kills;
            }

            return snapshot;
        }
    }
}

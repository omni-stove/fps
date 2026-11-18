using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientGunGameStatusTests
    {
        private const string _rifleId = "5d2c8e1a-7b4f-4a39-9e60-1c3b5a7d9f24";
        private const string _launcherId = "e8a14b6c-2d93-4f75-8c01-6b9e3d5a7f12";
        private const string _handgunId = "9b7e3f20-4c1a-4d86-b5e9-0a2c4e6f8d13";

        [Test]
        public void APlayingMatchShowsTheTiersKillsAndClockLikeTheOfflineRunner()
        {
            var status = new ClientGunGameStatus(Ladder());
            var snapshot = Snapshot(GameModeState.Playing, 120);
            AddPlayer(snapshot, 1, tier: 1, kills: 3);
            AddPlayer(snapshot, 2, tier: 0, kills: 1);

            var shown = status.Update(snapshot, 1);

            Assert.That(shown.RemainingSeconds, Is.EqualTo(120f));
            Assert.That(shown.SideScores, Is.EqualTo(new[] { new SideScore(new Side(1), 3d), new SideScore(new Side(2), 1d) }));
            Assert.That(shown.OwnSide, Is.EqualTo(new Side(1)));
            Assert.That(shown.RespawnSeconds, Is.Null);
            Assert.That(shown.IsEnded, Is.False);
            Assert.That(shown.Winner, Is.Null);
            var detail = (GunGameStatus)shown.Detail;
            Assert.That(detail.LocalPlayerId, Is.EqualTo(1));
            Assert.That(detail.GunGame, Is.SameAs(status.GunGame));
            Assert.That(detail.GunGame.GetTier(1), Is.EqualTo(1));
            Assert.That(detail.GunGame.GetWeapon(1).WeaponId, Is.EqualTo(_launcherId));
            Assert.That(detail.GunGame.GetWeapon(1).NameKey, Is.EqualTo("weapon.launcher"));
            Assert.That(detail.GunGame.GetNextWeapon(1)?.WeaponId, Is.EqualTo(_handgunId));
            Assert.That(detail.GunGame.GetTier(2), Is.Zero);
            Assert.That(detail.GunGame.FinisherId, Is.Null);
            Assert.That(status.LadderMismatch, Is.False);
        }

        [Test]
        public void TheFinisherWinsWhenTheMatchEnds()
        {
            var status = new ClientGunGameStatus(Ladder());
            var playing = Snapshot(GameModeState.Playing, 30);
            AddPlayer(playing, 1, tier: 2, kills: 5);
            AddPlayer(playing, 2, tier: 3, kills: 4);

            var beforeEnd = status.Update(playing, 1);
            Assert.That(beforeEnd.IsEnded, Is.False);
            Assert.That(beforeEnd.Winner, Is.Null, "the winner is shown only once the match has ended");
            Assert.That(status.GunGame.FinisherId, Is.EqualTo(2));

            var ended = Snapshot(GameModeState.Ended, 29);
            AddPlayer(ended, 1, tier: 2, kills: 5);
            AddPlayer(ended, 2, tier: 3, kills: 4);
            var shown = status.Update(ended, 1);

            Assert.That(shown.IsEnded, Is.True);
            Assert.That(shown.Winner, Is.EqualTo(new Side(2)), "the finisher wins even with fewer kills");
            Assert.That(((GunGameStatus)shown.Detail).GunGame.GetNextWeapon(2), Is.Null);
        }

        [Test]
        public void AtTimeUpTheHighestTierWinsAndATieIsADraw()
        {
            var status = new ClientGunGameStatus(Ladder());
            var ended = Snapshot(GameModeState.Ended, 0);
            AddPlayer(ended, 4, tier: 1, kills: 2);
            AddPlayer(ended, 7, tier: 2, kills: 1);

            Assert.That(status.Update(ended, 4).Winner, Is.EqualTo(new Side(7)));

            var tied = Snapshot(GameModeState.Ended, 0);
            AddPlayer(tied, 4, tier: 2, kills: 2);
            AddPlayer(tied, 7, tier: 2, kills: 1);

            var shown = status.Update(tied, 4);
            Assert.That(shown.IsEnded, Is.True);
            Assert.That(shown.Winner, Is.Null);
        }

        [Test]
        public void APlayerWhoLeftIsNoLongerRanked()
        {
            var status = new ClientGunGameStatus(Ladder());
            var both = Snapshot(GameModeState.Playing, 60);
            AddPlayer(both, 1, tier: 0, kills: 0);
            AddPlayer(both, 2, tier: 2, kills: 2);
            status.Update(both, 1);

            var alone = Snapshot(GameModeState.Ended, 0);
            AddPlayer(alone, 1, tier: 1, kills: 1);
            var shown = status.Update(alone, 1);

            Assert.That(shown.Winner, Is.EqualTo(new Side(1)));
            Assert.That(status.GunGame.GetTier(2), Is.Zero, "the tiers are copied again from each snapshot");
            Assert.That(shown.SideScores.Count, Is.EqualTo(1));
        }

        [Test]
        public void ASpectatorWithoutARowHasNoOwnSideOrTier()
        {
            var status = new ClientGunGameStatus(Ladder());
            var snapshot = Snapshot(GameModeState.Playing, 60);
            AddPlayer(snapshot, 1, tier: 1, kills: 1);

            var shown = status.Update(snapshot, 9);

            Assert.That(shown.OwnSide, Is.Null);
            Assert.That(((GunGameStatus)shown.Detail).LocalPlayerId, Is.Null);
        }

        [Test]
        public void ATierBeyondTheLadderIsShownAsTheLastAndReported()
        {
            var status = new ClientGunGameStatus(Ladder());
            var snapshot = Snapshot(GameModeState.Playing, 60);
            AddPlayer(snapshot, 1, tier: 5, kills: 5);

            status.Update(snapshot, 1);

            Assert.That(status.LadderMismatch, Is.True);
            Assert.That(status.GunGame.GetTier(1), Is.EqualTo(3));
            Assert.That(status.GunGame.FinisherId, Is.EqualTo(1));

            var fixedSnapshot = Snapshot(GameModeState.Playing, 59);
            AddPlayer(fixedSnapshot, 1, tier: 1, kills: 5);
            status.Update(fixedSnapshot, 1);
            Assert.That(status.LadderMismatch, Is.False);
        }

        [Test]
        public void AnotherObjectiveShowsNoGunGame()
        {
            var status = new ClientGunGameStatus(Ladder());
            var snapshot = Snapshot(GameModeState.Playing, 60);
            snapshot.Match.Objective = MatchObjective.None;
            AddPlayer(snapshot, 1, tier: 1, kills: 1);

            Assert.That(status.Update(snapshot, 1), Is.Null);
        }

        [Test]
        public void TheLadderAndTheSnapshotAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => new ClientGunGameStatus(null));
            Assert.Throws<ArgumentNullException>(() => new ClientGunGameStatus(Ladder()).Update(null, 1));
        }

        private static GunGameLadder Ladder()
        {
            return new GunGameLadder(
                new[]
                {
                    new GunGameTier(_rifleId, "weapon.rifle"),
                    new GunGameTier(_launcherId, "weapon.launcher"),
                    new GunGameTier(_handgunId, "weapon.handgun"),
                },
                null,
                true);
        }

        private static SnapshotMessage Snapshot(GameModeState state, int remainingSeconds)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.State = state;
            snapshot.Match.RemainingSeconds = remainingSeconds;
            snapshot.Match.Objective = MatchObjective.GunGame;
            return snapshot;
        }

        private static void AddPlayer(SnapshotMessage snapshot, int playerId, int tier, int kills)
        {
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = playerId;
            row.GunGameTier = tier;
            row.Kills = kills;
        }
    }
}

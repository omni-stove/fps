using System;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientSearchAndDestroyStatusTests
    {
        private const int _self = 1;
        private const int _enemy = 2;

        private ClientSearchAndDestroyStatus _status;

        [SetUp]
        public void SetUp()
        {
            _status = new ClientSearchAndDestroyStatus(_self);
        }

        [Test]
        public void OtherObjectivesPublishNoStatus()
        {
            var snapshot = Snapshot(RoundPhase.Live, BombState.Carried);
            snapshot.Match.Objective = MatchObjective.Elimination;

            Assert.That(_status.Apply(snapshot), Is.False);
            Assert.That(_status.Status, Is.Null);
        }

        [Test]
        public void ThePreparationIsShownInTheSameShapeAsOffline()
        {
            var snapshot = Snapshot(RoundPhase.Preparing, BombState.Carried, remaining: 4, attacker: Team.Bravo, carrier: _enemy, round: 1, toWin: 3);
            Assert.That(_status.Apply(snapshot), Is.True);

            var status = _status.Status;
            Assert.That(status.RemainingSeconds, Is.EqualTo(4f));
            Assert.That(status.OwnSide, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(status.IsEnded, Is.False);
            Assert.That(status.Winner, Is.Null);
            Assert.That(status.RespawnSeconds, Is.Null);
            Assert.That(status.SideScores.Count, Is.EqualTo(2));
            var detail = (SearchAndDestroyStatus)status.Detail;
            Assert.That(detail.LocalPlayerId, Is.EqualTo(_self));
            var game = detail.Game;
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(game.PreparationRemaining, Is.EqualTo(4f));
            Assert.That(game.RoundNumber, Is.EqualTo(1));
            Assert.That(game.WinsToWin, Is.EqualTo(3), "the server's wins to win is shown");
            Assert.That(game.Attacker, Is.EqualTo(Side.Bravo), "the bomb belongs to the attackers");
            Assert.That(game.Defender, Is.EqualTo(Side.Alpha));
            Assert.That(game.Bomb, Is.EqualTo(BombState.Carried));
            Assert.That(game.CarrierId, Is.EqualTo(_enemy));
            Assert.That(game.HasPlayer(_self), Is.True);
            Assert.That(game.HasPlayer(9), Is.False);
            Assert.That(game.IsAttacker(_self), Is.False);
            Assert.That(game.IsAttacker(_enemy), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => game.IsAttacker(9));
            Assert.That(game.DefuserId, Is.Null);
            Assert.That(game.DefuseFraction, Is.Zero);
            Assert.That(game.LastRoundEnd, Is.Null);
            Assert.That(game.LastRoundWinner, Is.Null);
            Assert.That(game.FuseRemaining, Is.Zero, "the fuse is not shown before the plant");
        }

        [Test]
        public void TheSameSnapshotKeepsTheSameStatus()
        {
            _status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, remaining: 80));
            var first = _status.Status;

            Assert.That(_status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, remaining: 80)), Is.False);
            Assert.That(_status.Status, Is.SameAs(first));

            Assert.That(_status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, remaining: 79)), Is.True);
            Assert.That(_status.Status, Is.Not.SameAs(first));
            Assert.That(Game().RoundRemaining, Is.EqualTo(79f));
        }

        [Test]
        public void TheRoundNumberAndWinsToWinComeFromTheServer()
        {
            _status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, alpha: 1, bravo: 0, round: 4, toWin: 6));

            Assert.That(Game().RoundNumber, Is.EqualTo(4), "drawn rounds count, which the wins alone cannot tell");
            Assert.That(Game().WinsToWin, Is.EqualTo(6));

            Assert.That(_status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, alpha: 1, bravo: 0, round: 5, toWin: 6)), Is.True);
            Assert.That(Game().RoundNumber, Is.EqualTo(5));
        }

        [Test]
        public void ThePlantProgressComesFromTheServer()
        {
            _status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, remaining: 80, activePoint: 1, plant: 0.25f));
            Assert.That(Game().PlantingSite, Is.EqualTo(1));
            Assert.That(Game().PlantedSite, Is.Null);
            Assert.That(Game().PlantFraction, Is.EqualTo(0.25f));

            var shown = _status.Status;
            Assert.That(_status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, remaining: 80, activePoint: 1, plant: 0.255f)), Is.False,
                "the same shown percent keeps the status");
            Assert.That(_status.Status, Is.SameAs(shown));

            Assert.That(_status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, remaining: 80, activePoint: 1, plant: 0.5f)), Is.True);
            Assert.That(Game().PlantFraction, Is.EqualTo(0.5f));

            _status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, remaining: 80, plant: 0.5f));
            Assert.That(Game().PlantingSite, Is.Null);
            Assert.That(Game().PlantFraction, Is.Zero, "nobody plants without a site");
        }

        [Test]
        public void ThePlantedBombShowsItsSiteAndFuse()
        {
            _status.Apply(Snapshot(RoundPhase.Live, BombState.Planted, remaining: 12, activePoint: 0, fuse: 12, carrier: null));

            var game = Game();
            Assert.That(game.Bomb, Is.EqualTo(BombState.Planted));
            Assert.That(game.PlantedSite, Is.EqualTo(0));
            Assert.That(game.PlantingSite, Is.Null);
            Assert.That(game.FuseRemaining, Is.EqualTo(12f));
            Assert.That(game.RoundRemaining, Is.Zero);
            Assert.That(game.CarrierId, Is.Null);
            Assert.That(game.DefuserId, Is.Null);
        }

        [Test]
        public void TheDefuserAndTheDefuseProgressAreShownWhilePlanted()
        {
            _status.Apply(Snapshot(RoundPhase.Live, BombState.Planted, remaining: 12, activePoint: 0, fuse: 12, carrier: null, defuser: _self, defuse: 0.4f));

            Assert.That(Game().DefuserId, Is.EqualTo(_self));
            Assert.That(Game().DefuseFraction, Is.EqualTo(0.4f));

            Assert.That(
                _status.Apply(Snapshot(RoundPhase.Live, BombState.Planted, remaining: 12, activePoint: 0, fuse: 12, carrier: null, defuser: _self, defuse: 0.5f)),
                Is.True);
            Assert.That(Game().DefuseFraction, Is.EqualTo(0.5f));

            _status.Apply(Snapshot(RoundPhase.Live, BombState.Planted, remaining: 12, activePoint: 0, fuse: 12, carrier: null));
            Assert.That(Game().DefuserId, Is.Null, "stopping the defuse clears the defuser");
            Assert.That(Game().DefuseFraction, Is.Zero);
        }

        [Test]
        public void ADefuserIsNotShownOnceTheBombIsDefused()
        {
            _status.Apply(Snapshot(RoundPhase.Live, BombState.Defused, activePoint: 0, carrier: null, defuser: _self, defuse: 1f));

            Assert.That(Game().DefuserId, Is.Null);
            Assert.That(Game().DefuseFraction, Is.Zero);
        }

        [Test]
        public void APlantedBombWithoutASiteIsIgnored()
        {
            _status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, remaining: 80));
            var before = _status.Status;

            Assert.That(_status.Apply(Snapshot(RoundPhase.Live, BombState.Planted, remaining: 12, fuse: 12, carrier: null)), Is.False);

            Assert.That(_status.Status, Is.SameAs(before));
        }

        [Test]
        public void TheLastRoundResultComesFromTheServer()
        {
            _status.Apply(Snapshot(
                RoundPhase.Preparing, BombState.Carried, alpha: 1, round: 2, lastEnd: RoundEndReason.Defused, lastWinner: Team.Bravo));

            Assert.That(Game().LastRoundEnd, Is.EqualTo((RoundEndReason?)RoundEndReason.Defused));
            Assert.That(Game().LastRoundWinner, Is.EqualTo((Side?)Side.Bravo), "the winner is the server's, not guessed from the wins");
        }

        [Test]
        public void ADrawnLastRoundHasAResultWithoutAWinner()
        {
            _status.Apply(Snapshot(RoundPhase.Preparing, BombState.Carried, round: 2, lastEnd: RoundEndReason.TimeUp));

            Assert.That(Game().LastRoundEnd, Is.EqualTo((RoundEndReason?)RoundEndReason.TimeUp));
            Assert.That(Game().LastRoundWinner, Is.Null);
        }

        [Test]
        public void ANewLastRoundResultReplacesTheStatus()
        {
            _status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, round: 1));
            var before = _status.Status;

            Assert.That(_status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, round: 1, lastEnd: RoundEndReason.Detonated, lastWinner: Team.Alpha)), Is.True);

            Assert.That(_status.Status, Is.Not.SameAs(before));
            Assert.That(Game().LastRoundEnd, Is.EqualTo((RoundEndReason?)RoundEndReason.Detonated));
        }

        [Test]
        public void TheEndedMatchShowsTheWinnerAndTheRoundsPlayed()
        {
            _status.Apply(Snapshot(
                RoundPhase.Ended,
                BombState.Defused,
                activePoint: 1,
                alpha: 1,
                bravo: 3,
                state: GameModeState.Ended,
                winner: Team.Bravo,
                carrier: null,
                round: 5,
                lastEnd: RoundEndReason.Defused,
                lastWinner: Team.Bravo));

            var status = _status.Status;
            Assert.That(status.IsEnded, Is.True);
            Assert.That(status.Winner, Is.EqualTo((Side?)Side.Bravo));
            var game = Game();
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Ended));
            Assert.That(game.Winner, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(game.RoundNumber, Is.EqualTo(5));
            Assert.That(game.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(game.GetRoundWins(Side.Bravo), Is.EqualTo(3));
            Assert.That(game.LastRoundEnd, Is.EqualTo((RoundEndReason?)RoundEndReason.Defused));
            Assert.That(game.LastRoundWinner, Is.EqualTo((Side?)Side.Bravo));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.GetRoundWins(new Side(5)));
        }

        [Test]
        public void ASpectatorWithoutARowHasNoOwnSide()
        {
            var snapshot = Snapshot(RoundPhase.Live, BombState.Carried);
            var status = new ClientSearchAndDestroyStatus(7);

            status.Apply(snapshot);

            Assert.That(status.Status.OwnSide, Is.Null);
            Assert.That(((SearchAndDestroyStatus)status.Status.Detail).LocalPlayerId, Is.Null);
        }

        [Test]
        public void LeavingTheModeDropsTheStatus()
        {
            _status.Apply(Snapshot(RoundPhase.Live, BombState.Carried));
            var snapshot = Snapshot(RoundPhase.Live, BombState.Carried);
            snapshot.Match.Objective = MatchObjective.None;

            Assert.That(_status.Apply(snapshot), Is.True);
            Assert.That(_status.Status, Is.Null);
            Assert.That(_status.Reset(), Is.False);
        }

        [Test]
        public void BeforeTheFirstRoundTheAttackersAreTheFirstSide()
        {
            var snapshot = Snapshot(RoundPhase.Waiting, BombState.Carried, round: 0);
            snapshot.Match.HasBomb = false;
            snapshot.ObjectiveItems.Clear();

            _status.Apply(snapshot);

            Assert.That(Game().Phase, Is.EqualTo(RoundPhase.Waiting));
            Assert.That(Game().RoundNumber, Is.Zero);
            Assert.That(Game().Attacker, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void TheStatusNeedsASnapshot()
        {
            Assert.Throws<ArgumentNullException>(() => _status.Apply(null));
        }

        private ISearchAndDestroyView Game()
        {
            return ((SearchAndDestroyStatus)_status.Status.Detail).Game;
        }

        private static SnapshotMessage Snapshot(
            RoundPhase phase,
            BombState bomb,
            int remaining = 60,
            Team attacker = Team.Alpha,
            int? carrier = _self,
            int? activePoint = null,
            int fuse = 0,
            int alpha = 0,
            int bravo = 0,
            GameModeState state = GameModeState.Playing,
            Team? winner = null,
            int round = 1,
            int toWin = 4,
            RoundEndReason? lastEnd = null,
            Team? lastWinner = null,
            float plant = 0f,
            int? defuser = null,
            float defuse = 0f)
        {
            var snapshot = new SnapshotMessage();
            ref var match = ref snapshot.Match;
            match.State = state;
            match.HasWinner = winner.HasValue;
            match.Winner = winner ?? Team.Alpha;
            match.RemainingSeconds = remaining;
            match.Objective = MatchObjective.SearchAndDestroy;
            match.AlphaPoints = alpha;
            match.BravoPoints = bravo;
            match.HasRound = true;
            match.Round = phase;
            match.HasBomb = true;
            match.Bomb = bomb;
            match.HasActivePoint = activePoint.HasValue;
            match.ActivePointIndex = activePoint ?? 0;
            match.ObjectiveRemainingSeconds = fuse;
            match.RoundNumber = round;
            match.RoundsToWin = toWin;
            match.HasLastRound = lastEnd.HasValue;
            match.LastRoundEnd = lastEnd ?? default;
            match.HasLastRoundWinner = lastWinner.HasValue;
            match.LastRoundWinner = lastWinner ?? Team.Alpha;
            match.PlantFraction = plant;
            match.HasDefuser = defuser.HasValue;
            match.DefuserId = defuser ?? 0;
            match.DefuseFraction = defuse;

            ref var self = ref snapshot.Players.Add();
            self.PlayerId = _self;
            self.Team = Team.Alpha;
            ref var enemy = ref snapshot.Players.Add();
            enemy.PlayerId = _enemy;
            enemy.Team = Team.Bravo;

            ref var item = ref snapshot.ObjectiveItems.Add();
            item.ItemId = 0;
            item.Present = ObjectiveItemFields.All;
            item.Kind = ObjectiveItemKind.Bomb;
            item.Team = attacker;
            item.Position = new Vector3(1f, 0f, 2f);
            item.HasCarrier = carrier.HasValue;
            item.CarrierId = carrier ?? 0;
            item.State = carrier.HasValue ? ObjectiveItemState.Carried
                : bomb == BombState.Dropped ? ObjectiveItemState.Dropped
                : ObjectiveItemState.Planted;
            return snapshot;
        }
    }
}

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

        private static readonly SearchAndDestroySpec _spec = new SearchAndDestroySpec(
            siteCount: 2, roundSeconds: 90f, plantSeconds: 2f, defuseSeconds: 4f, fuseSeconds: 20f, winsToWin: 3, preparationSeconds: 5f);

        private ClientSearchAndDestroyStatus _status;

        [SetUp]
        public void SetUp()
        {
            _status = new ClientSearchAndDestroyStatus(_self, _spec);
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
            Assert.That(_status.Apply(Snapshot(RoundPhase.Preparing, BombState.Carried, remaining: 4, attacker: Team.Bravo, carrier: _enemy)), Is.True);

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
            Assert.That(game.WinsToWin, Is.EqualTo(3), "the snapshot has no wins to win, so the configured value is used");
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
            Assert.That(game.LastRoundEnd, Is.Null, "the snapshot does not say how a round ended");
            Assert.That(game.FuseRemaining, Is.EqualTo(20f));
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
        public void ThePlantProgressIsCountedLocallyWhileTheSiteStaysTheSame()
        {
            _status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, remaining: 80, activePoint: 1));
            Assert.That(Game().PlantingSite, Is.EqualTo(1));
            Assert.That(Game().PlantedSite, Is.Null);
            Assert.That(Game().PlantFraction, Is.Zero);

            Assert.That(_status.Tick(0.5f), Is.True);
            Assert.That(Game().PlantFraction, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(_status.Tick(0f), Is.False);

            Assert.That(_status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, remaining: 79, activePoint: 1)), Is.True);
            Assert.That(Game().PlantFraction, Is.EqualTo(0.25f).Within(1e-5f), "a new snapshot keeps the progress");

            _status.Tick(5f);
            Assert.That(Game().PlantFraction, Is.EqualTo(1f), "the progress stops at full");

            _status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, remaining: 79, activePoint: 0));
            Assert.That(Game().PlantingSite, Is.EqualTo(0));
            Assert.That(Game().PlantFraction, Is.Zero, "another site starts over");

            _status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, remaining: 79));
            Assert.That(Game().PlantingSite, Is.Null);
            Assert.That(_status.Tick(1f), Is.False, "nothing counts while nobody plants");
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
        public void TheEndedMatchShowsTheWinnerAndTheRoundsPlayed()
        {
            _status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, remaining: 30, alpha: 1, bravo: 2));
            Assert.That(Game().RoundNumber, Is.EqualTo(4));

            _status.Apply(Snapshot(
                RoundPhase.Ended, BombState.Defused, activePoint: 1, alpha: 1, bravo: 3, state: GameModeState.Ended, winner: Team.Bravo, carrier: null));

            var status = _status.Status;
            Assert.That(status.IsEnded, Is.True);
            Assert.That(status.Winner, Is.EqualTo((Side?)Side.Bravo));
            var game = Game();
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Ended));
            Assert.That(game.Winner, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(game.RoundNumber, Is.EqualTo(4));
            Assert.That(game.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(game.GetRoundWins(Side.Bravo), Is.EqualTo(3));
            Assert.That(game.LastRoundWinner, Is.EqualTo((Side?)Side.Bravo));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.GetRoundWins(new Side(5)));
        }

        [Test]
        public void TheNextMatchForgetsTheLastRoundWinner()
        {
            _status.Apply(Snapshot(RoundPhase.Live, BombState.Carried, alpha: 0, bravo: 0));
            _status.Apply(Snapshot(RoundPhase.Preparing, BombState.Carried, alpha: 1, bravo: 0));
            Assert.That(Game().LastRoundWinner, Is.EqualTo((Side?)Side.Alpha));

            _status.Apply(Snapshot(RoundPhase.Preparing, BombState.Carried, alpha: 0, bravo: 0));

            Assert.That(Game().LastRoundWinner, Is.Null);
            Assert.That(Game().RoundNumber, Is.EqualTo(1));
        }

        [Test]
        public void ASpectatorWithoutARowHasNoOwnSide()
        {
            var snapshot = Snapshot(RoundPhase.Live, BombState.Carried);
            var status = new ClientSearchAndDestroyStatus(7, _spec);

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
            var snapshot = Snapshot(RoundPhase.Waiting, BombState.Carried);
            snapshot.Match.HasBomb = false;
            snapshot.ObjectiveItems.Clear();

            _status.Apply(snapshot);

            Assert.That(Game().Phase, Is.EqualTo(RoundPhase.Waiting));
            Assert.That(Game().RoundNumber, Is.Zero);
            Assert.That(Game().Attacker, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void TheStatusNeedsASpecAndASnapshot()
        {
            Assert.Throws<ArgumentNullException>(() => new ClientSearchAndDestroyStatus(_self, null));
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
            Team? winner = null)
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

using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientHordeStatusTests
    {
        private const int _self = 3;

        private ClientHordeStatus _status;

        [SetUp]
        public void SetUp()
        {
            _status = new ClientHordeStatus(_self);
        }

        [Test]
        public void AFightingWaveIsShownInTheSameShapeAsOffline()
        {
            var changed = _status.Apply(Snapshot(wave: 2, waves: 5, remaining: 4, currency: 70, score: 6, elapsed: 95));

            Assert.That(changed, Is.True);
            var status = _status.Status;
            Assert.That(status.Detail, Is.InstanceOf<HordeStatus>(), "the HUD formatter registered for HordeStatus reads it");
            Assert.That(status.Detail, Is.SameAs(_status.Horde));
            var horde = _status.Horde;
            Assert.That(horde.Horde, Is.Null, "an online status carries no offline horde");
            Assert.That(horde.WaveNumber, Is.EqualTo(2));
            Assert.That(horde.WaveCount, Is.EqualTo(5));
            Assert.That(horde.Phase, Is.EqualTo(WavePhase.Fighting));
            Assert.That(horde.RemainingEnemies, Is.EqualTo(4));
            Assert.That(horde.RestRemaining, Is.Zero);
            Assert.That(horde.Currency, Is.EqualTo(70));
            Assert.That(horde.ElapsedSeconds, Is.EqualTo(95d));
            Assert.That(horde.IsEnded, Is.False);
            Assert.That(horde.IsVictory, Is.False);
            Assert.That(horde.LocalPlayerId, Is.EqualTo(_self));
            Assert.That(horde.IsAwaitingReturn, Is.False);
            Assert.That(horde.Offer, Is.Null);
            Assert.That(status.RemainingSeconds, Is.Null, "a horde has no time limit");
            Assert.That(status.SideScores, Has.Count.EqualTo(1));
            Assert.That(status.SideScores[0].Side, Is.EqualTo(Side.Alpha));
            Assert.That(status.SideScores[0].Score, Is.EqualTo(6d));
            Assert.That(status.OwnSide, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(status.IsEnded, Is.False);
        }

        [Test]
        public void TheSameValuesKeepTheShownStatus()
        {
            _status.Apply(Snapshot(wave: 1, waves: 5, remaining: 3));
            var shown = _status.Status;

            Assert.That(_status.Apply(Snapshot(wave: 1, waves: 5, remaining: 3)), Is.False);
            Assert.That(_status.Status, Is.SameAs(shown));

            Assert.That(_status.Apply(Snapshot(wave: 1, waves: 5, remaining: 2)), Is.True);
            Assert.That(_status.Status, Is.Not.SameAs(shown));
            Assert.That(((HordeStatus)shown.Detail).RemainingEnemies, Is.EqualTo(3), "the status already handed out keeps its values");
        }

        [Test]
        public void AClearedWaveIsARestWithTheSecondsToTheNext()
        {
            _status.Apply(Snapshot(wave: 1, waves: 5, remaining: 0, nextWave: 4));

            Assert.That(_status.Horde.Phase, Is.EqualTo(WavePhase.Resting));
            Assert.That(_status.Horde.RestRemaining, Is.EqualTo(4f));
        }

        [Test]
        public void BeforeTheFirstWaveItIsWaiting()
        {
            _status.Apply(Snapshot(wave: 0, waves: 5, remaining: 0, nextWave: 3));

            Assert.That(_status.Horde.Phase, Is.EqualTo(WavePhase.Waiting));
            Assert.That(_status.Horde.RestRemaining, Is.Zero, "the rest is only shown between waves");
        }

        [Test]
        public void AnEndlessHordeHasNoWaveCount()
        {
            _status.Apply(Snapshot(wave: 12, waves: 0, remaining: 0));

            Assert.That(_status.Horde.WaveCount, Is.Null);
            Assert.That(_status.Horde.Phase, Is.EqualTo(WavePhase.Resting), "an endless horde always has a next wave");
        }

        [Test]
        public void TheLastWaveClearedIsCompletedEvenBeforeTheEndArrives()
        {
            _status.Apply(Snapshot(wave: 5, waves: 5, remaining: 0));

            Assert.That(_status.Horde.Phase, Is.EqualTo(WavePhase.Completed));
        }

        [Test]
        public void TheEndShowsVictoryOrDefeat()
        {
            _status.Apply(Snapshot(wave: 5, waves: 5, remaining: 0, state: GameModeState.Ended, winner: Team.Alpha));

            Assert.That(_status.Horde.IsEnded, Is.True);
            Assert.That(_status.Horde.IsVictory, Is.True);
            Assert.That(_status.Status.Winner, Is.EqualTo((Side?)Side.Alpha));

            _status.Apply(Snapshot(wave: 3, waves: 5, remaining: 2, state: GameModeState.Ended, winner: Team.Bravo));

            Assert.That(_status.Horde.IsVictory, Is.False);
            Assert.That(_status.Horde.Phase, Is.EqualTo(WavePhase.Fighting), "the enemies were still coming");
            Assert.That(_status.Status.Winner, Is.EqualTo((Side?)Side.Bravo));
        }

        [Test]
        public void AFallenDefenderWaitsForTheEndOfTheWave()
        {
            _status.Apply(Snapshot(wave: 2, waves: 5, remaining: 3, selfHealth: 0));

            Assert.That(_status.Horde.IsAwaitingReturn, Is.True);

            _status.Apply(Snapshot(wave: 2, waves: 5, remaining: 0, nextWave: 5, selfHealth: 0));

            Assert.That(_status.Horde.IsAwaitingReturn, Is.False, "the rest brings everyone back");
        }

        [Test]
        public void ASpectatorHasNoOwnSide()
        {
            _status.Apply(Snapshot(wave: 1, waves: 5, remaining: 3, selfInMatch: false));

            Assert.That(_status.Status.OwnSide, Is.Null);
            Assert.That(_status.Horde.LocalPlayerId, Is.Null);
            Assert.That(_status.Horde.IsAwaitingReturn, Is.False);
        }

        [Test]
        public void AnotherObjectiveClearsTheStatus()
        {
            _status.Apply(Snapshot(wave: 1, waves: 5, remaining: 3));
            var snapshot = Snapshot(wave: 1, waves: 5, remaining: 3);
            snapshot.Match.Objective = MatchObjective.Payload;

            Assert.That(_status.Apply(snapshot), Is.True);
            Assert.That(_status.Status, Is.Null);
            Assert.That(_status.Apply(snapshot), Is.False, "already cleared");
            Assert.That(_status.Clear(), Is.False);
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => _status.Apply(null));
        }

        private static SnapshotMessage Snapshot(
            int wave,
            int waves,
            int remaining,
            int nextWave = 0,
            int currency = 50,
            int score = 0,
            int elapsed = 10,
            GameModeState state = GameModeState.Playing,
            Team? winner = null,
            int selfHealth = 100,
            bool selfInMatch = true)
        {
            var snapshot = new SnapshotMessage { Tick = 10 };
            ref var match = ref snapshot.Match;
            match.State = state;
            match.HasWinner = winner.HasValue;
            match.Winner = winner ?? Team.Alpha;
            match.Objective = MatchObjective.Horde;
            match.AlphaScore = score;
            match.AlphaPoints = currency;
            match.BravoPoints = remaining;
            match.RoundNumber = wave;
            match.RoundsToWin = waves;
            match.NextWaveSeconds = nextWave;
            match.ObjectiveRemainingSeconds = elapsed;
            if (selfInMatch)
            {
                ref var self = ref snapshot.Players.Add();
                self.PlayerId = _self;
                self.Team = Team.Alpha;
                self.Health = selfHealth;
                self.WeaponId = "";
            }

            ref var enemy = ref snapshot.Players.Add();
            enemy.PlayerId = BotRoster.FirstBotPlayerId;
            enemy.Team = Team.Bravo;
            enemy.Health = 100;
            enemy.WeaponId = "";
            return snapshot;
        }
    }
}

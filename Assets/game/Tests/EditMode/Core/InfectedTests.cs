using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class InfectedTests
    {
        private const float _startDelaySeconds = 5f;
        private const float _roundSeconds = 60f;
        private const float _respawnSeconds = 2f;
        private const int _scorePerInfection = 2;
        private const int _scorePerSurvivedRound = 7;

        private static readonly int[] _firstPlayer = { 0 };
        private static readonly int[] _lastPlayer = { 3 };
        private static readonly int[] _firstTwoPlayers = { 0, 1 };
        private static readonly int[] _firstThreePlayers = { 0, 1, 2 };

        [Test]
        public void EveryoneStartsAsASurvivorUntilTheStartDelayEnds()
        {
            var infected = Started(4, PickFirst);
            var turns = Record(infected);

            infected.Session.Tick(_startDelaySeconds - 0.5f);

            Assert.That(infected.HasBrokenOut, Is.False);
            Assert.That(infected.SecondsUntilOutbreak, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(infected.SurvivorCount, Is.EqualTo(4));
            Assert.That(infected.InfectedCount, Is.Zero);
            Assert.That(turns, Is.Empty);
            Assert.That(infected.Infect(1, 0), Is.False, "nobody can be infected before the outbreak");
            Assert.That(infected.IsInfected(1), Is.False);
        }

        [Test]
        public void TheOutbreakTurnsTheConfiguredNumberOfSurvivors()
        {
            var infected = Started(4, PickLast, initialInfected: 1);
            var turns = Record(infected);

            infected.Session.Tick(_startDelaySeconds);

            Assert.That(infected.HasBrokenOut, Is.True);
            Assert.That(infected.SecondsUntilOutbreak, Is.Zero);
            Assert.That(infected.FirstInfectedIds, Is.EqualTo(_lastPlayer));
            Assert.That(infected.Session.GetSide(3), Is.EqualTo(Infected.InfectedSide));
            Assert.That(infected.SurvivorCount, Is.EqualTo(3));
            Assert.That(infected.InfectedCount, Is.EqualTo(1));
            Assert.That(turns.Count, Is.EqualTo(1));
            Assert.That(turns[0].PlayerId, Is.EqualTo(3));
            Assert.That(turns[0].IsFirst, Is.True);
            Assert.That(turns[0].InfectorId, Is.Null);
        }

        [Test]
        public void ManySmallTicksReachTheOutbreak()
        {
            var infected = Started(3, PickFirst);

            for (var i = 0; i < 50; i++)
            {
                infected.Session.Tick(_startDelaySeconds / 50f);
            }

            Assert.That(infected.HasBrokenOut, Is.True);
            Assert.That(infected.FirstInfectedIds, Is.EqualTo(_firstPlayer));
        }

        [Test]
        public void TheOutbreakAlwaysLeavesOneSurvivor()
        {
            var infected = Started(3, PickFirst, initialInfected: 5);

            infected.Session.Tick(_startDelaySeconds);

            Assert.That(infected.FirstInfectedIds, Is.EqualTo(_firstTwoPlayers));
            Assert.That(infected.SurvivorCount, Is.EqualTo(1));
            Assert.That(infected.Session.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void ASurvivorKilledByTheInfectedJoinsThemAndScoresForThem()
        {
            var infected = BrokenOut(4);
            var turns = Record(infected);
            var session = infected.Session;

            session.ReportKill(0, 1);
            Assert.That(infected.Infect(1, 0), Is.True);

            Assert.That(session.GetSide(1), Is.EqualTo(Infected.InfectedSide));
            Assert.That(session.GetScore(Infected.InfectedSide), Is.EqualTo((double)_scorePerInfection));
            Assert.That(session.GetKills(0), Is.EqualTo(1));
            Assert.That(session.IsDown(1), Is.True, "the new infected waits to respawn");
            Assert.That(session.GetRespawnRemaining(1), Is.EqualTo(_respawnSeconds));
            Assert.That(turns.Count, Is.EqualTo(1));
            Assert.That(turns[0].PlayerId, Is.EqualTo(1));
            Assert.That(turns[0].InfectorId, Is.EqualTo((int?)0));
            Assert.That(turns[0].IsFirst, Is.False);
            Assert.That(infected.Infect(1, 0), Is.False, "an infected player is not infected twice");
        }

        [Test]
        public void ASurvivorWhoDiesWithoutAKillerIsInfectedToo()
        {
            var infected = BrokenOut(3);

            infected.Session.ReportDeath(2);
            Assert.That(infected.Infect(2, null), Is.True);

            Assert.That(infected.IsInfected(2), Is.True);
            Assert.That(infected.Session.GetScore(Infected.InfectedSide), Is.Zero, "nobody infected them");
        }

        [Test]
        public void KillingAnInfectedPlayerScoresNothingAndTheyComeBack()
        {
            var infected = BrokenOut(3);
            var session = infected.Session;
            var respawned = new List<int>();
            session.RespawnReady += respawned.Add;

            session.ReportKill(1, 0);

            Assert.That(session.GetScore(Infected.SurvivorSide), Is.Zero);
            Assert.That(infected.IsInfected(0), Is.True);
            session.Tick(_respawnSeconds);
            Assert.That(respawned, Is.EqualTo(_firstPlayer));
        }

        [Test]
        public void TheInfectedWinOnceEverySurvivorIsInfected()
        {
            var infected = BrokenOut(3);
            var session = infected.Session;

            session.ReportKill(0, 1);
            infected.Infect(1, 0);
            session.Tick(0.1f);
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing), "one survivor is still standing");

            session.ReportKill(1, 2);
            infected.Infect(2, 1);
            session.Tick(0.1f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo((Side?)Infected.InfectedSide));
            Assert.That(session.GetScore(Infected.InfectedSide), Is.EqualTo(2d * _scorePerInfection));
        }

        [Test]
        public void TheSurvivorsWinWhenTimeRunsOutAndScoreForSurviving()
        {
            var infected = BrokenOut(3);
            var session = infected.Session;

            session.Tick(_roundSeconds);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo((Side?)Infected.SurvivorSide));
            Assert.That(session.GetScore(Infected.SurvivorSide), Is.EqualTo((double)_scorePerSurvivedRound));
            Assert.That(infected.Infect(1, 0), Is.False, "nobody is infected after the end");
        }

        [Test]
        public void TheRoundUsesTheConfiguredTimeLimit()
        {
            var infected = new Infected(Spec(1, roundSeconds: 90f), PickFirst);

            Assert.That(infected.Session.TimeLimitSeconds, Is.EqualTo(90f));
            Assert.That(infected.Session.RemainingSeconds, Is.EqualTo(90f));
        }

        [Test]
        public void ResetPutsEveryoneBackOnTheSurvivorSide()
        {
            var infected = BrokenOut(3);
            infected.Session.ReportKill(0, 1);
            infected.Infect(1, 0);

            infected.Reset();

            Assert.That(infected.HasBrokenOut, Is.False);
            Assert.That(infected.SecondsUntilOutbreak, Is.EqualTo(_startDelaySeconds));
            Assert.That(infected.FirstInfectedIds, Is.Empty);
            Assert.That(infected.SurvivorCount, Is.EqualTo(3));
            Assert.That(infected.Session.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(infected.Session.GetScore(Infected.InfectedSide), Is.Zero);
        }

        [Test]
        public void PlayersAlreadyOnTheInfectedSideCountTowardTheFirstInfected()
        {
            var infected = new Infected(Spec(2), PickFirst);
            infected.Session.AddPlayer(0, Infected.SurvivorSide);
            infected.Session.AddPlayer(1, Infected.InfectedSide);
            infected.Session.AddPlayer(2, Infected.SurvivorSide);
            infected.Session.AddPlayer(3, Infected.SurvivorSide);
            infected.Session.Start();

            infected.Session.Tick(_startDelaySeconds);

            Assert.That(infected.FirstInfectedIds, Is.EqualTo(_firstPlayer));
            Assert.That(infected.InfectedCount, Is.EqualTo(2));
        }

        [Test]
        public void UnknownPlayersAreRejected()
        {
            var infected = BrokenOut(2);

            Assert.Throws<ArgumentNullException>(() => new Infected(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => infected.Infect(9, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => infected.Infect(1, 9));
            Assert.Throws<ArgumentOutOfRangeException>(() => infected.IsInfected(9));
        }

        [Test]
        public void TheStatusCopiesTheCountsAndTheLocalPlayersSide()
        {
            var infected = Started(4, PickFirst, initialInfected: 3);
            var before = InfectedStatus.Of(infected, 0);

            infected.Session.Tick(_startDelaySeconds);
            var after = InfectedStatus.Of(infected, 0);
            var botsOnly = InfectedStatus.Of(infected, null);

            Assert.That(before.HasBrokenOut, Is.False);
            Assert.That(before.SecondsUntilOutbreak, Is.EqualTo(_startDelaySeconds));
            Assert.That(before.SurvivorCount, Is.EqualTo(4));
            Assert.That(before.IsLocalPlayerInfected, Is.EqualTo((bool?)false));
            Assert.That(infected.FirstInfectedIds, Is.EqualTo(_firstThreePlayers));
            Assert.That(after.HasBrokenOut, Is.True);
            Assert.That(after.SurvivorCount, Is.EqualTo(1));
            Assert.That(after.InfectedCount, Is.EqualTo(3));
            Assert.That(after.IsLocalPlayerInfected, Is.EqualTo((bool?)true));
            Assert.That(botsOnly.IsLocalPlayerInfected, Is.Null);
            Assert.That(before.SurvivorCount, Is.EqualTo(4), "the earlier status keeps its values");
            Assert.Throws<ArgumentNullException>(() => InfectedStatus.Of(null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InfectedStatus(false, -1f, 0, 0, null));
        }

        [Test]
        public void TheLogLinesNameThePlayers()
        {
            Assert.That(InfectedLog.Outbreak(_firstTwoPlayers), Is.EqualTo("[Mode] infected outbreak first=0,1"));
            Assert.That(InfectedLog.Outbreak(Array.Empty<int>()), Is.EqualTo("[Mode] infected outbreak first=-"));
            Assert.That(InfectedLog.Turn(new InfectedTurn(2, 0, false)), Is.EqualTo("[Mode] infected turn player=2 by=0"));
            Assert.That(InfectedLog.Turn(new InfectedTurn(2, null, false)), Is.EqualTo("[Mode] infected turn player=2 by=-"));
            Assert.That(InfectedLog.Turn(new InfectedTurn(3, null, true)), Is.EqualTo("[Mode] infected turn player=3 by=first"));
            Assert.That(InfectedLog.Equip(1, "weapon.handgun"), Is.EqualTo("[Mode] infected equip player=1 weapon=weapon.handgun"));
            Assert.That(InfectedLog.Equip(1, " "), Is.EqualTo("[Mode] infected equip player=1 weapon=-"));
            Assert.Throws<ArgumentNullException>(() => InfectedLog.Outbreak(null));
        }

        [Test]
        public void TheFirstInfectedComeFromThePicker()
        {
            var first = Started(4, PickFirst);
            var last = Started(4, PickLast);

            first.Session.Tick(_startDelaySeconds);
            last.Session.Tick(_startDelaySeconds);

            Assert.That(first.FirstInfectedIds, Is.EqualTo(_firstPlayer));
            Assert.That(last.FirstInfectedIds, Is.EqualTo(_lastPlayer));
            Assert.That(first.IsInfected(1), Is.False);
        }

        [Test]
        public void WithoutAPickerTheFirstInfectedIsOneOfTheSurvivors()
        {
            var infected = new Infected(Spec(1));
            for (var id = 0; id < 4; id++)
            {
                infected.Session.AddPlayer(id, Infected.SurvivorSide);
            }

            infected.Session.Start();
            infected.Session.Tick(_startDelaySeconds);

            Assert.That(infected.FirstInfectedIds.Count, Is.EqualTo(1));
            Assert.That(infected.FirstInfectedIds[0], Is.InRange(0, 3));
            Assert.That(infected.InfectedCount, Is.EqualTo(1));
        }

        [Test]
        public void APickerOutsideTheSurvivorsIsRejected()
        {
            var infected = Started(3, count => count);

            Assert.Throws<InvalidOperationException>(() => infected.Session.Tick(_startDelaySeconds));
        }

        private static InfectedSpec Spec(int initialInfected, float roundSeconds = _roundSeconds)
        {
            return new InfectedSpec(
                initialInfected,
                _startDelaySeconds,
                roundSeconds,
                InfectedSpec.DefaultInfectedMaxHealth,
                InfectedSpec.DefaultInfectedSpeedMultiplier,
                _respawnSeconds,
                _scorePerInfection,
                _scorePerSurvivedRound);
        }

        private static Infected Started(int players, Func<int, int> pickIndex, int initialInfected = 1)
        {
            var infected = new Infected(Spec(initialInfected), pickIndex);
            for (var id = 0; id < players; id++)
            {
                infected.Session.AddPlayer(id, Infected.SurvivorSide);
            }

            infected.Session.Start();
            return infected;
        }

        // 最初の感染者は 0 番になる。
        private static Infected BrokenOut(int players)
        {
            var infected = Started(players, PickFirst);
            infected.Session.Tick(_startDelaySeconds);
            Assert.That(infected.FirstInfectedIds, Is.EqualTo(_firstPlayer));
            return infected;
        }

        private static List<InfectedTurn> Record(Infected infected)
        {
            var turns = new List<InfectedTurn>();
            infected.Turned += turns.Add;
            return turns;
        }

        private static int PickFirst(int count) => 0;

        private static int PickLast(int count) => count - 1;
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using ExtractionOutcome = Shooter.Core.ExtractionOutcome;

namespace Shooter.Net.Tests
{
    public sealed class NetV2MatchResultPartTests
    {
        // 承認される前の MatchClient の自分の Id
        private const int _localId = 0;
        private const int _otherId = 1;

        private readonly List<MatchResult> _results = new List<MatchResult>();
        private MatchClient _client;
        private NetV2MatchResultPart _part;

        [SetUp]
        public void SetUp()
        {
            _results.Clear();
            CombatEvents.MatchFinished += _results.Add;
            _client = new MatchClient(new SilentTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _ => { });
            _part = new NetV2MatchResultPart(_client);
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.MatchFinished -= _results.Add;
            _part.Dispose();
            _client.Dispose();
        }

        [Test]
        public void WinningTheTeamMatchRaisesAWonResultWithTheOwnRowAndTeamScore()
        {
            _part.Apply(TeamSnapshot(GameModeState.Playing, null));
            Assert.That(_results, Is.Empty, "nothing is raised while playing");

            _part.Apply(TeamSnapshot(GameModeState.Ended, Team.Alpha));

            Assert.That(_results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Won, 4, 2, 7, true) }));
        }

        [Test]
        public void LosingTheTeamMatchRaisesALostResult()
        {
            _part.Apply(TeamSnapshot(GameModeState.Playing, null));
            _part.Apply(TeamSnapshot(GameModeState.Ended, Team.Bravo));

            Assert.That(_results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Lost, 4, 2, 7, true) }));
        }

        [Test]
        public void EndingWithoutAWinnerRaisesADraw()
        {
            _part.Apply(TeamSnapshot(GameModeState.Playing, null));
            _part.Apply(TeamSnapshot(GameModeState.Ended, null));

            Assert.That(_results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Draw, 4, 2, 7, true) }));
        }

        [Test]
        public void TheEndedSnapshotsAfterTheFirstDoNotRaiseAgain()
        {
            _part.Apply(TeamSnapshot(GameModeState.Playing, null));
            _part.Apply(TeamSnapshot(GameModeState.Ended, Team.Alpha));
            _part.Apply(TeamSnapshot(GameModeState.Ended, Team.Alpha));
            _part.Apply(TeamSnapshot(GameModeState.Ended, Team.Alpha));

            Assert.That(_results, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheNextMatchOnTheSameConnectionRaisesItsOwnResult()
        {
            _part.Apply(TeamSnapshot(GameModeState.Playing, null));
            _part.Apply(TeamSnapshot(GameModeState.Ended, Team.Alpha));
            _part.Apply(TeamSnapshot(GameModeState.Waiting, null));
            _part.Apply(TeamSnapshot(GameModeState.Playing, null));
            _part.Apply(TeamSnapshot(GameModeState.Ended, Team.Bravo));

            Assert.That(_results, Is.EqualTo(new[]
            {
                new MatchResult(MatchOutcome.Won, 4, 2, 7, true),
                new MatchResult(MatchOutcome.Lost, 4, 2, 7, true),
            }));
        }

        [Test]
        public void JoiningOnTheEndScreenRaisesNothing()
        {
            _part.Apply(TeamSnapshot(GameModeState.Ended, Team.Alpha));

            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void WithoutTheOwnRowNothingIsRaised()
        {
            _part.Apply(TeamSnapshot(GameModeState.Playing, null));
            var ended = TeamSnapshot(GameModeState.Ended, Team.Alpha);
            ended.Players.Clear();

            _part.Apply(ended);

            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void AfterDisposeNothingIsRaised()
        {
            _part.Apply(TeamSnapshot(GameModeState.Playing, null));
            _part.Dispose();

            _part.Apply(TeamSnapshot(GameModeState.Ended, Team.Alpha));

            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void TheSoleLeaderOfAFreeForAllWinsWithTheKillPoints()
        {
            var result = NetV2MatchResultPart.ResultOf(FreeForAllSnapshot(ownKills: 5, otherKills: 3), _localId);

            Assert.That(result, Is.EqualTo((MatchResult?)new MatchResult(MatchOutcome.Won, 5, 1, 10, true)));
        }

        [Test]
        public void BehindTheLeaderOfAFreeForAllLoses()
        {
            var result = NetV2MatchResultPart.ResultOf(FreeForAllSnapshot(ownKills: 2, otherKills: 3), _localId);

            Assert.That(result, Is.EqualTo((MatchResult?)new MatchResult(MatchOutcome.Lost, 2, 1, 4, true)));
        }

        [Test]
        public void ATiedTopOfAFreeForAllIsADraw()
        {
            var result = NetV2MatchResultPart.ResultOf(FreeForAllSnapshot(ownKills: 3, otherKills: 3), _localId);

            Assert.That(result, Is.EqualTo((MatchResult?)new MatchResult(MatchOutcome.Draw, 3, 1, 6, true)));
        }

        [Test]
        public void AnObjectiveMatchCountsTheObjectivePointsOfTheOwnTeam()
        {
            var snapshot = TeamSnapshot(GameModeState.Ended, Team.Bravo, Team.Bravo);
            snapshot.Match.Objective = MatchObjective.Domination;
            snapshot.Match.AlphaPoints = 120;
            snapshot.Match.BravoPoints = 200;

            var result = NetV2MatchResultPart.ResultOf(snapshot, _localId);

            Assert.That(result, Is.EqualTo((MatchResult?)new MatchResult(MatchOutcome.Won, 4, 2, 200, true)));
        }

        [Test]
        public void GunGameWinsOnlyForTheSoleTopTierPlayerEvenOnTheWinningTeam()
        {
            var winner = GunGameSnapshot(ownTier: 6, otherTier: 3);
            var loser = GunGameSnapshot(ownTier: 3, otherTier: 6);
            var tied = GunGameSnapshot(ownTier: 4, otherTier: 4);

            Assert.That(NetV2MatchResultPart.ResultOf(winner, _localId)?.Outcome, Is.EqualTo(MatchOutcome.Won));
            Assert.That(NetV2MatchResultPart.ResultOf(loser, _localId)?.Outcome, Is.EqualTo(MatchOutcome.Lost));
            Assert.That(NetV2MatchResultPart.ResultOf(tied, _localId)?.Outcome, Is.EqualTo(MatchOutcome.Draw));
        }

        [Test]
        public void BattleRoyaleWinsForTheSquadThatIsLeftStanding()
        {
            Assert.That(NetV2MatchResultPart.ResultOf(BattleRoyaleSnapshot(ownHealth: 50, otherHealth: 0), _localId)?.Outcome, Is.EqualTo(MatchOutcome.Won));
            Assert.That(NetV2MatchResultPart.ResultOf(BattleRoyaleSnapshot(ownHealth: 0, otherHealth: 50), _localId)?.Outcome, Is.EqualTo(MatchOutcome.Lost));
        }

        [Test]
        public void ExtractionWinsOnlyWhenTheRaidEndedExtracted()
        {
            var snapshot = TeamSnapshot(GameModeState.Ended, null);
            snapshot.Match.Objective = MatchObjective.Extraction;

            Assert.That(NetV2MatchResultPart.ResultOf(snapshot, _localId, ExtractionOutcome.Extracted)?.Outcome, Is.EqualTo(MatchOutcome.Won));
            Assert.That(NetV2MatchResultPart.ResultOf(snapshot, _localId, ExtractionOutcome.Died)?.Outcome, Is.EqualTo(MatchOutcome.Lost));
            Assert.That(NetV2MatchResultPart.ResultOf(snapshot, _localId, ExtractionOutcome.TimedOut)?.Outcome, Is.EqualTo(MatchOutcome.Lost));
        }

        [Test]
        public void RefusesMissingInputs()
        {
            Assert.Throws<ArgumentNullException>(() => new NetV2MatchResultPart(null));
            Assert.Throws<ArgumentNullException>(() => NetV2MatchResultPart.ResultOf(null, _localId));
        }

        // 自分は Alpha で 4 キル 2 デス、陣営の得点は Alpha 7・Bravo 5
        private static SnapshotMessage TeamSnapshot(GameModeState state, Team? winner, Team ownTeam = Team.Alpha)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.State = state;
            snapshot.Match.Objective = MatchObjective.None;
            snapshot.Match.HasWinner = winner.HasValue;
            snapshot.Match.Winner = winner ?? Team.Alpha;
            snapshot.Match.AlphaScore = 7;
            snapshot.Match.BravoScore = 5;
            ref var own = ref snapshot.Players.Add();
            own.PlayerId = _localId;
            own.Team = ownTeam;
            own.Kills = 4;
            own.Deaths = 2;
            own.Health = 100;
            return snapshot;
        }

        // 1 キル 2 点の FFA
        private static SnapshotMessage FreeForAllSnapshot(int ownKills, int otherKills)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.State = GameModeState.Ended;
            snapshot.Match.Objective = MatchObjective.FreeForAll;
            snapshot.Match.AlphaPoints = 2;
            ref var own = ref snapshot.Players.Add();
            own.PlayerId = _localId;
            own.Kills = ownKills;
            own.Deaths = 1;
            ref var other = ref snapshot.Players.Add();
            other.PlayerId = _otherId;
            other.Kills = otherKills;
            other.Deaths = 1;
            return snapshot;
        }

        // 2 人とも Alpha で、勝った人のチームとして Alpha が届く
        private static SnapshotMessage GunGameSnapshot(int ownTier, int otherTier)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.State = GameModeState.Ended;
            snapshot.Match.Objective = MatchObjective.GunGame;
            snapshot.Match.HasWinner = true;
            snapshot.Match.Winner = Team.Alpha;
            ref var own = ref snapshot.Players.Add();
            own.PlayerId = _localId;
            own.Team = Team.Alpha;
            own.GunGameTier = ownTier;
            ref var other = ref snapshot.Players.Add();
            other.PlayerId = _otherId;
            other.Team = Team.Alpha;
            other.GunGameTier = otherTier;
            return snapshot;
        }

        // サーバーは勝者のチームを送らない。自分は分隊 1、相手は分隊 2
        private static SnapshotMessage BattleRoyaleSnapshot(int ownHealth, int otherHealth)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.State = GameModeState.Ended;
            snapshot.Match.Objective = MatchObjective.BattleRoyale;
            ref var own = ref snapshot.Players.Add();
            own.PlayerId = _localId;
            own.SquadId = 1;
            own.Health = ownHealth;
            ref var other = ref snapshot.Players.Add();
            other.PlayerId = _otherId;
            other.SquadId = 2;
            other.Health = otherHealth;
            return snapshot;
        }

        private sealed class SilentTransport : IMatchTransport
        {
            public bool IsActive => false;

            public void Listen(string address, ushort port)
            {
                throw new InvalidOperationException("a client transport does not listen");
            }

            public void Connect(string address, ushort port)
            {
            }

            public void Disconnect(ConnectionId connection, string reason)
            {
            }

            public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
            {
            }

            public int GetMaxPayloadSize(TransportChannel channel)
            {
                return MessageWriter.DefaultMaxLength;
            }

            public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
            {
                milliseconds = 0;
                return false;
            }

            public bool TryReceive(out TransportEvent transportEvent)
            {
                transportEvent = default;
                return false;
            }

            public void Shutdown(string reason)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}

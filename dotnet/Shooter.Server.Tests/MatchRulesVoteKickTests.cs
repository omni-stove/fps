using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class MatchRulesVoteKickTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);
        private static readonly VoteKickSpec _enabled = new VoteKickSpec(true, 0.6f, 30f, 120f);

        private StringWriter _log;
        private RecordingTransport _transport;
        private StubControl _control;
        private ServerMatch _match;
        private MatchRulesModule _module;
        private long _tick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new RecordingTransport();
            _control = new StubControl();
            _tick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _module?.Stop();
            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void TwoOfThreeVotingOutTheThirdKicksThemAndBarsTheirRejoin()
        {
            Start(_enabled);
            Join(1);
            Join(2);
            Join(3);

            Assert.That(_module.ServerCallVoteKick(1, 3), Is.EqualTo(VoteKickCall.Started));

            var opened = States(2).Single();
            Assert.That(opened.TargetPlayerId, Is.EqualTo(3));
            Assert.That(opened.CallerPlayerId, Is.EqualTo(1));
            Assert.That(opened.YesVotes, Is.EqualTo(1));
            Assert.That(opened.RequiredVotes, Is.EqualTo(2));
            Assert.That(opened.RemainingSeconds, Is.EqualTo(30f));
            Assert.That(opened.Outcome, Is.EqualTo(VoteKickOutcome.Undecided));
            Assert.That(States(3), Has.Count.EqualTo(1), "the target is told too");
            Assert.That(_control.Kicks, Is.Empty);

            Assert.That(_module.ServerVoteKickBallot(2, 3, true), Is.EqualTo(VoteKickBallot.Cast));

            Assert.That(_control.Kicks, Is.EqualTo(new[] { (new ConnectionId(3), DisconnectNotice.VoteKickedReason) }));
            var decided = States(3).Last();
            Assert.That(decided.Outcome, Is.EqualTo(VoteKickOutcome.Passed), "the target learns the result before being cut");
            Assert.That(decided.YesVotes, Is.EqualTo(2));
            Assert.That(decided.RemainingSeconds, Is.Zero);
            Assert.That(States(1).Last().Outcome, Is.EqualTo(VoteKickOutcome.Passed));
            Assert.That(_match.IsBarred("device3"), Is.True);
            Assert.That(_match.IsBarred("device1"), Is.False);

            var log = _log.ToString();
            Assert.That(log, Does.Contain(VoteKickLog.Settings(_enabled)));
            Assert.That(log, Does.Contain(VoteKickLog.Called(1, 3, VoteKickCall.Started)));
            Assert.That(log, Does.Contain(VoteKickLog.Voted(2, 3, true, VoteKickBallot.Cast)));
            Assert.That(log, Does.Contain(VoteKickLog.Decided(new VoteKickResult(3, 1, 2, 0, 2, VoteKickEnd.Passed))));
            Assert.That(log, Does.Contain(VoteKickLog.Kicked(3, true)));
        }

        [Test]
        public void RequestMessagesCallTheVoteAndCastTheBallots()
        {
            Start(_enabled);
            Join(1);
            Join(2);
            Join(3);

            _module.OnMessage(new ConnectionId(1), new VoteKickRequestMessage { TargetPlayerId = 3, InFavor = true });
            Assert.That(_module.CurrentVoteKick.IsOpen, Is.True);
            Assert.That(_module.CurrentVoteKick.TargetId, Is.EqualTo(3));

            _module.OnMessage(new ConnectionId(2), new VoteKickRequestMessage { TargetPlayerId = 3, InFavor = true });

            Assert.That(_control.Kicks.Select(k => k.Connection), Is.EqualTo(new[] { new ConnectionId(3) }));
        }

        [Test]
        public void AVoteAgainstWithoutAnOpenVoteStartsNothing()
        {
            Start(_enabled);
            Join(1);
            Join(2);
            Join(3);

            _module.OnMessage(new ConnectionId(1), new VoteKickRequestMessage { TargetPlayerId = 3, InFavor = false });

            Assert.That(_module.CurrentVoteKick.IsOpen, Is.False);
            Assert.That(States(1), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain(VoteKickLog.Voted(1, 3, false, VoteKickBallot.NoVote)));
        }

        [Test]
        public void ADisabledVoteKickIsRefused()
        {
            Start(VoteKickSpec.Default);
            Join(1);
            Join(2);
            Join(3);

            Assert.That(_module.ServerCallVoteKick(1, 3), Is.EqualTo(VoteKickCall.Disabled));
            _module.OnMessage(new ConnectionId(2), new VoteKickRequestMessage { TargetPlayerId = 3, InFavor = true });

            Assert.That(_module.CurrentVoteKick.IsOpen, Is.False);
            Assert.That(_control.Kicks, Is.Empty);
            Assert.That(States(1), Is.Empty);
            Assert.That(States(3), Is.Empty);
            Assert.That(_match.BarredCount, Is.Zero);
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[VoteKick] off"));
            Assert.That(log, Does.Contain(VoteKickLog.Called(1, 3, VoteKickCall.Disabled)));
        }

        [Test]
        public void TheBarIsLiftedWhenTheNextMatchStarts()
        {
            Start(_enabled);
            Join(1);
            Join(2);
            Join(3);
            _module.ServerCallVoteKick(1, 3);
            _module.ServerVoteKickBallot(2, 3, true);
            Assert.That(_match.Leave(new ConnectionId(3)), Is.True, "the server cuts the kicked player");
            Assert.That(_match.IsBarred("device3"), Is.True);

            EndTheMatch();
            Assert.That(_match.IsBarred("device3"), Is.True, "still barred while the match shows its result");
            Advance(1f + 0.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.IsBarred("device3"), Is.False);
            Assert.That(_match.BarredCount, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain(VoteKickLog.Unbarred(1)));
        }

        [Test]
        public void TheStateIsSentAgainEverySecondAndTheVoteFailsAtTheDeadline()
        {
            Start(new VoteKickSpec(true, 0.6f, 2f, 120f));
            Join(1);
            Join(2);
            Join(3);
            Join(4);
            _module.ServerCallVoteKick(1, 4);
            Assert.That(States(2), Has.Count.EqualTo(1));

            Advance(1.05f);
            var states = States(2);
            Assert.That(states, Has.Count.EqualTo(2));
            Assert.That(states[1].RemainingSeconds, Is.EqualTo(1f).Within(0.05f));

            Advance(1f);

            var failed = States(2).Last();
            Assert.That(failed.Outcome, Is.EqualTo(VoteKickOutcome.Failed));
            Assert.That(failed.YesVotes, Is.EqualTo(1));
            Assert.That(failed.RequiredVotes, Is.EqualTo(2));
            Assert.That(_module.CurrentVoteKick.IsOpen, Is.False);
            Assert.That(_control.Kicks, Is.Empty);
            Assert.That(_match.BarredCount, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain(VoteKickLog.Decided(new VoteKickResult(4, 1, 1, 0, 2, VoteKickEnd.Expired))));
        }

        [Test]
        public void VotesAgainstEndTheVoteWithoutAKick()
        {
            Start(_enabled);
            Join(1);
            Join(2);
            Join(3);

            _module.ServerCallVoteKick(1, 3);
            Assert.That(_module.ServerVoteKickBallot(2, 3, false), Is.EqualTo(VoteKickBallot.Cast));

            Assert.That(States(1).Last().Outcome, Is.EqualTo(VoteKickOutcome.Failed));
            Assert.That(_control.Kicks, Is.Empty);
        }

        [Test]
        public void SomeoneJoiningDuringTheVoteIsSentItButHasNoVote()
        {
            Start(_enabled);
            Join(1);
            Join(2);
            Join(3);
            _module.ServerCallVoteKick(1, 3);

            Join(4);

            var state = States(4).Single();
            Assert.That(state.TargetPlayerId, Is.EqualTo(3));
            Assert.That(state.Outcome, Is.EqualTo(VoteKickOutcome.Undecided));
            Assert.That(_module.ServerVoteKickBallot(4, 3, true), Is.EqualTo(VoteKickBallot.NotVoter));
            Assert.That(_control.Kicks, Is.Empty);
        }

        [Test]
        public void ALeaversVoteIsDroppedAndTheOthersAreTold()
        {
            Start(_enabled);
            Join(1);
            Join(2);
            Join(3);
            Join(4);
            Join(5);
            _module.ServerCallVoteKick(1, 5);
            _module.ServerVoteKickBallot(2, 5, true);
            Assert.That(States(3).Last().YesVotes, Is.EqualTo(2));

            Assert.That(_match.Leave(new ConnectionId(2)), Is.True);

            var state = States(3).Last();
            Assert.That(state.YesVotes, Is.EqualTo(1));
            Assert.That(state.RequiredVotes, Is.EqualTo(2));
            Assert.That(state.Outcome, Is.EqualTo(VoteKickOutcome.Undecided));
            Assert.That(_log.ToString(), Does.Contain(VoteKickLog.Left(2)));
        }

        [Test]
        public void TheTargetLeavingEndsTheVoteWithoutBarringThem()
        {
            Start(_enabled);
            Join(1);
            Join(2);
            Join(3);
            _module.ServerCallVoteKick(1, 3);

            Assert.That(_match.Leave(new ConnectionId(3)), Is.True);

            Assert.That(States(1).Last().Outcome, Is.EqualTo(VoteKickOutcome.Failed));
            Assert.That(_control.Kicks, Is.Empty);
            Assert.That(_match.IsBarred("device3"), Is.False);
        }

        [Test]
        public void TheCallerCannotCallAgainStraightAway()
        {
            Start(_enabled);
            Join(1);
            Join(2);
            Join(3);
            Join(4);
            _module.ServerCallVoteKick(1, 4);
            _module.ServerVoteKickBallot(2, 4, false);
            _module.ServerVoteKickBallot(3, 4, false);
            Assert.That(_module.CurrentVoteKick.IsOpen, Is.False);

            Assert.That(_module.ServerCallVoteKick(1, 4), Is.EqualTo(VoteKickCall.CoolingDown));
            Assert.That(_module.ServerCallVoteKick(2, 4), Is.EqualTo(VoteKickCall.Started), "someone else may call");
        }

        [Test]
        public void SelfAndBotsCannotBeVotedOut()
        {
            Start(_enabled);
            Join(1);
            Join(2);
            Join(3);
            var bot = BotRoster.FirstBotPlayerId;
            _match.JoinBot(bot);

            Assert.That(_module.ServerCallVoteKick(1, 1), Is.EqualTo(VoteKickCall.Self));
            Assert.That(_module.ServerCallVoteKick(1, bot), Is.EqualTo(VoteKickCall.Bot));
            Assert.That(_module.ServerCallVoteKick(1, 99), Is.EqualTo(VoteKickCall.TargetNotPresent));

            Assert.That(_module.ServerCallVoteKick(1, 3), Is.EqualTo(VoteKickCall.Started));
            Assert.That(_module.CurrentVoteKick.VoterCount, Is.EqualTo(2), "the bot has no vote");
        }

        [Test]
        public void OnlyOneVoteRunsAtATime()
        {
            Start(_enabled);
            Join(1);
            Join(2);
            Join(3);
            Join(4);
            _module.ServerCallVoteKick(1, 4);

            _module.OnMessage(new ConnectionId(2), new VoteKickRequestMessage { TargetPlayerId = 3, InFavor = true });

            Assert.That(_module.CurrentVoteKick.TargetId, Is.EqualTo(4));
            Assert.That(_log.ToString(), Does.Contain(VoteKickLog.Called(2, 3, VoteKickCall.InProgress)));
        }

        private void Start(VoteKickSpec voteKick)
        {
            var settings = new NetMatchSettings(scoreLimit: 1, restartDelaySeconds: 1f, voteKick: voteKick);
            var rules = GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
            var events = new ServerEventWriter(false, _log, _log);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, events, () => _time, settings.MaxTeamSizeDifference, settings.MapVoteSeconds);
            var map = TestMaps.Data("NetTest", TestRules.Points());
            _match.UseMaps(map, null);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                map,
                _match,
                new ServerMessenger(_transport),
                _control,
                TestRules.Data(rules, settings));
            _module = new MatchRulesModule();
            _module.Start(context);
        }

        private void Join(int id)
        {
            _match.Join(new ConnectionId((ulong)id), "device" + id);
        }

        // 1 は Alpha、2 は Bravo に入り、1 点で試合が終わる
        private void EndTheMatch()
        {
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
        }

        private void Advance(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _module.Tick(++_tick, _tickSeconds);
            }
        }

        private List<VoteKickStateMessage> States(int connection)
        {
            return _transport.Read<VoteKickStateMessage>(connection, Copy);
        }

        private static VoteKickStateMessage Copy(VoteKickStateMessage message)
        {
            return new VoteKickStateMessage
            {
                TargetPlayerId = message.TargetPlayerId,
                CallerPlayerId = message.CallerPlayerId,
                YesVotes = message.YesVotes,
                RequiredVotes = message.RequiredVotes,
                RemainingSeconds = message.RemainingSeconds,
                Outcome = message.Outcome,
            };
        }
    }
}

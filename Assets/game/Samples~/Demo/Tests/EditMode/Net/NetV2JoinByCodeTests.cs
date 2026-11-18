using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Runtime;

namespace Shooter.Net.Tests
{
    public sealed class NetV2JoinByCodeTests
    {
        private const string _self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _owner = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string _version = "0.3.0";
        private const string _code = "ABC234";

        private readonly List<FakeLobbyBackend> _backends = new List<FakeLobbyBackend>();
        private readonly List<JoinCodeResult> _connected = new List<JoinCodeResult>();
        private readonly List<JoinCodeOutcome> _outcomes = new List<JoinCodeOutcome>();
        private readonly List<string> _logs = new List<string>();
        private string _refusal;
        private int _partySize;
        private NetV2JoinByCode _join;

        [SetUp]
        public void SetUp()
        {
            GameSignals.JoinByCode.Clear();
            _backends.Clear();
            _connected.Clear();
            _outcomes.Clear();
            _logs.Clear();
            _refusal = null;
            _partySize = 1;
            _join = new NetV2JoinByCode(CreateBackend, _version, () => _partySize, Connect, _logs.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _join.Dispose();
            GameSignals.JoinByCode.Clear();
        }

        [Test]
        public void ItIsPublishedAsTheJoinByCodeSignalAndDisposingWithdrawsIt()
        {
            Assert.That(GameSignals.JoinByCode.Value, Is.SameAs(_join));

            _join.Dispose();

            Assert.That(GameSignals.JoinByCode.HasValue, Is.False);
        }

        [Test]
        public void AFoundServerIsConnectedWithTheNormalizedCode()
        {
            _join.Join(" abc234 ", _outcomes.Add);
            var backend = _backends[0];
            backend.CompleteLogin(_self);
            backend.CompleteSearch(new[] { Server("mine", "203.0.113.3:7790", 2, _code) });

            Assert.That(_outcomes, Is.EqualTo(new[] { JoinCodeOutcome.Joined }));
            Assert.That(_connected, Has.Count.EqualTo(1));
            Assert.That(_connected[0].JoinCode, Is.EqualTo(_code));
            Assert.That(_connected[0].Options.Address, Is.EqualTo("203.0.113.3"));
            Assert.That(_connected[0].Options.Port, Is.EqualTo(7790));
            Assert.That(_logs, Has.Member(JoinCodeLog.Connecting("203.0.113.3:7790")));
            Assert.That(_logs, Has.None.Contains(_code), "the code lets anyone in, so it stays out of the log");
        }

        [Test]
        public void NoServerWithTheCodeIsNotFoundAndNothingConnects()
        {
            _join.Join(_code, _outcomes.Add);
            _backends[0].CompleteLogin(_self);
            _backends[0].CompleteSearch(new[] { Server("other", "203.0.113.2:7777", 1, "ZZZ999") });

            Assert.That(_outcomes, Is.EqualTo(new[] { JoinCodeOutcome.NotFound }));
            Assert.That(_connected, Is.Empty);
        }

        [Test]
        public void AMistypedCodeIsNotFoundWithoutLoggingIn()
        {
            _join.Join("not a code!", _outcomes.Add);

            Assert.That(_outcomes, Is.EqualTo(new[] { JoinCodeOutcome.NotFound }));
            Assert.That(_backends[0].Logins, Is.EqualTo(0));
        }

        [Test]
        public void AServerThatCannotBeConnectedIsAFailure()
        {
            _refusal = "already in an online match";
            _join.Join(_code, _outcomes.Add);
            _backends[0].CompleteLogin(_self);
            _backends[0].CompleteSearch(new[] { Server("mine", "203.0.113.3:7777", 0, _code) });

            Assert.That(_outcomes, Is.EqualTo(new[] { JoinCodeOutcome.Failed }));
            Assert.That(_logs, Has.Member(JoinCodeLog.Finished(JoinCodeOutcome.Failed, JoinCodeLog.NotConnected(_refusal))));
        }

        [Test]
        public void ASecondJoinWhileSearchingIsRefused()
        {
            _join.Join(_code, _outcomes.Add);

            _join.Join("ZZZ999", _outcomes.Add);

            Assert.That(_outcomes, Is.EqualTo(new[] { JoinCodeOutcome.Failed }));
            Assert.That(_backends, Has.Count.EqualTo(1));
            Assert.That(_logs, Has.Member(JoinCodeLog.Finished(JoinCodeOutcome.Failed, JoinCodeLog.Busy())));
            Assert.That(_join.IsSearching, Is.True);
        }

        [Test]
        public void NoBackendIsAFailure()
        {
            var join = new NetV2JoinByCode(() => null, _version, () => 1, Connect, _logs.Add);
            var outcomes = new List<JoinCodeOutcome>();

            join.Join(_code, outcomes.Add);
            join.Dispose();

            Assert.That(outcomes, Is.EqualTo(new[] { JoinCodeOutcome.Failed }));
            Assert.That(_logs, Has.Member(JoinCodeLog.Finished(
                JoinCodeOutcome.Failed, JoinCodeLog.Unavailable("the EOS lobby factory returned no backend"))));
        }

        [Test]
        public void TickDrivesTheSearchAndClosesItsServiceAfterItFinishes()
        {
            _join.Join(_code, _outcomes.Add);
            var backend = _backends[0];
            _join.Tick(1f);
            Assert.That(backend.Ticks, Is.EqualTo(1));

            backend.CompleteLogin(_self);
            backend.CompleteSearch(new[] { Server("mine", "203.0.113.3:7777", 0, _code) });
            Assert.That(backend.Disposed, Is.False, "the service is not closed inside its own callback");

            _join.Tick(2f);
            _join.Tick(3f);

            Assert.That(backend.Disposed, Is.True);
            Assert.That(_join.IsSearching, Is.False);
        }

        [Test]
        public void TheFoundServersMatchLobbyIsHandedToTheConnectionAndKeptOpen()
        {
            _join.Join(_code, _outcomes.Add);
            var backend = _backends[0];
            backend.AcceptsMatchLobbies = true;
            backend.CompleteLogin(_self);
            backend.CompleteSearch(new[] { Server("mine", "203.0.113.3:7777", 0, _code) });

            Assert.That(_outcomes, Is.EqualTo(new[] { JoinCodeOutcome.Joined }));
            Assert.That(backend.MatchLobbyJoins, Is.EqualTo(new[] { "mine" }));
            Assert.That(_connected[0].MatchLobby, Is.Not.Null);
            Assert.That(_connected[0].MatchLobby.LobbyId, Is.EqualTo("mine"));

            _join.Tick(1f);
            _join.Tick(2f);

            Assert.That(backend.Disposed, Is.False, "the connection owns the lobby service for the match");
            _connected[0].MatchLobby.Dispose();
            Assert.That(backend.Disposed, Is.True);
        }

        [Test]
        public void AMatchLobbyThatIsNotConnectedIsLeft()
        {
            _refusal = "already in an online match";
            _join.Join(_code, _outcomes.Add);
            var backend = _backends[0];
            backend.AcceptsMatchLobbies = true;
            backend.CompleteLogin(_self);
            backend.CompleteSearch(new[] { Server("mine", "203.0.113.3:7777", 0, _code) });

            Assert.That(_outcomes, Is.EqualTo(new[] { JoinCodeOutcome.Failed }));
            Assert.That(backend.Disposed, Is.True);
            Assert.That(_logs, Has.Member(MatchLobbyLog.Left("mine")));
        }

        [Test]
        public void AMatchLobbyThatCannotBeJoinedStillConnects()
        {
            _join.Join(_code, _outcomes.Add);
            _backends[0].CompleteLogin(_self);
            _backends[0].CompleteSearch(new[] { Server("mine", "203.0.113.3:7777", 0, _code) });

            Assert.That(_outcomes, Is.EqualTo(new[] { JoinCodeOutcome.Joined }));
            Assert.That(_connected[0].MatchLobby, Is.Null);
            Assert.That(_logs, Has.Member(MatchLobbyLog.NotJoined("mine", LobbyBackendErrors.NotSupported)));
        }

        [Test]
        public void TheSearchLooksForRoomForTheWholeParty()
        {
            _partySize = 3;
            _join.Join(_code, _outcomes.Add);
            _backends[0].CompleteLogin(_self);
            _backends[0].CompleteSearch(new[] { Server("mine", "203.0.113.3:7777", NetMatchSettings.DefaultMaxPlayers - 2, _code) });

            Assert.That(_outcomes, Is.EqualTo(new[] { JoinCodeOutcome.Full }));
        }

        [Test]
        public void ADisposedJoinRefusesToStart()
        {
            _join.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _join.Join(_code, _outcomes.Add));
            Assert.Throws<ArgumentNullException>(() => new NetV2JoinByCode(CreateBackend, _version, () => 1, Connect, null));
        }

        [TestCase(JoinCodeOutcome.NotFound)]
        [TestCase(JoinCodeOutcome.Full)]
        [TestCase(JoinCodeOutcome.VersionMismatch)]
        [TestCase(JoinCodeOutcome.Failed)]
        public void TheClientRefusesAResultThatFoundNoServerWithoutStartingAConnection(JoinCodeOutcome outcome)
        {
            var refusal = NetV2Client.ConnectByJoinCode(JoinCodeResult.NotJoined(outcome, "not joined"));

            Assert.That(refusal, Is.EqualTo("no server was found for the join code (" + outcome + ")"));
            Assert.That(NetV2Client.Current, Is.Null);
        }

        [Test]
        public void TheClientNeedsAJoinResult()
        {
            Assert.Throws<ArgumentNullException>(() => NetV2Client.ConnectByJoinCode(null));
            Assert.That(NetV2Client.Current, Is.Null);
        }

        private ILobbyBackend CreateBackend()
        {
            var backend = new FakeLobbyBackend();
            _backends.Add(backend);
            return backend;
        }

        private string Connect(JoinCodeResult result)
        {
            if (_refusal != null)
            {
                return _refusal;
            }

            _connected.Add(result);
            return null;
        }

        private static LobbyInfo Server(string id, string endpoint, int players, string code)
        {
            var match = new NetMatchSettings(privateMatch: true, joinCode: code);
            return new LobbyInfo(
                id, _owner, ServerLobby.MaxMembers(NetMatchSettings.DefaultMaxPlayers), new[] { _owner },
                ServerLobby.Attributes(endpoint, _version, players, match));
        }
    }
}

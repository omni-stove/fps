using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.NetV2.Tests
{
    public sealed class JoinCodeSearchTests
    {
        private const string _self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _owner = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string _version = "0.3.0";
        private const string _code = "ABC234";

        private FakeLobbyBackend _backend;
        private List<string> _log;
        private List<JoinCodeResult> _results;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeLobbyBackend();
            _log = new List<string>();
            _results = new List<JoinCodeResult>();
        }

        [TearDown]
        public void TearDown()
        {
            _backend.Dispose();
        }

        private JoinCodeSearch Begin(string code, int partySize = 1)
        {
            var search = new JoinCodeSearch(_backend, _version, 10f, _log.Add);
            search.Begin(0f, code, partySize, _results.Add);
            return search;
        }

        private static LobbyInfo Server(string id, string endpoint, int players, string code = null, string version = _version)
        {
            var match = code == null ? NetMatchSettings.Default : new NetMatchSettings(privateMatch: true, joinCode: code);
            return new LobbyInfo(
                id, _owner, ServerLobby.MaxMembers(NetMatchSettings.DefaultMaxPlayers), new[] { _owner },
                ServerLobby.Attributes(endpoint, version, players, match));
        }

        private JoinCodeResult Result()
        {
            Assert.That(_results, Has.Count.EqualTo(1));
            return _results[0];
        }

        [Test]
        public void ConnectsToTheServerWhoseCodeMatchesOverUdp()
        {
            var search = Begin(" abc234 ");
            Assert.That(_backend.Logins, Is.EqualTo(1));
            _backend.CompleteLogin(_self);

            Assert.That(_backend.Searches, Is.EqualTo(1));
            Assert.That(_backend.SearchedBucket, Is.EqualTo("shooter-server:0.3.0"));
            Assert.That(_backend.SearchedMax, Is.EqualTo(LobbyLaunchOptions.MaxSearchResults));
            _backend.CompleteSearch(new List<LobbyInfo>
            {
                Server("public", "203.0.113.1:7777", 4),
                Server("other", "203.0.113.2:7777", 1, "ZZZ999"),
                Server("mine", "203.0.113.3:7790", 2, _code),
            });

            var result = Result();
            Assert.That(result.Outcome, Is.EqualTo(JoinCodeOutcome.Joined));
            Assert.That(result.Options.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(result.Options.Transport, Is.EqualTo(NetTransportKind.Udp));
            Assert.That(result.Options.Address, Is.EqualTo("203.0.113.3"));
            Assert.That(result.Options.Port, Is.EqualTo(7790));
            Assert.That(result.JoinCode, Is.EqualTo(_code));
            Assert.That(result.Reason, Is.Null);
            Assert.That(search.IsFinished, Is.True);
            Assert.That(_log, Has.Some.EqualTo("[Lobby] join code: picked id=mine server=203.0.113.3:7790 players=2/8"));
            Assert.That(_log, Has.None.Contains(_code), "the code lets anyone in, so it stays out of the log");
        }

        [Test]
        public void TheFoundCodeGoesIntoTheConnectionPayloadNextToThePartyAndSkill()
        {
            Begin("abc234");
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(new List<LobbyInfo> { Server("mine", "203.0.113.3:7777", 0, _code) });

            var extras = Result().WithJoinCode(new ConnectionExtras("party-1", null, 1500, _self));
            Assert.That(extras.PartyId, Is.EqualTo("party-1"));
            Assert.That(extras.JoinCode, Is.EqualTo(_code));
            Assert.That(extras.SkillRating, Is.EqualTo(1500));
            Assert.That(extras.ProductUserId, Is.EqualTo(_self));

            var payload = ConnectionPayload.Encode(_version, "rejoin-key-1", extras);
            Assert.That(ConnectionPayload.TryDecode(payload, out _, out _, out var decoded), Is.True);
            Assert.That(decoded.JoinCode, Is.EqualTo(_code));
        }

        [Test]
        public void IsNotFoundWhenNoServerHasTheCode()
        {
            var search = Begin(_code);
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(new List<LobbyInfo>
            {
                Server("public", "203.0.113.1:7777", 0),
                Server("other", "203.0.113.2:7777", 0, "ZZZ999"),
            });

            var result = Result();
            Assert.That(result.Outcome, Is.EqualTo(JoinCodeOutcome.NotFound));
            Assert.That(result.Options, Is.Null);
            Assert.That(result.JoinCode, Is.Null);
            Assert.That(result.Reason, Is.EqualTo("no server in shooter-server:0.3.0 has the join code"));
            Assert.That(search.IsFinished, Is.True);
            Assert.That(_backend.Searches, Is.EqualTo(1), "a code search does not search again");
            Assert.Throws<InvalidOperationException>(() => result.WithJoinCode(ConnectionExtras.None));
        }

        [Test]
        public void IsNotFoundWhenTheSearchReturnsNothing()
        {
            Begin(_code);
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(null);

            Assert.That(Result().Outcome, Is.EqualTo(JoinCodeOutcome.NotFound));
        }

        [Test]
        public void IsFullWhenTheServerWithTheCodeHasNoRoom()
        {
            Begin(_code);
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(new List<LobbyInfo> { Server("mine", "203.0.113.3:7777", NetMatchSettings.DefaultMaxPlayers, _code) });

            var result = Result();
            Assert.That(result.Outcome, Is.EqualTo(JoinCodeOutcome.Full));
            Assert.That(result.Options, Is.Null);
            Assert.That(result.Reason, Is.EqualTo("the server with the join code is full"));
        }

        [Test]
        public void IsFullWhenTheServerHasNoRoomForTheWholeParty()
        {
            Begin(_code, 3);
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(new List<LobbyInfo> { Server("mine", "203.0.113.3:7777", NetMatchSettings.DefaultMaxPlayers - 2, _code) });

            var result = Result();
            Assert.That(result.Outcome, Is.EqualTo(JoinCodeOutcome.Full));
            Assert.That(result.Reason, Is.EqualTo("the server with the join code has no room for a party of 3"));
        }

        [Test]
        public void IsVersionMismatchWhenTheServerWithTheCodeRunsAnotherVersion()
        {
            Begin(_code);
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(new List<LobbyInfo> { Server("mine", "203.0.113.3:7777", 0, _code, "0.2.0") });

            var result = Result();
            Assert.That(result.Outcome, Is.EqualTo(JoinCodeOutcome.VersionMismatch));
            Assert.That(result.Reason, Does.Contain("0.3.0"));
        }

        [Test]
        public void AnInvalidCodeIsNotFoundWithoutLoggingIn()
        {
            var search = Begin("ab c!");

            var result = Result();
            Assert.That(result.Outcome, Is.EqualTo(JoinCodeOutcome.NotFound));
            Assert.That(result.Reason, Does.Contain("[0-9A-Za-z_-]"));
            Assert.That(_backend.Logins, Is.Zero);
            Assert.That(search.IsFinished, Is.True);
        }

        [Test]
        public void ABlankCodeIsNotFoundWithoutLoggingIn()
        {
            Begin("   ");

            Assert.That(Result().Outcome, Is.EqualTo(JoinCodeOutcome.NotFound));
            Assert.That(_backend.Logins, Is.Zero);
        }

        [Test]
        public void FailsWhenTheLoginFails()
        {
            Begin(_code);
            _backend.CompleteLogin(null, "TimedOut");

            var result = Result();
            Assert.That(result.Outcome, Is.EqualTo(JoinCodeOutcome.Failed));
            Assert.That(result.Reason, Is.EqualTo("EOS login failed: TimedOut"));
            Assert.That(_backend.Searches, Is.Zero);
        }

        [Test]
        public void FailsWhenTheLoginReturnsNoUser()
        {
            Begin(_code);
            _backend.CompleteLogin(" ");

            Assert.That(Result().Outcome, Is.EqualTo(JoinCodeOutcome.Failed));
            Assert.That(_backend.Searches, Is.Zero);
        }

        [Test]
        public void FailsWhenTheSearchFails()
        {
            Begin(_code);
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(null, "TooManyRequests");

            var result = Result();
            Assert.That(result.Outcome, Is.EqualTo(JoinCodeOutcome.Failed));
            Assert.That(result.Reason, Is.EqualTo("search failed: TooManyRequests"));
            Assert.That(_log, Has.Some.EqualTo("[Lobby] join code: Failed reason=search failed: TooManyRequests"));
        }

        [Test]
        public void FailsWhenLoggedOutWhileSearching()
        {
            Begin(_code);
            _backend.CompleteLogin(_self);
            _backend.LogOut("expired");
            _backend.CompleteSearch(new List<LobbyInfo> { Server("mine", "203.0.113.3:7777", 0, _code) });

            var result = Result();
            Assert.That(result.Outcome, Is.EqualTo(JoinCodeOutcome.Failed));
            Assert.That(result.Reason, Is.EqualTo("logged out: expired"));
        }

        [Test]
        public void FailsWhenTheLobbyServiceDoesNotAnswerInTime()
        {
            var search = Begin(_code);
            _backend.CompleteLogin(_self);

            search.Tick(9.9f);
            Assert.That(_results, Is.Empty);
            Assert.That(_backend.Ticks, Is.EqualTo(1));

            search.Tick(10f);
            var result = Result();
            Assert.That(result.Outcome, Is.EqualTo(JoinCodeOutcome.Failed));
            Assert.That(result.Reason, Is.EqualTo("no answer from the lobby service after 10s while searching"));

            _backend.CompleteSearch(new List<LobbyInfo> { Server("mine", "203.0.113.3:7777", 0, _code) });
            Assert.That(_results, Has.Count.EqualTo(1), "a late answer does not finish the search twice");
        }

        [Test]
        public void TimesOutWhileLoggingIn()
        {
            var search = Begin(_code);
            search.Tick(10f);

            Assert.That(Result().Reason, Is.EqualTo("no answer from the lobby service after 10s while logging in"));
        }

        [Test]
        public void DisposingStopsTheAnswerAndDisposesTheBackend()
        {
            var search = Begin(_code);
            _backend.CompleteLogin(_self);
            search.Dispose();
            search.Dispose();
            _backend.CompleteSearch(new List<LobbyInfo> { Server("mine", "203.0.113.3:7777", 0, _code) });
            _backend.LogOut("expired");
            search.Tick(100f);

            Assert.That(_results, Is.Empty);
            Assert.That(_backend.Disposed, Is.EqualTo(1));
            Assert.That(_backend.Ticks, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => search.Begin(0f, _code, _ => { }));
        }

        [Test]
        public void ALogOutBeforeBeginningIsIgnored()
        {
            var search = new JoinCodeSearch(_backend, _version, _log.Add);
            _backend.LogOut("expired");

            Assert.That(search.IsFinished, Is.False);
            search.Begin(0f, _code, _results.Add);
            Assert.That(_backend.Logins, Is.EqualTo(1));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var search = Begin(_code);

            Assert.Throws<InvalidOperationException>(() => search.Begin(0f, _code, _ => { }));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new JoinCodeSearch(new FakeLobbyBackend(), _version, _log.Add).Begin(0f, _code, 0, _ => { }));
            Assert.Throws<ArgumentNullException>(
                () => new JoinCodeSearch(new FakeLobbyBackend(), _version, _log.Add).Begin(0f, _code, null));
            Assert.Throws<ArgumentNullException>(() => new JoinCodeSearch(null, _version, _log.Add));
            Assert.Throws<ArgumentNullException>(() => new JoinCodeSearch(_backend, _version, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new JoinCodeSearch(_backend, _version, 0f, _log.Add));
            Assert.Throws<ArgumentOutOfRangeException>(() => new JoinCodeSearch(_backend, _version, float.NaN, _log.Add));
            Assert.Throws<ArgumentOutOfRangeException>(() => new JoinCodeSearch(_backend, _version, float.PositiveInfinity, _log.Add));
        }

        [Test]
        public void AResultCannotBeMadeInconsistent()
        {
            var options = new NetLaunchOptions(NetLaunchMode.Client, "203.0.113.3", 7777, NetTransportKind.Udp, null);

            Assert.Throws<ArgumentNullException>(() => JoinCodeResult.Joined(null, _code));
            Assert.Throws<ArgumentException>(() => JoinCodeResult.Joined(options, "ab c"));
            Assert.Throws<ArgumentException>(() => JoinCodeResult.Joined(options, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => JoinCodeResult.NotJoined(JoinCodeOutcome.Joined, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => JoinCodeResult.NotJoined((JoinCodeOutcome)99, "x"));
            Assert.Throws<ArgumentNullException>(() => JoinCodeResult.Joined(options, _code).WithJoinCode(null));
        }

        private sealed class FakeLobbyBackend : ILobbyBackend
        {
            private Action<string, string> _login;
            private Action<IReadOnlyList<LobbyInfo>, string> _search;

            public event Action<string> LoggedOut;

            public event Action<string, string> MemberAttributesChanged
            {
                add { }
                remove { }
            }

            public int Ticks { get; private set; }

            public int Logins { get; private set; }

            public int Searches { get; private set; }

            public string SearchedBucket { get; private set; }

            public int SearchedMax { get; private set; }

            public int Disposed { get; private set; }

            public void Tick()
            {
                Ticks++;
            }

            public void Login(Action<string, string> done)
            {
                Logins++;
                _login = done;
            }

            public void SearchLobbies(string bucketId, int maxResults, Action<IReadOnlyList<LobbyInfo>, string> done)
            {
                Searches++;
                SearchedBucket = bucketId;
                SearchedMax = maxResults;
                _search = done;
            }

            public void CreateMemberLobby(
                string bucketId, int maxMembers, IReadOnlyList<LobbyAttribute> attributes, Action<LobbyInfo, string> done)
            {
                done(null, LobbyBackendErrors.NotSupported);
            }

            public void JoinMemberLobby(string lobbyId, Action<LobbyInfo, string> done)
            {
                done(null, LobbyBackendErrors.NotSupported);
            }

            public void LeaveMemberLobby(string lobbyId, Action<string> done)
            {
                done(LobbyBackendErrors.NotSupported);
            }

            public void ReadMemberLobby(string lobbyId, Action<LobbyInfo, string> done)
            {
                done(null, LobbyBackendErrors.NotSupported);
            }

            public void SetMemberAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done)
            {
                done(LobbyBackendErrors.NotSupported);
            }

            public void ReadMemberAttributes(string lobbyId, string userId, Action<IReadOnlyList<LobbyAttribute>, string> done)
            {
                done(null, LobbyBackendErrors.NotSupported);
            }

            public void JoinMatchLobby(string lobbyId, Action<LobbyInfo, string> done)
            {
                done(null, LobbyBackendErrors.NotSupported);
            }

            public void LeaveMatchLobby(string lobbyId, Action<string> done)
            {
                done(LobbyBackendErrors.NotSupported);
            }

            public void Dispose()
            {
                Disposed++;
            }

            public void CompleteLogin(string userId, string error = null)
            {
                _login(userId, error);
            }

            public void CompleteSearch(IReadOnlyList<LobbyInfo> lobbies, string error = null)
            {
                _search(lobbies, error);
            }

            public void LogOut(string reason)
            {
                LoggedOut?.Invoke(reason);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.NetV2.Tests
{
    public sealed class QuickMatchSearchTests
    {
        private const string _self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _owner = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string _version = "0.3.0";

        private FakeLobbyBackend _backend;
        private List<string> _log;
        private NetLaunchOptions _ready;
        private string _failure;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeLobbyBackend();
            _log = new List<string>();
            _ready = null;
            _failure = null;
        }

        [TearDown]
        public void TearDown()
        {
            _backend.Dispose();
        }

        private QuickMatchSearch Begin()
        {
            var search = new QuickMatchSearch(_backend, _version, _log.Add);
            search.Begin(0f, options => _ready = options, reason => _failure = reason);
            return search;
        }

        private static LobbyInfo Server(string id, string endpoint, int players, string version = _version)
        {
            return new LobbyInfo(
                id, _owner, ServerLobby.MaxMembers(NetMatchSettings.DefaultMaxPlayers), new[] { _owner }, ServerLobby.Attributes(endpoint, version, players));
        }

        [Test]
        public void ConnectsToTheBusiestServerOverUdp()
        {
            var search = Begin();
            Assert.That(_backend.Logins, Is.EqualTo(1));
            _backend.CompleteLogin(_self);

            Assert.That(_backend.Searches, Is.EqualTo(1));
            Assert.That(_backend.SearchedBucket, Is.EqualTo("shooter-server:0.3.0"));
            Assert.That(_backend.SearchedMax, Is.EqualTo(LobbyLaunchOptions.MaxSearchResults));
            _backend.CompleteSearch(new List<LobbyInfo>
            {
                Server("quiet", "203.0.113.1:7777", 1),
                Server("busy", "203.0.113.2:7790", 4),
                Server("full", "203.0.113.3:7777", NetMatchSettings.DefaultMaxPlayers),
            });

            Assert.That(_failure, Is.Null);
            Assert.That(_ready.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(_ready.Transport, Is.EqualTo(NetTransportKind.Udp));
            Assert.That(_ready.Address, Is.EqualTo("203.0.113.2"));
            Assert.That(_ready.Port, Is.EqualTo(7790));
            Assert.That(search.IsFinished, Is.True);
            Assert.That(_log, Has.Some.StartsWith("[Lobby] quick match picked id=busy server=203.0.113.2:7790 players=4/8"));
        }

        private static LobbyInfo RatedServer(string id, string endpoint, int players, int? skillAverage)
        {
            return new LobbyInfo(
                id, _owner, ServerLobby.MaxMembers(NetMatchSettings.DefaultMaxPlayers), new[] { _owner },
                ServerLobby.Attributes(endpoint, _version, players, NetMatchSettings.Default, skillAverage));
        }

        [Test]
        public void ConnectsToTheServerNearestToTheSkillRating()
        {
            var search = new QuickMatchSearch(_backend, _version, _log.Add);
            search.Begin(0f, 1, 1000, options => _ready = options, reason => _failure = reason);
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(new List<LobbyInfo>
            {
                RatedServer("strong", "203.0.113.1:7777", 6, 1800),
                RatedServer("near", "203.0.113.2:7777", 1, 1050),
                RatedServer("unrated", "203.0.113.3:7777", 7, null),
            });

            Assert.That(_failure, Is.Null);
            Assert.That(_ready.Address, Is.EqualTo("203.0.113.2"));
            Assert.That(_log, Has.Some.EqualTo("[Lobby] logging in to search shooter-server:0.3.0 near skill 1000"));
            Assert.That(_log, Has.Some.StartsWith("[Lobby] quick match picked id=near server=203.0.113.2:7777 players=1/8 skill=1050 (self 1000)"));
        }

        [Test]
        public void APartyPassesTheAverageOfItsMembers()
        {
            var search = new QuickMatchSearch(_backend, _version, _log.Add);
            search.Begin(0f, 2, ServerLobby.SkillAverage(new int?[] { 1600, 2000 }), options => _ready = options, reason => _failure = reason);
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(new List<LobbyInfo>
            {
                RatedServer("low", "203.0.113.1:7777", 1, 1500),
                RatedServer("high", "203.0.113.2:7777", 1, 1850),
                RatedServer("crowded", "203.0.113.3:7777", 7, 1800),
            });

            Assert.That(_failure, Is.Null);
            Assert.That(_ready.Address, Is.EqualTo("203.0.113.2"), "1850 is nearest to the party's 1800 among the servers with room for 2");
            Assert.That(_log, Has.Some.EqualTo("[Lobby] logging in to search shooter-server:0.3.0 for a party of 2 near skill 1800"));
        }

        [Test]
        public void WithoutASkillRatingTheBusiestServerIsStillPicked()
        {
            var search = new QuickMatchSearch(_backend, _version, _log.Add);
            search.Begin(0f, 1, null, options => _ready = options, reason => _failure = reason);
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(new List<LobbyInfo>
            {
                RatedServer("near", "203.0.113.1:7777", 1, 1000),
                RatedServer("busy", "203.0.113.2:7777", 5, null),
            });

            Assert.That(_ready.Address, Is.EqualTo("203.0.113.2"));
            Assert.That(_log, Has.Some.EqualTo("[Lobby] logging in to search shooter-server:0.3.0"));
            Assert.That(_log, Has.None.Contains("skill"));
        }

        [Test]
        public void APartyOnlyPicksAServerWithRoomForEveryone()
        {
            var search = new QuickMatchSearch(_backend, _version, _log.Add);
            search.Begin(0f, 3, options => _ready = options, reason => _failure = reason);
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(new List<LobbyInfo>
            {
                Server("busy", "203.0.113.1:7777", NetMatchSettings.DefaultMaxPlayers - 2),
                Server("roomy", "203.0.113.2:7777", NetMatchSettings.DefaultMaxPlayers - 3),
                Server("quiet", "203.0.113.3:7777", 1),
            });

            Assert.That(_failure, Is.Null);
            Assert.That(_ready.Address, Is.EqualTo("203.0.113.2"), "the busiest server has room for only 2 of the 3");
            Assert.That(_log, Has.Some.EqualTo("[Lobby] logging in to search shooter-server:0.3.0 for a party of 3"));
            Assert.That(_log, Has.Some.StartsWith("[Lobby] quick match picked id=roomy server=203.0.113.2:7777 players=5/8"));
        }

        [Test]
        public void APartySearchesAgainWhenNoServerHasRoomForEveryone()
        {
            var search = new QuickMatchSearch(_backend, _version, _log.Add);
            search.Begin(0f, 3, options => _ready = options, reason => _failure = reason);
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(new List<LobbyInfo>
            {
                Server("busy", "203.0.113.1:7777", NetMatchSettings.DefaultMaxPlayers - 2),
                Server("full", "203.0.113.2:7777", NetMatchSettings.DefaultMaxPlayers),
            });

            Assert.That(_ready, Is.Null);
            Assert.That(_log, Has.Some.EqualTo(
                "[Lobby] no server to join among 2 found (room for 2 (party of 3) x1, full x1); searching again in 5s"));

            search.Tick(5f);
            Assert.That(_backend.Searches, Is.EqualTo(2));
            _backend.CompleteSearch(new List<LobbyInfo> { Server("busy", "203.0.113.1:7777", NetMatchSettings.DefaultMaxPlayers - 3) });

            Assert.That(_failure, Is.Null);
            Assert.That(_ready.Address, Is.EqualTo("203.0.113.1"));
        }

        [Test]
        public void SearchesEveryFiveSecondsUntilAServerAppears()
        {
            var search = Begin();
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(Array.Empty<LobbyInfo>());

            search.Tick(4.9f);
            Assert.That(_backend.Searches, Is.EqualTo(1));

            search.Tick(5f);
            Assert.That(_backend.Searches, Is.EqualTo(2));
            _backend.CompleteSearch(null, "TimedOut");

            search.Tick(10f);
            Assert.That(_backend.Searches, Is.EqualTo(3));
            _backend.CompleteSearch(new List<LobbyInfo> { Server("s1", "203.0.113.1:7777", 0) });

            Assert.That(_failure, Is.Null);
            Assert.That(_ready.Address, Is.EqualTo("203.0.113.1"));
            Assert.That(_backend.Ticks, Is.EqualTo(3));
        }

        [Test]
        public void GivesUpAfterThirtySecondsWithTheReason()
        {
            var search = Begin();
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(Array.Empty<LobbyInfo>());

            for (var now = 5f; now < 30f; now += 5f)
            {
                search.Tick(now);
                _backend.CompleteSearch(new List<LobbyInfo> { Server("old", "203.0.113.1:7777", 0, "0.2.0") });
            }

            Assert.That(_backend.Searches, Is.EqualTo(6), "searched at 0, 5, 10, 15, 20 and 25 seconds");
            search.Tick(29.9f);
            Assert.That(_failure, Is.Null);

            search.Tick(30f);
            Assert.That(_failure, Does.Contain("no quick match server found after 30s").And.Contain("0.2.0"));
            Assert.That(_ready, Is.Null);
            Assert.That(search.IsFinished, Is.True);
            Assert.That(_backend.Searches, Is.EqualTo(6));
        }

        [Test]
        public void IgnoresAServerFoundAfterGivingUp()
        {
            var search = Begin();
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(Array.Empty<LobbyInfo>());
            search.Tick(25f);

            search.Tick(30f);
            _backend.CompleteSearch(new List<LobbyInfo> { Server("late", "203.0.113.1:7777", 0) });

            Assert.That(_failure, Does.Contain("no quick match server found"));
            Assert.That(_ready, Is.Null);
        }

        [Test]
        public void FailsWhenTheLoginFails()
        {
            Begin();
            _backend.CompleteLogin(null, "InvalidCredentials");

            Assert.That(_failure, Is.EqualTo("EOS login failed: InvalidCredentials"));
            Assert.That(_backend.Searches, Is.Zero);
        }

        [Test]
        public void FailsWhenTheLoginReturnsNoUser()
        {
            Begin();
            _backend.CompleteLogin(" ");

            Assert.That(_failure, Does.Contain("no user id"));
        }

        [Test]
        public void FailsWhenLoggedOutWhileSearching()
        {
            Begin();
            _backend.CompleteLogin(_self);
            _backend.LogOut("expired");

            Assert.That(_failure, Is.EqualTo("logged out: expired"));
            _backend.CompleteSearch(new List<LobbyInfo> { Server("s1", "203.0.113.1:7777", 0) });
            Assert.That(_ready, Is.Null);
        }

        [Test]
        public void DisposeClosesTheBackendAndDropsLateResults()
        {
            var search = Begin();
            _backend.CompleteLogin(_self);
            search.Dispose();
            search.Dispose();

            _backend.CompleteSearch(new List<LobbyInfo> { Server("s1", "203.0.113.1:7777", 0) });
            search.Tick(5f);

            Assert.That(_backend.Disposed, Is.EqualTo(1), "a second Dispose does not close the backend again");
            Assert.That(_ready, Is.Null);
            Assert.That(_failure, Is.Null);
            Assert.Throws<ObjectDisposedException>(() => search.Begin(0f, _ => { }, _ => { }));
        }

        private QuickMatchSearch BeginJoiningTheMatchLobby()
        {
            var search = new QuickMatchSearch(_backend, _version, _log.Add) { JoinsMatchLobby = true };
            search.Begin(0f, options => _ready = options, reason => _failure = reason);
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(new List<LobbyInfo> { Server("busy", "203.0.113.2:7790", 4) });
            return search;
        }

        [Test]
        public void JoinsThePickedServersMatchLobbyBeforeConnecting()
        {
            var search = BeginJoiningTheMatchLobby();
            Assert.That(_backend.MatchLobbyJoins, Is.EqualTo(new List<string> { "busy" }));
            Assert.That(_ready, Is.Null, "connecting waits for the match lobby");
            Assert.That(search.IsFinished, Is.False);

            _backend.CompleteMatchLobbyJoin(Server("busy", "203.0.113.2:7790", 4));

            Assert.That(_ready.Address, Is.EqualTo("203.0.113.2"));
            Assert.That(_ready.Port, Is.EqualTo(7790));
            var lobby = search.TakeMatchLobby();
            Assert.That(lobby.LobbyId, Is.EqualTo("busy"));
            Assert.That(search.TakeMatchLobby(), Is.Null, "the lobby is handed over once");

            search.Tick(1f);
            search.Dispose();
            Assert.That(_backend.Ticks, Is.Zero, "the owner of the match lobby ticks the service");
            Assert.That(_backend.Disposed, Is.Zero, "the search no longer owns the service");

            lobby.Dispose();
            Assert.That(_backend.Disposed, Is.EqualTo(1));
        }

        [Test]
        public void AnUntakenMatchLobbyIsClosedWithTheSearch()
        {
            var search = BeginJoiningTheMatchLobby();
            _backend.CompleteMatchLobbyJoin(Server("busy", "203.0.113.2:7790", 4));

            search.Dispose();

            Assert.That(_backend.Disposed, Is.EqualTo(1));
            Assert.That(_log, Has.Member(MatchLobbyLog.Left("busy")));
        }

        [Test]
        public void AMatchLobbyThatCannotBeJoinedStillConnectsWithoutVoice()
        {
            var search = BeginJoiningTheMatchLobby();

            _backend.CompleteMatchLobbyJoin(null, "LobbyIsFull");

            Assert.That(_failure, Is.Null);
            Assert.That(_ready.Address, Is.EqualTo("203.0.113.2"));
            Assert.That(search.TakeMatchLobby(), Is.Null);
            Assert.That(_log, Has.Member(MatchLobbyLog.NotJoined("busy", "LobbyIsFull")));
        }

        [Test]
        public void NoAnswerFromTheMatchLobbyConnectsWithoutVoiceAtTheTimeout()
        {
            var search = BeginJoiningTheMatchLobby();

            search.Tick(29.9f);
            Assert.That(_ready, Is.Null);
            search.Tick(30f);

            Assert.That(_failure, Is.Null, "the server is already picked");
            Assert.That(_ready.Address, Is.EqualTo("203.0.113.2"));
            Assert.That(search.TakeMatchLobby(), Is.Null);
            Assert.That(_backend.Searches, Is.EqualTo(1), "it does not search again");
        }

        [Test]
        public void ALogOutWhileJoiningTheMatchLobbyConnectsWithoutVoice()
        {
            var search = BeginJoiningTheMatchLobby();

            _backend.LogOut("expired");

            Assert.That(_failure, Is.Null);
            Assert.That(_ready.Address, Is.EqualTo("203.0.113.2"));
            Assert.That(search.TakeMatchLobby(), Is.Null);
        }

        [Test]
        public void JoinsMatchLobbyCannotChangeAfterBeginning()
        {
            var search = Begin();

            Assert.Throws<InvalidOperationException>(() => search.JoinsMatchLobby = true);
        }

        [Test]
        public void BeginsOnlyOnceAndChecksItsArguments()
        {
            var search = Begin();

            Assert.Throws<InvalidOperationException>(() => search.Begin(0f, _ => { }, _ => { }));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new QuickMatchSearch(new FakeLobbyBackend(), _version, _log.Add).Begin(0f, 0, _ => { }, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new QuickMatchSearch(null, _version, _log.Add));
            Assert.Throws<ArgumentNullException>(() => new QuickMatchSearch(_backend, _version, null));
            Assert.Throws<ArgumentNullException>(() => new QuickMatchSearch(_backend, _version, null, _log.Add));
        }

        private sealed class FakeLobbyBackend : ILobbyBackend
        {
            private Action<string, string> _login;
            private Action<IReadOnlyList<LobbyInfo>, string> _search;
            private Action<LobbyInfo, string> _joinMatch;

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

            public List<string> MatchLobbyJoins { get; } = new List<string>();

            public void JoinMatchLobby(string lobbyId, Action<LobbyInfo, string> done)
            {
                MatchLobbyJoins.Add(lobbyId);
                _joinMatch = done;
            }

            public void CompleteMatchLobbyJoin(LobbyInfo lobby, string error = null)
            {
                _joinMatch(lobby, error);
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

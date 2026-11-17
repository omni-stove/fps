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

        /// <summary>既定の間隔 (5 秒) と期限 (30 秒) で探し始める。</summary>
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

        [Test]
        public void BeginsOnlyOnceAndChecksItsArguments()
        {
            var search = Begin();

            Assert.Throws<InvalidOperationException>(() => search.Begin(0f, _ => { }, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new QuickMatchSearch(null, _version, _log.Add));
            Assert.Throws<ArgumentNullException>(() => new QuickMatchSearch(_backend, _version, null));
            Assert.Throws<ArgumentNullException>(() => new QuickMatchSearch(_backend, _version, null, _log.Add));
        }

        private sealed class FakeLobbyBackend : ILobbyBackend
        {
            private Action<string, string> _login;
            private Action<IReadOnlyList<LobbyInfo>, string> _search;

            public event Action<string> LoggedOut;

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

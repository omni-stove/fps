using System;
using System.Collections.Generic;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Net.Tests
{
    public sealed class LobbyLauncherTests
    {
        private const string _self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _owner = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const float _interval = 3f;
        private const float _timeout = 30f;
        private const string _version = "0.3.0";

        private FakeLobbyBackend _backend;
        private LobbyService _service;
        private NetLaunchOptions _ready;
        private string _failure;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeLobbyBackend();
            _service = new LobbyService(_backend, "fps:test");
            _ready = null;
            _failure = null;
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        private LobbyLauncher Begin(LobbyLaunchOptions options)
        {
            var launcher = new LobbyLauncher(_service, options, new RetrySchedule(_interval, _timeout));
            launcher.Begin(0f, options2 => _ready = options2, reason => _failure = reason);
            return launcher;
        }

        private static LobbyInfo Lobby(string id, string owner, params string[] members)
        {
            return new LobbyInfo(id, owner, 8, members);
        }

        private static LobbyInfo Server(string id, string endpoint, int players, string version = _version)
        {
            return new LobbyInfo(id, _owner, ServerLobby.MaxPlayers, new[] { _owner }, ServerLobby.Attributes(endpoint, version, players));
        }

        /// <summary>既定の間隔 (5 秒) と期限 (30 秒) でクイックマッチを始める。</summary>
        private LobbyLauncher BeginQuickMatch()
        {
            var launcher = new LobbyLauncher(_service, LobbyLaunchOptions.QuickMatch(), _version);
            launcher.Begin(0f, options => _ready = options, reason => _failure = reason);
            return launcher;
        }

        [Test]
        public void QuickMatchConnectsToTheBusiestServerOverUdpWithoutJoiningItsLobby()
        {
            var launcher = BeginQuickMatch();
            _backend.CompleteLogin(_self);

            Assert.That(_backend.Searches, Is.EqualTo(1));
            _backend.CompleteSearch(new List<LobbyInfo>
            {
                Server("quiet", "203.0.113.1:7777", 1),
                Server("busy", "203.0.113.2:7790", 4),
                Server("full", "203.0.113.3:7777", ServerLobby.MaxPlayers),
            });

            Assert.That(_failure, Is.Null);
            Assert.That(_ready.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(_ready.Transport, Is.EqualTo(NetTransportKind.Udp));
            Assert.That(_ready.Address, Is.EqualTo("203.0.113.2"));
            Assert.That(_ready.Port, Is.EqualTo(7790));
            Assert.That(_backend.JoinedId, Is.Null, "the client talks to the server directly, not through the lobby");
            Assert.That(_service.Current, Is.Null);
            Assert.That(launcher.IsFinished, Is.True);
        }

        [Test]
        public void QuickMatchSearchesEveryFiveSecondsUntilAServerAppears()
        {
            var launcher = BeginQuickMatch();
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(Array.Empty<LobbyInfo>());

            launcher.Tick(4.9f);
            Assert.That(_backend.Searches, Is.EqualTo(1));

            launcher.Tick(5f);
            Assert.That(_backend.Searches, Is.EqualTo(2));
            _backend.CompleteSearch(null, "TimedOut");

            launcher.Tick(10f);
            Assert.That(_backend.Searches, Is.EqualTo(3));
            _backend.CompleteSearch(new List<LobbyInfo> { Server("s1", "203.0.113.1:7777", 0) });

            Assert.That(_failure, Is.Null);
            Assert.That(_ready.Address, Is.EqualTo("203.0.113.1"));
        }

        [Test]
        public void QuickMatchGivesUpAfterThirtySecondsWithTheReason()
        {
            var launcher = BeginQuickMatch();
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(Array.Empty<LobbyInfo>());

            for (var now = 5f; now < 30f; now += 5f)
            {
                launcher.Tick(now);
                _backend.CompleteSearch(new List<LobbyInfo> { Server("old", "203.0.113.1:7777", 0, "0.2.0") });
            }

            Assert.That(_backend.Searches, Is.EqualTo(6), "searched at 0, 5, 10, 15, 20 and 25 seconds");
            launcher.Tick(29.9f);
            Assert.That(_failure, Is.Null);

            launcher.Tick(30f);
            Assert.That(_failure, Does.Contain("no quick match server found after 30s").And.Contain("0.2.0"));
            Assert.That(_ready, Is.Null);
            Assert.That(launcher.IsFinished, Is.True);
            Assert.That(_backend.Searches, Is.EqualTo(6));
        }

        [Test]
        public void QuickMatchIgnoresAServerFoundAfterGivingUp()
        {
            var launcher = BeginQuickMatch();
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(Array.Empty<LobbyInfo>());
            launcher.Tick(25f);

            launcher.Tick(30f);
            _backend.CompleteSearch(new List<LobbyInfo> { Server("late", "203.0.113.1:7777", 0) });

            Assert.That(_failure, Does.Contain("no quick match server found"));
            Assert.That(_ready, Is.Null);
        }

        [Test]
        public void CreateLogsInCreatesAndStartsAHost()
        {
            var launcher = Begin(LobbyLaunchOptions.Create(4));
            _backend.CompleteLogin(_self);

            Assert.That(_backend.CreatedMaxMembers, Is.EqualTo(4));
            _backend.CompleteCreate(Lobby("l1", _self, _self));

            Assert.That(_failure, Is.Null);
            Assert.That(_ready.Mode, Is.EqualTo(NetLaunchMode.Host));
            Assert.That(_ready.Transport, Is.EqualTo(NetTransportKind.Eos));
            Assert.That(launcher.IsFinished, Is.True);
        }

        [Test]
        public void JoinSearchesAndConnectsToTheOwner()
        {
            Begin(LobbyLaunchOptions.Join());
            _backend.CompleteLogin(_self);

            Assert.That(_backend.Searches, Is.EqualTo(1));
            _backend.CompleteSearch(new List<LobbyInfo> { Lobby("l1", _owner, _owner) });

            Assert.That(_backend.JoinedId, Is.EqualTo("l1"));
            _backend.CompleteJoin(Lobby("l1", _owner, _owner, _self));

            Assert.That(_failure, Is.Null);
            Assert.That(_ready.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(_ready.EosServerId, Is.EqualTo(_owner));
            Assert.That(_service.Current.Id, Is.EqualTo("l1"));
        }

        [Test]
        public void JoinByIdSkipsTheSearch()
        {
            Begin(LobbyLaunchOptions.Join("l9"));
            _backend.CompleteLogin(_self);

            Assert.That(_backend.Searches, Is.Zero);
            Assert.That(_backend.JoinedId, Is.EqualTo("l9"));
        }

        [Test]
        public void JoinByIdFailsWithoutRetrying()
        {
            Begin(LobbyLaunchOptions.Join("l9"));
            _backend.CompleteLogin(_self);
            _backend.CompleteJoin(null, "NotFound");

            Assert.That(_failure, Does.Contain("l9").And.Contain("NotFound"));
            Assert.That(_backend.Searches, Is.Zero);
        }

        [Test]
        public void SearchesAgainUntilALobbyAppears()
        {
            var launcher = Begin(LobbyLaunchOptions.Join());
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(new List<LobbyInfo> { Lobby("mine", _self, _self) });

            launcher.Tick(_interval - 0.1f);
            Assert.That(_backend.Searches, Is.EqualTo(1));

            launcher.Tick(_interval + 0.1f);
            Assert.That(_backend.Searches, Is.EqualTo(2));

            _backend.CompleteSearch(null, "TimedOut");
            launcher.Tick(2 * _interval + 0.2f);
            Assert.That(_backend.Searches, Is.EqualTo(3));

            _backend.CompleteSearch(new List<LobbyInfo> { Lobby("l1", _owner, _owner) });
            _backend.CompleteJoin(null, "LobbyTooManyPlayers");
            launcher.Tick(3 * _interval + 0.3f);
            Assert.That(_backend.Searches, Is.EqualTo(4));
            Assert.That(_failure, Is.Null);
        }

        [Test]
        public void GivesUpAfterTheTimeout()
        {
            var launcher = Begin(LobbyLaunchOptions.Join());
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(Array.Empty<LobbyInfo>());

            launcher.Tick(_timeout);

            Assert.That(_failure, Does.Contain("gave up"));
            Assert.That(_ready, Is.Null);
            Assert.That(launcher.IsFinished, Is.True);
        }

        [Test]
        public void TimeoutDuringJoinLeavesTheLateLobby()
        {
            var launcher = Begin(LobbyLaunchOptions.Join());
            _backend.CompleteLogin(_self);
            _backend.CompleteSearch(new List<LobbyInfo> { Lobby("l1", _owner, _owner) });

            launcher.Tick(_timeout);
            _backend.CompleteJoin(Lobby("l1", _owner, _owner, _self));

            Assert.That(_failure, Does.Contain("joining"));
            Assert.That(_ready, Is.Null);
            Assert.That(_backend.Leaves, Is.EqualTo(new[] { "leave:l1" }));
        }

        [Test]
        public void LoginFailureFails()
        {
            Begin(LobbyLaunchOptions.Create());
            _backend.CompleteLogin(null, "InvalidCredentials");

            Assert.That(_failure, Does.Contain("InvalidCredentials"));
            Assert.That(_ready, Is.Null);
        }

        [Test]
        public void CreateFailureFails()
        {
            Begin(LobbyLaunchOptions.Create());
            _backend.CompleteLogin(_self);
            _backend.CompleteCreate(null, "TooManyRequests");

            Assert.That(_failure, Does.Contain("TooManyRequests"));
        }

        [Test]
        public void TicksTheServiceEvenAfterFinishing()
        {
            var launcher = Begin(LobbyLaunchOptions.Create());
            _backend.CompleteLogin(_self);
            _backend.CompleteCreate(Lobby("l1", _self, _self));

            launcher.Tick(1f);
            launcher.Tick(_timeout + 1f);

            Assert.That(_backend.Ticks, Is.EqualTo(2));
            Assert.That(_failure, Is.Null);
        }

        [Test]
        public void RejectsMisuse()
        {
            Assert.Throws<ArgumentException>(() => _ = new LobbyLauncher(_service, LobbyLaunchOptions.None, _version));
            Assert.Throws<ArgumentNullException>(() => _ = new LobbyLauncher(_service, null, _version));
            var launcher = Begin(LobbyLaunchOptions.Create());
            Assert.Throws<InvalidOperationException>(() => launcher.Begin(0f, options => { }, reason => { }));
        }
    }
}

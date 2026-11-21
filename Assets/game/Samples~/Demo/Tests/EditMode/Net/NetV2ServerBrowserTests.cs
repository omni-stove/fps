using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Runtime;

namespace Shooter.Net.Tests
{
    public sealed class NetV2ServerBrowserTests
    {
        private const string _self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _owner = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string _version = "0.3.0";

        private readonly List<FakeLobbyBackend> _backends = new List<FakeLobbyBackend>();
        private readonly List<ServerJoinResult> _connected = new List<ServerJoinResult>();
        private readonly List<ServerJoinOutcome> _outcomes = new List<ServerJoinOutcome>();
        private readonly List<string> _logs = new List<string>();
        private string _refusal;
        private int _partySize;
        private NetV2ServerBrowser _browser;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ServerBrowser.Clear();
            GameSignals.ServerBrowserControl.Clear();
            _backends.Clear();
            _connected.Clear();
            _outcomes.Clear();
            _logs.Clear();
            _refusal = null;
            _partySize = 1;
            _browser = new NetV2ServerBrowser(CreateBackend, null, _version, () => _partySize, Connect, _logs.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _browser.Dispose();
            GameSignals.ServerBrowser.Clear();
            GameSignals.ServerBrowserControl.Clear();
        }

        [Test]
        public void ItIsPublishedAsTheControlAndDisposingWithdrawsItAndTheList()
        {
            Assert.That(GameSignals.ServerBrowserControl.Value, Is.SameAs(_browser));
            Load(Server("one", "203.0.113.1:7777", 1));
            Assert.That(GameSignals.ServerBrowser.HasValue, Is.True);

            _browser.Dispose();

            Assert.That(GameSignals.ServerBrowserControl.HasValue, Is.False);
            Assert.That(GameSignals.ServerBrowser.Value, Is.Null);
            Assert.That(_backends[0].Disposed, Is.True);
        }

        [Test]
        public void TheListIsPublishedAsItLoads()
        {
            Assert.That(_browser.Refresh(), Is.True);
            Assert.That(GameSignals.ServerBrowser.Value.Value.Stage, Is.EqualTo(ServerBrowserStage.Loading));

            _backends[0].CompleteLogin(_self);
            _backends[0].CompleteSearch(new[] { Server("one", "203.0.113.1:7777", 1), Server("two", "203.0.113.2:7777", 2) });

            var status = GameSignals.ServerBrowser.Value.Value;
            Assert.That(status.Stage, Is.EqualTo(ServerBrowserStage.Ready));
            Assert.That(status.Servers, Has.Count.EqualTo(2));
        }

        [Test]
        public void APickedServerIsConnectedWithoutAVoiceLobbyItCannotJoin()
        {
            Load(Server("one", "203.0.113.1:7790", 1));

            _browser.Join("one", _outcomes.Add);

            Assert.That(_outcomes, Is.EqualTo(new[] { ServerJoinOutcome.Joined }));
            Assert.That(_connected, Has.Count.EqualTo(1));
            Assert.That(_connected[0].Options.Address, Is.EqualTo("203.0.113.1"));
            Assert.That(_connected[0].Options.Port, Is.EqualTo(7790));
            Assert.That(_connected[0].MatchLobby, Is.Null);
            Assert.That(_backends[0].MatchLobbyJoins, Is.EqualTo(new[] { "one" }), "it tries the server's voice lobby first");
            Assert.That(_logs, Has.Member(ServerBrowserLog.Connecting("203.0.113.1:7790")));
        }

        [Test]
        public void ThePartySizeComesFromTheParty()
        {
            Load(Server("six", "203.0.113.1:7777", 6));
            _partySize = 3;

            _browser.Join("six", _outcomes.Add);

            Assert.That(_outcomes, Is.EqualTo(new[] { ServerJoinOutcome.Full }));
            Assert.That(_connected, Is.Empty);
        }

        [Test]
        public void AServerThatCannotBeConnectedIsAFailure()
        {
            _refusal = "already in an online match";
            Load(Server("one", "203.0.113.1:7777", 1));

            _browser.Join("one", _outcomes.Add);

            Assert.That(_outcomes, Is.EqualTo(new[] { ServerJoinOutcome.Failed }));
            Assert.That(_logs, Has.Member(ServerBrowserLog.NotJoined("one", ServerJoinOutcome.Failed, ServerBrowserLog.NotConnected(_refusal))));
        }

        [Test]
        public void ClosingClearsTheListAndDropsTheServiceOnTheNextTick()
        {
            Load(Server("one", "203.0.113.1:7777", 1));

            _browser.Close();

            Assert.That(GameSignals.ServerBrowser.Value, Is.Null);
            Assert.That(_backends[0].Disposed, Is.False);
            _browser.Tick(1f);
            Assert.That(_backends[0].Disposed, Is.True);
        }

        [Test]
        public void ADisposedBrowserRefusesEverything()
        {
            _browser.Dispose();

            Assert.That(_browser.Refresh(), Is.False);
            Assert.Throws<ObjectDisposedException>(() => _browser.Join("one", _outcomes.Add));
            Assert.Throws<ArgumentNullException>(() => new NetV2ServerBrowser(CreateBackend, null, _version, null, Connect, _logs.Add));
            Assert.Throws<ArgumentNullException>(() => new NetV2ServerBrowser(CreateBackend, null, _version, () => 1, null, _logs.Add));
        }

        private void Load(params LobbyInfo[] lobbies)
        {
            Assert.That(_browser.Refresh(), Is.True);
            _backends[_backends.Count - 1].CompleteLogin(_self);
            _backends[_backends.Count - 1].CompleteSearch(lobbies);
        }

        private ILobbyBackend CreateBackend()
        {
            var backend = new FakeLobbyBackend();
            _backends.Add(backend);
            return backend;
        }

        private string Connect(ServerJoinResult result)
        {
            if (_refusal != null)
            {
                return _refusal;
            }

            _connected.Add(result);
            return null;
        }

        private static LobbyInfo Server(string id, string endpoint, int players)
        {
            return new LobbyInfo(
                id, _owner, ServerLobby.MaxMembers(NetMatchSettings.DefaultMaxPlayers), new[] { _owner },
                ServerLobby.Attributes(endpoint, _version, players, NetMatchSettings.Default));
        }
    }
}

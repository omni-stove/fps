using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fps.Net.Tests
{
    public sealed class ServerLobbyRegistrarTests
    {
        private const string _server = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        private const string _endpoint = "203.0.113.10:7777";
        private const string _version = "0.3.0";
        private const float _interval = 5f;
        private const float _timeout = 60f;

        private FakeLobbyBackend _backend;
        private LobbyService _service;
        private ServerLobbyRegistrar _registrar;
        private string _failure;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeLobbyBackend();
            _service = new LobbyService(_backend, ServerLobby.BucketId(_version));
            _registrar = new ServerLobbyRegistrar(_service, _endpoint, _version, new RetrySchedule(_interval, _timeout));
            _failure = null;
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        private void Begin()
        {
            _registrar.Begin(0f, reason => _failure = reason);
        }

        private void Register(string lobbyId = "l1")
        {
            Begin();
            _backend.CompleteLogin(_server);
            _backend.CompleteCreate(Lobby(lobbyId));
        }

        private static LobbyInfo Lobby(string id)
        {
            return new LobbyInfo(id, _server, ServerLobby.MaxPlayers, new[] { _server });
        }

        private static Dictionary<string, LobbyAttribute> ByKey(IReadOnlyList<LobbyAttribute> attributes)
        {
            var byKey = new Dictionary<string, LobbyAttribute>();
            foreach (var attribute in attributes)
            {
                byKey.Add(attribute.Key, attribute);
            }

            return byKey;
        }

        private static long PlayersIn(IReadOnlyList<LobbyAttribute> attributes)
        {
            return ByKey(attributes)[ServerLobby.PlayersKey].Number;
        }

        [Test]
        public void LogsInAndCreatesALobbyWithTheServerAttributes()
        {
            Begin();

            Assert.That(_backend.Logins, Is.EqualTo(1));
            _backend.CompleteLogin(_server);

            Assert.That(_backend.CreatedBucket, Is.EqualTo("fps-server:" + _version));
            Assert.That(_backend.CreatedMaxMembers, Is.EqualTo(ServerLobby.MaxPlayers));
            var attributes = ByKey(_backend.CreatedAttributes);
            Assert.That(attributes[ServerLobby.ServerKey].Text, Is.EqualTo(_endpoint));
            Assert.That(attributes[ServerLobby.ModeKey].Text, Is.EqualTo("tdm"));
            Assert.That(attributes[ServerLobby.VersionKey].Text, Is.EqualTo(_version));
            Assert.That(attributes[ServerLobby.PlayersKey].Number, Is.EqualTo(0));
            Assert.That(_registrar.IsRegistered, Is.False);

            _backend.CompleteCreate(Lobby("l1"));

            Assert.That(_registrar.IsRegistered, Is.True);
            Assert.That(_registrar.HasRegistered, Is.True);
            Assert.That(_registrar.PublishedPlayers, Is.EqualTo(0));
            Assert.That(_service.Current.Id, Is.EqualTo("l1"));
        }

        [Test]
        public void EachJoinAndLeaveUpdatesThePlayersAttribute()
        {
            Register();

            _registrar.SetPlayers(1);

            Assert.That(_backend.UpdatedLobbyId, Is.EqualTo("l1"));
            Assert.That(PlayersIn(_backend.Updates[0]), Is.EqualTo(1));
            _backend.CompleteUpdate();
            Assert.That(_registrar.PublishedPlayers, Is.EqualTo(1));

            _registrar.SetPlayers(0);

            Assert.That(PlayersIn(_backend.Updates[1]), Is.EqualTo(0));
            _backend.CompleteUpdate();
            Assert.That(_registrar.PublishedPlayers, Is.EqualTo(0));
        }

        [Test]
        public void ChangesDuringAnUpdateAreSentAsTheLatestCountAfterIt()
        {
            Register();

            _registrar.SetPlayers(1);
            _registrar.SetPlayers(2);
            _registrar.SetPlayers(3);

            Assert.That(_backend.Updates.Count, Is.EqualTo(1));
            _backend.CompleteUpdate();

            Assert.That(_backend.Updates.Count, Is.EqualTo(2));
            Assert.That(PlayersIn(_backend.Updates[1]), Is.EqualTo(3));
            _backend.CompleteUpdate();
            Assert.That(_registrar.PublishedPlayers, Is.EqualTo(3));
        }

        [Test]
        public void PlayersWhoJoinBeforeRegistrationAreInTheCreatedLobby()
        {
            Begin();
            _registrar.SetPlayers(2);
            _backend.CompleteLogin(_server);

            Assert.That(PlayersIn(_backend.CreatedAttributes), Is.EqualTo(2));
            _backend.CompleteCreate(Lobby("l1"));

            Assert.That(_backend.Updates, Is.Empty);
        }

        [Test]
        public void PlayersWhoJoinWhileCreatingAreSentOnceCreated()
        {
            Begin();
            _backend.CompleteLogin(_server);
            _registrar.SetPlayers(1);
            _backend.CompleteCreate(Lobby("l1"));

            Assert.That(_backend.Updates.Count, Is.EqualTo(1));
            Assert.That(PlayersIn(_backend.Updates[0]), Is.EqualTo(1));
        }

        [Test]
        public void FailedUpdatesAreRetriedAfterTheInterval()
        {
            Register();
            _registrar.SetPlayers(1);
            _backend.CompleteUpdate("throttled");

            _registrar.Tick(_interval - 0.1f);
            Assert.That(_backend.Updates.Count, Is.EqualTo(1));

            _registrar.Tick(_interval);
            Assert.That(_backend.Updates.Count, Is.EqualTo(2));
            _backend.CompleteUpdate();
            Assert.That(_registrar.PublishedPlayers, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedUpdateFailuresCloseAndRecreateTheLobby()
        {
            Register();
            _registrar.SetPlayers(1);
            var now = 0f;
            for (var i = 0; i < ServerLobbyRegistrar.MaxUpdateFailures; i++)
            {
                _backend.CompleteUpdate("not found");
                now += _interval;
                _registrar.Tick(now);
            }

            Assert.That(_registrar.IsRegistered, Is.False);
            Assert.That(_backend.Leaves, Is.EqualTo(new[] { "destroy:l1" }));

            _backend.CompleteLeave();
            _registrar.Tick(now + _interval);

            Assert.That(_backend.Creates, Is.EqualTo(2));
            Assert.That(PlayersIn(_backend.CreatedAttributes), Is.EqualTo(1));
        }

        [Test]
        public void ALostLobbyIsRecreatedRightAway()
        {
            Register();
            _registrar.SetPlayers(1);
            _backend.CompleteUpdate();

            _backend.RaiseRemoved("l1", "this user was Disconnected");
            Assert.That(_registrar.IsRegistered, Is.False);

            _registrar.Tick(10f);

            Assert.That(_backend.Logins, Is.EqualTo(1));
            Assert.That(_backend.Creates, Is.EqualTo(2));
            Assert.That(PlayersIn(_backend.CreatedAttributes), Is.EqualTo(1));
            _backend.CompleteCreate(Lobby("l2"));
            Assert.That(_registrar.IsRegistered, Is.True);
            Assert.That(_service.Current.Id, Is.EqualTo("l2"));
        }

        [Test]
        public void AnUpdateToTheLostLobbyDoesNotCountForTheNewOne()
        {
            Register();
            _registrar.SetPlayers(1);
            _backend.RaiseRemoved("l1", "the lobby was closed");
            _registrar.Tick(1f);
            _registrar.SetPlayers(2);
            _backend.CompleteCreate(Lobby("l2"));

            Assert.That(PlayersIn(_backend.CreatedAttributes), Is.EqualTo(1));
            Assert.That(_backend.Updates.Count, Is.EqualTo(1));

            _backend.CompleteUpdate();

            Assert.That(_backend.Updates.Count, Is.EqualTo(2));
            Assert.That(_backend.UpdatedLobbyId, Is.EqualTo("l2"));
            Assert.That(PlayersIn(_backend.Updates[1]), Is.EqualTo(2));
        }

        [Test]
        public void ALostLoginLogsInAgainBeforeRecreating()
        {
            Register();

            _backend.RaiseLoggedOut("auth expired");
            _registrar.Tick(1f);

            Assert.That(_backend.Logins, Is.EqualTo(2));
            Assert.That(_backend.Creates, Is.EqualTo(1));
            _backend.CompleteLogin(_server);
            Assert.That(_backend.Creates, Is.EqualTo(2));
            _backend.CompleteCreate(Lobby("l2"));
            Assert.That(_registrar.IsRegistered, Is.True);
        }

        [Test]
        public void FailuresAreRetriedOnTheInterval()
        {
            Begin();
            _backend.CompleteLogin(null, "network down");

            _registrar.Tick(_interval - 0.1f);
            Assert.That(_backend.Logins, Is.EqualTo(1));

            _registrar.Tick(_interval);
            Assert.That(_backend.Logins, Is.EqualTo(2));
            _backend.CompleteLogin(_server);
            _backend.CompleteCreate(null, "rate limited");

            _registrar.Tick(_interval * 2f);
            Assert.That(_backend.Logins, Is.EqualTo(2));
            Assert.That(_backend.Creates, Is.EqualTo(2));
            Assert.That(_failure, Is.Null);
        }

        [Test]
        public void GivesUpWhenNeverRegisteredBeforeTheTimeout()
        {
            Begin();
            _backend.CompleteLogin(null, "invalid client credentials");

            _registrar.Tick(_timeout);

            Assert.That(_failure, Does.Contain("not registered").And.Contain("invalid client credentials"));
            _registrar.Tick(_timeout + _interval);
            Assert.That(_backend.Logins, Is.EqualTo(1));
        }

        [Test]
        public void KeepsTryingAfterTheTimeoutOnceRegistered()
        {
            Register();
            _backend.RaiseRemoved("l1", "the lobby was closed");
            _registrar.Tick(1f);
            _backend.CompleteCreate(null, "service unavailable");

            LogAssert.Expect(LogType.Error, new Regex("not registered after"));
            _registrar.Tick(1f + _timeout);

            Assert.That(_failure, Is.Null);
            Assert.That(_backend.Creates, Is.EqualTo(3));
        }

        [Test]
        public void CloseDestroysTheLobbyAndStopsRetrying()
        {
            Register();
            var closed = false;

            _registrar.Close(() => closed = true);

            Assert.That(_backend.Leaves, Is.EqualTo(new[] { "destroy:l1" }));
            Assert.That(closed, Is.False);
            _backend.CompleteLeave();
            Assert.That(closed, Is.True);

            _registrar.SetPlayers(1);
            _registrar.Tick(_timeout * 2f);
            Assert.That(_backend.Updates, Is.Empty);
            Assert.That(_backend.Creates, Is.EqualTo(1));
            Assert.That(_failure, Is.Null);
        }

        [Test]
        public void CloseWhileCreatingClosesTheLobbyOnceCreated()
        {
            Begin();
            _backend.CompleteLogin(_server);
            var closed = false;

            _registrar.Close(() => closed = true);
            _backend.CompleteCreate(Lobby("l1"));

            Assert.That(closed, Is.True);
            Assert.That(_backend.Leaves, Is.EqualTo(new[] { "destroy:l1" }));
            Assert.That(_registrar.IsRegistered, Is.False);
        }

        [Test]
        public void CloseBeforeBeginFinishesAtOnce()
        {
            var closed = false;

            _registrar.Close(() => closed = true);

            Assert.That(closed, Is.True);
            Assert.That(_backend.Logins, Is.Zero);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new ServerLobbyRegistrar(null, _endpoint, _version));
            Assert.Throws<ArgumentException>(() => _ = new ServerLobbyRegistrar(_service, "0.0.0.0:7777", _version));
            Assert.Throws<ArgumentException>(() => _ = new ServerLobbyRegistrar(_service, "example.com:7777", _version));
            Assert.Throws<ArgumentOutOfRangeException>(() => _registrar.SetPlayers(-1));
            Begin();
            Assert.Throws<InvalidOperationException>(Begin);
        }
    }
}

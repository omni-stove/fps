using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Eos.Tests
{
    public sealed class ServerLobbyRegistrarTests
    {
        private const string _server = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        private const string _endpoint = "203.0.113.10:7777";
        private const string _version = "0.3.0";
        private const float _interval = 5f;
        private const float _timeout = 60f;
        private const int _maxPlayers = 6;

        private StringWriter _log;
        private FakeLobbyBackend _backend;
        private ServerLobbyService _service;
        private ServerLobbyRegistrar _registrar;
        private string _failure;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _backend = new FakeLobbyBackend();
            _service = new ServerLobbyService(_backend, ServerLobby.BucketId(_version), _log);
            _registrar = new ServerLobbyRegistrar(_service, _endpoint, _version, _maxPlayers, new RetrySchedule(_interval, _timeout), _log);
            _failure = null;
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            _backend.Dispose();
            _log.Dispose();
        }

        [Test]
        public void LogsInAndCreatesALobbyWithTheServerAttributes()
        {
            Begin();

            Assert.That(_backend.Logins, Is.EqualTo(1));
            _backend.CompleteLogin(_server);

            Assert.That(_backend.CreatedBucket, Is.EqualTo("shooter-server:" + _version));
            Assert.That(_backend.CreatedMaxMembers, Is.EqualTo(_maxPlayers + 1), "the server takes a member slot besides the players");
            Assert.That(_backend.CreatedVoiceRoom, Is.True);
            var attributes = ByKey(_backend.CreatedAttributes);
            Assert.That(attributes[ServerLobby.ServerKey].Text, Is.EqualTo(_endpoint));
            Assert.That(attributes[ServerLobby.ModeKey].Text, Is.EqualTo("tdm"));
            Assert.That(attributes[ServerLobby.VersionKey].Text, Is.EqualTo(_version));
            Assert.That(attributes[ServerLobby.PlayersKey].Number, Is.Zero);
            Assert.That(attributes[ServerLobby.VoiceEnabledKey].IsText, Is.False);
            Assert.That(attributes[ServerLobby.VoiceEnabledKey].Number, Is.EqualTo(1));
            Assert.That(_registrar.IsRegistered, Is.False);

            _backend.CompleteCreate(Lobby("l1"));

            Assert.That(_registrar.IsRegistered, Is.True);
            Assert.That(_registrar.HasRegistered, Is.True);
            Assert.That(_registrar.PublishedPlayers, Is.Zero);
            Assert.That(_service.Current.Id, Is.EqualTo("l1"));
        }

        [Test]
        public void AFullSizedMatchKeepsTheLobbyWithinTheEosMemberLimit()
        {
            using var service = new ServerLobbyService(_backend, ServerLobby.BucketId(_version), _log);
            var registrar = new ServerLobbyRegistrar(
                service, _endpoint, _version, NetMatchSettings.MaxMaxPlayers, new RetrySchedule(_interval, _timeout), _log);

            registrar.Begin(0f, reason => _failure = reason);
            _backend.CompleteLogin(_server);

            Assert.That(_backend.CreatedMaxMembers, Is.EqualTo(LobbyLaunchOptions.MaxMaxPlayers));
            Assert.That(_backend.CreatedVoiceRoom, Is.True);
        }

        [Test]
        public void ARecreatedLobbyAlsoOpensTheVoiceRoom()
        {
            Register();

            _backend.RaiseRemoved("l1", "the lobby was closed");
            _registrar.Tick(1f);

            Assert.That(_backend.Creates, Is.EqualTo(2));
            Assert.That(_backend.CreatedVoiceRoom, Is.True);
            Assert.That(_backend.CreatedMaxMembers, Is.EqualTo(_maxPlayers + 1));
            Assert.That(ByKey(_backend.CreatedAttributes)[ServerLobby.VoiceEnabledKey].Number, Is.EqualTo(1));
        }

        [Test]
        public void ARecreatedLobbyKeepsTheRegion()
        {
            _registrar.Region = "europe";
            Register();

            Assert.That(ByKey(_backend.CreatedAttributes)[ServerLobby.RegionKey].Text, Is.EqualTo("europe"));

            _backend.RaiseRemoved("l1", "the lobby was closed");
            _registrar.Tick(1f);

            Assert.That(_backend.Creates, Is.EqualTo(2));
            Assert.That(ByKey(_backend.CreatedAttributes)[ServerLobby.RegionKey].Text, Is.EqualTo("europe"));
        }

        [Test]
        public void TheRegionIsSetOnlyBeforeBegin()
        {
            Assert.That(_registrar.Region, Is.Null);
            Assert.Throws<ArgumentException>(() => _registrar.Region = ControlSettings.AutoRegion);
            Assert.Throws<ArgumentException>(() => _registrar.Region = "eu west");

            _registrar.Region = "asia";
            Begin();

            Assert.Throws<InvalidOperationException>(() => _registrar.Region = "europe");
            Assert.That(_registrar.Region, Is.EqualTo("asia"));
            Assert.That(Lines()[0], Is.EqualTo("[Lobby] registering server=" + _endpoint + " bucket=shooter-server:" + _version + " region=asia"));
        }

        [Test]
        public void LogsTheSameLinesAsTheUnityServer()
        {
            Register();
            _registrar.SetPlayers(1);
            _backend.CompleteUpdate();
            _registrar.Close();
            _backend.CompleteDestroy();

            Assert.That(Lines(), Is.EqualTo(new[]
            {
                "[Lobby] registering server=" + _endpoint + " bucket=shooter-server:" + _version,
                "[Lobby] logged in with the device id user=" + _server,
                "[Lobby] created id=l1 max=" + (_maxPlayers + 1) + " owner=" + _server
                    + " rtc=on server=" + _endpoint + " mode=tdm version=" + _version + " players=0 voice_enabled=1",
                "[Lobby] members=1/" + (_maxPlayers + 1) + " [" + _server + "] id=l1",
                "[Lobby] registered id=l1 server=" + _endpoint + " players=0",
                "[Lobby] players 0 -> 1",
                "[Lobby] updated id=l1 players=1",
                "[Lobby] unregistering id=l1",
                "[Lobby] closed id=l1",
            }));
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

            Assert.That(PlayersIn(_backend.Updates[1]), Is.Zero);
            _backend.CompleteUpdate();
            Assert.That(_registrar.PublishedPlayers, Is.Zero);
        }

        [Test]
        public void ChangesDuringAnUpdateAreSentAsTheLatestCountAfterIt()
        {
            Register();

            _registrar.SetPlayers(1);
            _registrar.SetPlayers(2);
            _registrar.SetPlayers(3);

            Assert.That(_backend.Updates, Has.Count.EqualTo(1));
            _backend.CompleteUpdate();

            Assert.That(_backend.Updates, Has.Count.EqualTo(2));
            Assert.That(PlayersIn(_backend.Updates[1]), Is.EqualTo(3));
            _backend.CompleteUpdate();
            Assert.That(_registrar.PublishedPlayers, Is.EqualTo(3));
        }

        [Test]
        public void ASkillAverageChangeDuringAnUpdateIsSentAfterIt()
        {
            Register();

            _registrar.SetPlayers(1, 1000);
            _registrar.SetPlayers(1, 1100);

            Assert.That(_backend.Updates, Has.Count.EqualTo(1));
            Assert.That(ByKey(_backend.Updates[0])[ServerLobby.SkillAverageKey].Number, Is.EqualTo(1000));
            _backend.CompleteUpdate();

            Assert.That(_backend.Updates, Has.Count.EqualTo(2));
            var second = ByKey(_backend.Updates[1]);
            Assert.That(second.ContainsKey(ServerLobby.PlayersKey), Is.False, "only the changed attribute is sent");
            Assert.That(second[ServerLobby.SkillAverageKey].Number, Is.EqualTo(1100));
            _backend.CompleteUpdate();
            Assert.That(_registrar.PublishedSkillAverage, Is.EqualTo(1100));
            Assert.That(_registrar.PublishedPlayers, Is.EqualTo(1));
        }

        [Test]
        public void AFailedSkillAverageUpdateIsRetriedAfterTheInterval()
        {
            Register();
            _registrar.SetPlayers(0, 1000);
            _backend.CompleteUpdate("throttled");

            Assert.That(_registrar.PublishedSkillAverage, Is.Null);
            _registrar.Tick(_interval);

            Assert.That(_backend.Updates, Has.Count.EqualTo(2));
            Assert.That(ByKey(_backend.Updates[1])[ServerLobby.SkillAverageKey].Number, Is.EqualTo(1000));
            _backend.CompleteUpdate();
            Assert.That(_registrar.PublishedSkillAverage, Is.EqualTo(1000));
        }

        [Test]
        public void ARecreatedLobbyCarriesTheSkillAverage()
        {
            Register();
            _registrar.SetPlayers(2, 1300);
            _backend.CompleteUpdate();

            _backend.RaiseRemoved("l1", "the lobby was closed");
            _registrar.Tick(1f);

            Assert.That(_backend.Creates, Is.EqualTo(2));
            Assert.That(ByKey(_backend.CreatedAttributes)[ServerLobby.SkillAverageKey].Number, Is.EqualTo(1300));
        }

        [Test]
        public void ANullSkillAverageKeepsTheLastOne()
        {
            Register();
            _registrar.SetPlayers(1, 1300);
            _backend.CompleteUpdate();

            _registrar.SetPlayers(0, null);

            Assert.That(_registrar.SkillAverage, Is.EqualTo(1300));
            Assert.That(_backend.Updates, Has.Count.EqualTo(2));
            Assert.That(ByKey(_backend.Updates[1]).ContainsKey(ServerLobby.SkillAverageKey), Is.False);
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

            Assert.That(_backend.Updates, Has.Count.EqualTo(1));
            Assert.That(PlayersIn(_backend.Updates[0]), Is.EqualTo(1));
        }

        [Test]
        public void FailedUpdatesAreRetriedAfterTheInterval()
        {
            Register();
            _registrar.SetPlayers(1);
            _backend.CompleteUpdate("throttled");

            _registrar.Tick(_interval - 0.1f);
            Assert.That(_backend.Updates, Has.Count.EqualTo(1));

            _registrar.Tick(_interval);
            Assert.That(_backend.Updates, Has.Count.EqualTo(2));
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
            Assert.That(_backend.Destroys, Is.EqualTo(Ids("l1")));

            _backend.CompleteDestroy();
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
            Assert.That(_log.ToString(), Does.Contain("[Lobby] lost the server lobby (this user was Disconnected); re-creating it"));

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
            Assert.That(_backend.Updates, Has.Count.EqualTo(1));

            _backend.CompleteUpdate();

            Assert.That(_backend.Updates, Has.Count.EqualTo(2));
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

            Assert.That(_log.ToString(), Does.Contain("[Lobby] EOS login failed: network down; trying again in 5s"));
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

            Assert.That(_failure, Is.EqualTo(
                "the server is not registered after 60s (last error: EOS login failed: invalid client credentials)"));
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

            _registrar.Tick(1f + _timeout);

            Assert.That(_failure, Is.Null);
            Assert.That(_backend.Creates, Is.EqualTo(3));
            Assert.That(_log.ToString(), Does.Contain("not registered after 60s").And.Contain("still trying"));
        }

        [Test]
        public void CloseDestroysTheLobbyAndStopsRetrying()
        {
            Register();
            var closed = false;

            _registrar.Close(() => closed = true);

            Assert.That(_backend.Destroys, Is.EqualTo(Ids("l1")));
            Assert.That(closed, Is.False);
            _backend.CompleteDestroy();
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

            Assert.That(closed, Is.False);
            _backend.CompleteDestroy();
            Assert.That(closed, Is.True);
            Assert.That(_backend.Destroys, Is.EqualTo(Ids("l1")));
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
        public void APublicMatchHasNoPrivateOrJoinCodeAttributes()
        {
            Begin();
            _backend.CompleteLogin(_server);

            var attributes = ByKey(_backend.CreatedAttributes);
            Assert.That(attributes.ContainsKey(ServerLobby.PrivateKey), Is.False);
            Assert.That(attributes.ContainsKey(ServerLobby.JoinCodeKey), Is.False);
        }

        [Test]
        public void APrivateMatchPublishesThePrivateMarkAndTheJoinCode()
        {
            using var service = new ServerLobbyService(_backend, ServerLobby.BucketId(_version), _log);
            var registrar = new ServerLobbyRegistrar(
                service,
                _endpoint,
                _version,
                new NetMatchSettings(maxPlayers: 4, privateMatch: true, joinCode: "friends"),
                new RetrySchedule(_interval, _timeout),
                _log);

            registrar.Begin(0f, reason => _failure = reason);
            _backend.CompleteLogin(_server);

            Assert.That(registrar.MaxPlayers, Is.EqualTo(4));
            Assert.That(_backend.CreatedMaxMembers, Is.EqualTo(5));
            var attributes = ByKey(_backend.CreatedAttributes);
            Assert.That(attributes[ServerLobby.PrivateKey].IsText, Is.False);
            Assert.That(attributes[ServerLobby.PrivateKey].Number, Is.EqualTo(1));
            Assert.That(attributes[ServerLobby.JoinCodeKey].Text, Is.EqualTo("FRIENDS"));
        }

        [Test]
        public void ARecreatedPrivateLobbyKeepsTheJoinCode()
        {
            using var service = new ServerLobbyService(_backend, ServerLobby.BucketId(_version), _log);
            var registrar = new ServerLobbyRegistrar(
                service,
                _endpoint,
                _version,
                new NetMatchSettings(maxPlayers: _maxPlayers, privateMatch: true, joinCode: "ABC234"),
                new RetrySchedule(_interval, _timeout),
                _log);
            registrar.Begin(0f, reason => _failure = reason);
            _backend.CompleteLogin(_server);
            _backend.CompleteCreate(Lobby("l1"));

            _backend.RaiseRemoved("l1", "the lobby was closed");
            registrar.Tick(1f);

            Assert.That(_backend.Creates, Is.EqualTo(2));
            var attributes = ByKey(_backend.CreatedAttributes);
            Assert.That(attributes[ServerLobby.PrivateKey].Number, Is.EqualTo(1));
            Assert.That(attributes[ServerLobby.JoinCodeKey].Text, Is.EqualTo("ABC234"));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var schedule = new RetrySchedule(_interval, _timeout);
            Assert.Throws<ArgumentNullException>(
                () => _ = new ServerLobbyRegistrar(_service, _endpoint, _version, (NetMatchSettings)null, schedule, _log));
            Assert.Throws<ArgumentNullException>(() => _ = new ServerLobbyRegistrar(null, _endpoint, _version, _maxPlayers, schedule, _log));
            Assert.Throws<ArgumentException>(() => _ = new ServerLobbyRegistrar(_service, "0.0.0.0:7777", _version, _maxPlayers, schedule, _log));
            Assert.Throws<ArgumentException>(() => _ = new ServerLobbyRegistrar(_service, "example.com:7777", _version, _maxPlayers, schedule, _log));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ServerLobbyRegistrar(_service, _endpoint, _version, 0, schedule, _log));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ServerLobbyRegistrar(_service, _endpoint, _version, 65, schedule, _log));
            Assert.Throws<ArgumentOutOfRangeException>(() => _registrar.SetPlayers(-1));
            Begin();
            Assert.Throws<InvalidOperationException>(Begin);
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

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Ids(params string[] ids) => ids;

        private string[] Lines()
        {
            return _log.ToString().Replace("\r", "", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        }

        private static LobbyInfo Lobby(string id)
        {
            return new LobbyInfo(id, _server, ServerLobby.MaxMembers(_maxPlayers), new[] { _server });
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
    }
}

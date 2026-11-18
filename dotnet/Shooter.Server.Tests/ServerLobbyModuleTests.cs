using System;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Server.Eos;
using Shooter.Server.Eos.Tests;

namespace Shooter.Server.Tests
{
    public sealed class ServerLobbyModuleTests
    {
        private const string _server = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        private const string _endpoint = "203.0.113.10:7777";

        private StringWriter _log;
        private UdpMatchTransport _transport;
        private FakeLobbyBackend _backend;
        private StubControl _control;
        private ServerMatch _match;
        private ServerLobbyModule _module;
        private float _now;
        private int _slept;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new UdpMatchTransport();
            _backend = new FakeLobbyBackend();
            _control = new StubControl();
            _match = new ServerMatch(0f, _log, new ServerEventWriter(false, _log, _log), () => DateTime.UtcNow);
            _now = 0f;
            _slept = 0;
            _module = new ServerLobbyModule(
                _ => _backend,
                _endpoint,
                () => _now,
                milliseconds =>
                {
                    _slept += milliseconds;
                    _now += milliseconds / 1000f;
                });
        }

        [TearDown]
        public void TearDown()
        {
            _module.Dispose();
            _backend.Dispose();
            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void RegistersOnTheFirstTickNotAtStart()
        {
            Start();

            Assert.That(_backend.Logins, Is.Zero);

            Tick();

            Assert.That(_backend.Logins, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] registering server=" + _endpoint + " bucket=shooter-server:0.3.0"));
        }

        [Test]
        public void PublishesTheConnectedPlayers()
        {
            Register();

            _match.Join(new ConnectionId(1), "device1");
            Tick();

            Assert.That(_backend.Updates, Has.Count.EqualTo(1));
            Assert.That(_backend.Updates[0][0].Number, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] players 0 -> 1"));
        }

        [Test]
        public void PublishesTheSkillAverageOfThePlayersAsTheyJoinAndLeave()
        {
            Register();

            _match.Join(new ConnectionId(1), "device1", skillRating: 1000);
            Assert.That(_module.SkillAverage, Is.EqualTo(1000));
            _match.Join(new ConnectionId(2), "device2", skillRating: 1400);
            _match.Join(new ConnectionId(3), "device3");
            Assert.That(_module.SkillAverage, Is.EqualTo(1200), "a player without a value is not counted");
            Tick();

            Assert.That(_backend.Updates, Has.Count.EqualTo(1), "the players and the average go in one update");
            Assert.That(_backend.Updates[0], Has.Some.Matches<LobbyAttribute>(a => a.Key == ServerLobby.PlayersKey && a.Number == 3));
            Assert.That(_backend.Updates[0], Has.Some.Matches<LobbyAttribute>(a => a.Key == ServerLobby.SkillAverageKey && a.Number == 1200));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] skill average - -> 1200"));
            _backend.CompleteUpdate();

            _match.Leave(new ConnectionId(1));
            Assert.That(_module.SkillAverage, Is.EqualTo(1400));
            Tick();

            Assert.That(_backend.Updates, Has.Count.EqualTo(2));
            Assert.That(_backend.Updates[1], Has.Some.Matches<LobbyAttribute>(a => a.Key == ServerLobby.PlayersKey && a.Number == 2));
            Assert.That(_backend.Updates[1], Has.Some.Matches<LobbyAttribute>(a => a.Key == ServerLobby.SkillAverageKey && a.Number == 1400));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] skill average 1200 -> 1400"));
            _backend.CompleteUpdate();
            Assert.That(_module.Registration.Registrar.PublishedSkillAverage, Is.EqualTo(1400));
        }

        [Test]
        public void APlayerWithoutAValueChangesOnlyThePlayers()
        {
            Register();
            _match.Join(new ConnectionId(1), "device1", skillRating: 1000);
            Tick();
            _backend.CompleteUpdate();

            _match.Join(new ConnectionId(2), "device2");
            Tick();

            Assert.That(_backend.Updates, Has.Count.EqualTo(2));
            Assert.That(_backend.Updates[1], Has.Count.EqualTo(1));
            Assert.That(_backend.Updates[1][0].Key, Is.EqualTo(ServerLobby.PlayersKey));
        }

        [Test]
        public void KeepsTheLastAverageWhenNoPlayerWithAValueIsLeft()
        {
            Register();
            _match.Join(new ConnectionId(1), "device1", skillRating: 1000);
            Tick();
            _backend.CompleteUpdate();

            _match.Leave(new ConnectionId(1));
            Assert.That(_module.SkillAverage, Is.Null);
            Tick();

            Assert.That(_backend.Updates, Has.Count.EqualTo(2));
            Assert.That(_backend.Updates[1], Has.None.Matches<LobbyAttribute>(a => a.Key == ServerLobby.SkillAverageKey));
            _backend.CompleteUpdate();
            Assert.That(_module.Registration.Registrar.PublishedSkillAverage, Is.EqualTo(1000));
        }

        [Test]
        public void PlayersWhoJoinBeforeRegistrationPutTheirAverageInTheCreatedLobby()
        {
            Start();
            _match.Join(new ConnectionId(1), "device1", skillRating: 1500);
            Tick();
            _backend.CompleteLogin(_server);

            Assert.That(_backend.CreatedAttributes, Has.Some.Matches<LobbyAttribute>(a => a.Key == ServerLobby.SkillAverageKey && a.Number == 1500));
            _backend.CompleteCreate(new LobbyInfo("l1", _server, NetMatchSettings.DefaultMaxPlayers, new[] { _server }, _backend.CreatedAttributes));

            Assert.That(_backend.Updates, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Lobby] registered id=l1 server=" + _endpoint + " players=1 skill_average=1500"));
        }

        [Test]
        public void ALobbyWithoutRatedPlayersHasNoSkillAverage()
        {
            Start();
            _match.Join(new ConnectionId(1), "device1");
            Tick();
            _backend.CompleteLogin(_server);

            Assert.That(_module.SkillAverage, Is.Null);
            Assert.That(_backend.CreatedAttributes, Has.None.Matches<LobbyAttribute>(a => a.Key == ServerLobby.SkillAverageKey));
        }

        [Test]
        public void StopsFollowingTheMatchAfterStop()
        {
            Register();
            _module.Stop();

            _match.Join(new ConnectionId(1), "device1", skillRating: 1000);

            Assert.That(_module.SkillAverage, Is.Null);
        }

        [Test]
        public void ClosesTheLobbyWhenTheServerStartsDraining()
        {
            Register();

            _control.IsDraining = true;
            Tick();

            Assert.That(_backend.Destroys, Has.Count.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] unregistering id=l1"));

            _match.Join(new ConnectionId(1), "device1");
            _backend.CompleteDestroy();
            Tick();
            Assert.That(_backend.Updates, Is.Empty);
            Assert.That(_backend.Creates, Is.EqualTo(1));
        }

        [Test]
        public void FailsTheServerWithExitSevenWhenNeverRegistered()
        {
            Start();
            Tick();
            _backend.CompleteLogin(null, "invalid client credentials");

            _now = ServerLobby.TimeoutSeconds;
            Tick();

            Assert.That(_control.ExitCode, Is.EqualTo(ServerProgram.ExitLobbyFailed));
            Assert.That(_control.Failures, Has.Count.EqualTo(1));
            Assert.That(_control.Failures[0], Does.StartWith("[Lobby] the server is not registered after 60s"));
        }

        [Test]
        public void StopWaitsForTheLobbyToCloseAndReleasesTheBackend()
        {
            Register();
            _backend.OnTick = () =>
            {
                if (_backend.PendingDestroy != null && _slept >= 50)
                {
                    _backend.CompleteDestroy();
                }
            };

            _module.Stop();

            Assert.That(_backend.Destroys, Has.Count.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] closing the server lobby before quitting (up to 3s)"));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] closed id=l1"));
            Assert.That(_log.ToString(), Does.Not.Contain("did not close"));
            Assert.That(_backend.Disposed, Is.True);
        }

        [Test]
        public void StopGivesUpAfterTheWait()
        {
            Register();

            _module.Stop();

            Assert.That(_now, Is.GreaterThanOrEqualTo(ServerLobbyRegistration.CloseWaitSeconds));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] the server lobby did not close within 3s; quitting anyway"));
            Assert.That(_backend.Disposed, Is.True);
        }

        [Test]
        public void StopBeforeAnyTickDoesNotWait()
        {
            Start();

            _module.Stop();

            Assert.That(_slept, Is.Zero);
            Assert.That(_backend.Logins, Is.Zero);
            Assert.That(_backend.Disposed, Is.True);
        }

        [Test]
        public void APrivateMatchPublishesTheJoinCodeSoQuickMatchSkipsIt()
        {
            Start(TestRules.Data(settings: new NetMatchSettings(privateMatch: true, joinCode: "FRIENDS")));
            Tick();
            _backend.CompleteLogin(_server);

            var attributes = _backend.CreatedAttributes;
            Assert.That(attributes, Has.Some.Matches<LobbyAttribute>(a => a.Key == ServerLobby.PrivateKey && a.Number == 1));
            Assert.That(attributes, Has.Some.Matches<LobbyAttribute>(a => a.Key == ServerLobby.JoinCodeKey && a.Text == "FRIENDS"));

            var lobby = new LobbyInfo("l1", _server, _backend.CreatedMaxMembers, new[] { _server }, attributes);
            Assert.That(lobby.IsPrivate, Is.True);
            Assert.That(lobby.JoinCode, Is.EqualTo("FRIENDS"));
            Assert.That(LobbySelector.ServerRejection(lobby, "0.3.0"), Is.EqualTo("private"));
            Assert.That(LobbySelector.PickServer(new[] { lobby }, "0.3.0"), Is.Null);
        }

        [Test]
        public void APublicMatchHasNoJoinCode()
        {
            Start(TestRules.Data());
            Tick();
            _backend.CompleteLogin(_server);

            Assert.That(_backend.CreatedAttributes, Has.None.Matches<LobbyAttribute>(a => a.Key == ServerLobby.PrivateKey));
            Assert.That(_backend.CreatedAttributes, Has.None.Matches<LobbyAttribute>(a => a.Key == ServerLobby.JoinCodeKey));
        }

        [Test]
        public void TheRegionGoesIntoTheCreatedLobbyAndTheLog()
        {
            _module.Region = "europe";
            Start();
            Tick();
            _backend.CompleteLogin(_server);

            Assert.That(_backend.CreatedAttributes, Has.Some.Matches<LobbyAttribute>(a => a.Key == ServerLobby.RegionKey && a.Text == "europe"));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] registering server=" + _endpoint + " bucket=shooter-server:0.3.0 region=europe"));

            var lobby = new LobbyInfo("l1", _server, _backend.CreatedMaxMembers, new[] { _server }, _backend.CreatedAttributes);
            _backend.CompleteCreate(lobby);

            Assert.That(lobby.Region, Is.EqualTo("europe"));
            Assert.That(LobbySelector.PickServer(new[] { lobby }, "0.3.0", 1, null, "europe"), Is.SameAs(lobby));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] registered id=l1 server=" + _endpoint + " players=0 region=europe"));
        }

        [Test]
        public void TheRegionIsNotSentAgainWhenThePlayersChange()
        {
            _module.Region = "asia";
            Start();
            Tick();
            _backend.CompleteLogin(_server);
            _backend.CompleteCreate(new LobbyInfo("l1", _server, NetMatchSettings.DefaultMaxPlayers, new[] { _server }, _backend.CreatedAttributes));

            _match.Join(new ConnectionId(1), "device1");
            Tick();

            Assert.That(_backend.Updates, Has.Count.EqualTo(1));
            Assert.That(_backend.Updates[0], Has.None.Matches<LobbyAttribute>(a => a.Key == ServerLobby.RegionKey));
        }

        [Test]
        public void WithoutARegionTheLobbyHasNone()
        {
            Start();
            Tick();
            _backend.CompleteLogin(_server);

            Assert.That(_module.Region, Is.Null);
            Assert.That(_backend.CreatedAttributes, Has.None.Matches<LobbyAttribute>(a => a.Key == ServerLobby.RegionKey));
            Assert.That(_log.ToString(), Does.Not.Contain("region="));
        }

        [Test]
        public void TheRegionIsSetOnlyBeforeStartAndMustBeAServerRegion()
        {
            Assert.Throws<ArgumentException>(() => _module.Region = ControlSettings.AutoRegion);
            Assert.Throws<ArgumentException>(() => _module.Region = "Europe");
            Assert.That(_module.Region, Is.Null);

            _module.Region = "europe";
            _module.Region = null;
            Assert.That(_module.Region, Is.Null);

            Start();

            Assert.Throws<InvalidOperationException>(() => _module.Region = "asia");
        }

        private void Start(ServerRulesData rules = null)
        {
            _module.Start(new ServerContext(
                new NetLaunchOptions(
                    NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp, null, _endpoint),
                "0.3.0",
                _log,
                TestMaps.Data(),
                _match,
                new ServerMessenger(_transport),
                _control,
                rules));
        }

        private void Register()
        {
            Start();
            Tick();
            _backend.CompleteLogin(_server);
            _backend.CompleteCreate(new LobbyInfo("l1", _server, NetMatchSettings.DefaultMaxPlayers, new[] { _server }));
        }

        private void Tick()
        {
            _module.Tick(1, 1f / 60f);
        }
    }
}

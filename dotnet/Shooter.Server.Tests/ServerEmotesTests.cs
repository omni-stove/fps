using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 移動・ルール・戦闘・エモートを同じホストで動かし、要求したエモートが全員へ配られ、オフラインと同じ条件で止まることを確かめる
    public sealed class ServerEmotesTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _waveId = "wave";
        private const string _danceId = "dance";
        private const string _standId = "stand";

        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);

        // 手を振るのは 1 秒で終わり、踊るのは止めるまで続き、立ち姿は動いても止めない
        private static readonly IReadOnlyList<EmoteSpec> _emotes = new[]
        {
            new EmoteSpec(_waveId, "emote.wave", durationSeconds: 1f),
            new EmoteSpec(_danceId, "emote.dance", durationSeconds: 2f, loops: true),
            new EmoteSpec(_standId, "emote.stand", durationSeconds: 5f, stopsOnMove: false),
        };

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerCombat _combat;
        private ServerEmotes _emotesModule;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void APlayedEmoteIsSentToEveryPlayerIncludingThePerformer()
        {
            StartWithTwo();

            Request(1, _waveId);

            foreach (var receiver in new[] { 1, 2 })
            {
                var states = States(receiver);
                Assert.That(states, Has.Count.EqualTo(1), "player " + receiver);
                Assert.That(states[0].PlayerId, Is.EqualTo(1));
                Assert.That(states[0].Playing, Is.True);
                Assert.That(states[0].EmoteId, Is.EqualTo(_waveId));
            }

            Assert.That(_emotesModule.IsPlaying(1, out var id), Is.True);
            Assert.That(id, Is.EqualTo(_waveId));
            Assert.That(_log.ToString(), Does.Contain("[Emote] server played player=1 id=wave seconds=1 loops=False stopsOnMove=True to=2"));
        }

        [Test]
        public void TheStartLineShowsTheRuleAndTheEmoteCount()
        {
            StartWithTwo();

            Assert.That(_log.ToString(), Does.Contain("[Emote] server rule=On emotes=3 tuning=memory/emotes.json"));
        }

        [Test]
        public void AnEmoteThatDoesNotLoopStopsWhenItsTimeEnds()
        {
            StartWithTwo();
            Request(1, _waveId);

            Step(0.9f);
            Assert.That(_emotesModule.IsPlaying(1, out _), Is.True);
            Step(0.2f);

            Assert.That(_emotesModule.IsPlaying(1, out _), Is.False);
            var states = States(2);
            Assert.That(states, Has.Count.EqualTo(2));
            Assert.That(states[1].Playing, Is.False);
            Assert.That(states[1].PlayerId, Is.EqualTo(1));
            Assert.That(states[1].StopReason, Is.EqualTo(EmoteStopReason.Finished));
            Assert.That(_log.ToString(), Does.Contain("[Emote] server stopped player=1 id=wave reason=Finished"));
        }

        [Test]
        public void ALoopingEmoteGoesOnPastItsCycle()
        {
            StartWithTwo();
            Request(1, _danceId);

            Step(5f);

            Assert.That(_emotesModule.IsPlaying(1, out var id), Is.True);
            Assert.That(id, Is.EqualTo(_danceId));
            Assert.That(States(2), Has.Count.EqualTo(1));
        }

        [Test]
        public void MovingStopsAnEmoteThatStopsOnMove()
        {
            StartWithTwo();
            Request(1, _danceId);

            Place(1, _alphaSpawn.X + 1f, 0f);

            Assert.That(_emotesModule.IsPlaying(1, out _), Is.False);
            Assert.That(States(2).Last().StopReason, Is.EqualTo(EmoteStopReason.Moved));
        }

        [Test]
        public void MovingWithinTheToleranceOrUpAndDownDoesNotStopIt()
        {
            StartWithTwo();
            Request(1, _danceId);

            Assert.That(_context.Bodies.TryGet(1, out var body), Is.True);
            var start = body.Position;
            _context.Bodies.Teleport(1, start + new Vector3(EmoteSpec.MoveToleranceMeters * 0.5f, 0.5f, 0f), 0f, _host.TickCount);
            Tick();

            Assert.That(_emotesModule.IsPlaying(1, out _), Is.True, _log.ToString());
        }

        [Test]
        public void MovingDoesNotStopAnEmoteThatKeepsGoing()
        {
            StartWithTwo();
            Request(1, _standId);

            Place(1, _alphaSpawn.X + 1f, 0f);

            Assert.That(_emotesModule.IsPlaying(1, out _), Is.True);
        }

        [Test]
        public void FiringStopsTheEmote()
        {
            StartWithTwo();
            Request(1, _danceId);

            FireEast(1);

            Assert.That(_emotesModule.IsPlaying(1, out _), Is.False, _log.ToString());
            Assert.That(States(2).Last().StopReason, Is.EqualTo(EmoteStopReason.Fired));
        }

        [Test]
        public void AStopRequestCancelsTheEmote()
        {
            StartWithTwo();
            Request(1, _danceId);

            _host.OnMessage(new ConnectionId(1), new EmoteRequestMessage { Action = EmoteAction.Stop });

            Assert.That(_emotesModule.IsPlaying(1, out _), Is.False);
            Assert.That(States(2).Last().StopReason, Is.EqualTo(EmoteStopReason.Cancelled));
        }

        [Test]
        public void AStopRequestWithNothingPlayingSendsNothing()
        {
            StartWithTwo();

            _host.OnMessage(new ConnectionId(1), new EmoteRequestMessage { Action = EmoteAction.Stop });

            Assert.That(States(2), Is.Empty);
        }

        [Test]
        public void ANewEmoteReplacesThePlayingOneWithoutAStopMessage()
        {
            StartWithTwo();
            Request(1, _danceId);
            Step(0.3f);

            Request(1, _waveId);

            var states = States(2);
            Assert.That(states.Select(s => s.Playing), Is.EqualTo(new[] { true, true }));
            Assert.That(states[1].EmoteId, Is.EqualTo(_waveId));
            Assert.That(_log.ToString(), Does.Contain("[Emote] server stopped player=1 id=dance reason=Replaced"));
        }

        [Test]
        public void AnEmoteIsRefusedWhenTheRulesTurnItOff()
        {
            Start(EmoteRules().WithActions(EmoteRules().Actions.WithEmote(Emote.Off)));
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_emotesModule.Play(1, _waveId), Is.EqualTo(EmoteRefusal.RuleOff));

            Assert.That(States(2), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Emote] server rule=Off"));
            Assert.That(_log.ToString(), Does.Contain("[Emote] server refused player=1 id=wave reason=RuleOff"));
        }

        [Test]
        public void AnIdNotInTheListIsRefused()
        {
            StartWithTwo();

            Assert.That(_emotesModule.Play(1, "backflip"), Is.EqualTo(EmoteRefusal.UnknownId));

            Assert.That(States(2), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Emote] server refused player=1 id=backflip reason=UnknownId"));
        }

        [Test]
        public void ADeadPlayerIsRefused()
        {
            StartWithTwo();
            Kill(1);

            Assert.That(_emotesModule.Play(1, _waveId), Is.EqualTo(EmoteRefusal.Dead));
            Assert.That(States(2).Where(s => s.Playing), Is.Empty);
        }

        [Test]
        public void DyingStopsTheEmote()
        {
            StartWithTwo();
            Request(1, _danceId);

            Kill(1);
            Tick();

            Assert.That(_emotesModule.IsPlaying(1, out _), Is.False);
            Assert.That(States(2).Last().StopReason, Is.EqualTo(EmoteStopReason.Died));
        }

        [Test]
        public void RequestsTooCloseTogetherAreRefused()
        {
            StartWithTwo();

            Assert.That(_emotesModule.Play(1, _danceId), Is.EqualTo(EmoteRefusal.None));
            Assert.That(_emotesModule.Play(1, _waveId), Is.EqualTo(EmoteRefusal.TooSoon));
            Assert.That(States(2), Has.Count.EqualTo(1));

            Step(ServerEmotes.MinRequestIntervalSeconds + _tickSeconds);

            Assert.That(_emotesModule.Play(1, _waveId), Is.EqualTo(EmoteRefusal.None));
            Assert.That(States(2), Has.Count.EqualTo(2));
        }

        [Test]
        public void TheIntervalIsCountedPerPlayer()
        {
            StartWithTwo();

            Assert.That(_emotesModule.Play(1, _danceId), Is.EqualTo(EmoteRefusal.None));
            Assert.That(_emotesModule.Play(2, _danceId), Is.EqualTo(EmoteRefusal.None));
        }

        [Test]
        public void APlayerWhoJoinsLaterSeesTheEmotesStillPlaying()
        {
            StartWithTwo();
            Request(1, _danceId);
            Request(2, _waveId);
            Step(1.2f);

            Join(3);

            var states = States(3);
            Assert.That(states, Has.Count.EqualTo(1), "the wave of 2 has ended");
            Assert.That(states[0].PlayerId, Is.EqualTo(1));
            Assert.That(states[0].Playing, Is.True);
            Assert.That(states[0].EmoteId, Is.EqualTo(_danceId));
        }

        [Test]
        public void APlayerWhoLeavesIsForgottenWithoutAStopMessage()
        {
            StartWithTwo();
            Request(1, _danceId);

            Assert.That(_match.TryGetPlayer(1, out var player), Is.True);
            _match.Leave(player.Connection);
            _host.OnDisconnected(player.Connection);
            Tick();

            Assert.That(_emotesModule.IsPlaying(1, out _), Is.False);
            Assert.That(States(2), Has.Count.EqualTo(1));
        }

        [Test]
        public void AMessageFromAConnectionNotInTheMatchIsIgnored()
        {
            StartWithTwo();

            _host.OnMessage(new ConnectionId(9), new EmoteRequestMessage { Action = EmoteAction.Play, EmoteId = _waveId });

            Assert.That(States(2), Is.Empty);
            Assert.That(_emotesModule.IsPlaying(9, out _), Is.False);
        }

        [Test]
        public void TheModuleNeedsItsParts()
        {
            var rules = new MatchRulesModule();
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(TestRules.Data()));
            var signals = new ServerCheatSignals();

            Assert.Throws<ArgumentNullException>(() => new ServerEmotes(null, movement, signals, _emotes));
            Assert.Throws<ArgumentNullException>(() => new ServerEmotes(rules, null, signals, _emotes));
            Assert.Throws<ArgumentNullException>(() => new ServerEmotes(rules, movement, null, _emotes));
            Assert.Throws<ArgumentNullException>(() => new ServerEmotes(rules, movement, signals, null));
        }

        [Test]
        public void TwoEmotesWithTheSameIdAreRefused()
        {
            var rules = new MatchRulesModule();
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(TestRules.Data()));

            Assert.Throws<ArgumentException>(() => new ServerEmotes(
                rules, movement, new ServerCheatSignals(), new[] { new EmoteSpec(_waveId), new EmoteSpec(_waveId) }));
        }

        [Test]
        public void TheEmotesAreReadFromTheTuningFolder()
        {
            using var root = new TempDirectory();
            var tuning = Path.Combine(root.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(
                Path.Combine(tuning, "emotes.json"),
                "{ \"emotes\": [ { \"id\": \"wave\", \"durationSeconds\": 2 }, { \"id\": \"dance\", \"loops\": true } ] }");

            Assert.That(ServerEmotes.TryLoadEmotes(new[] { root.Path }, out var emotes, out var path, out var error), Is.True, error);

            Assert.That(path, Is.EqualTo(Path.GetFullPath(Path.Combine(tuning, "emotes.json"))));
            Assert.That(emotes.Select(e => e.Id), Is.EqualTo(new[] { "wave", "dance" }));
            Assert.That(emotes[0].DurationSeconds, Is.EqualTo(2f));
            Assert.That(emotes[1].Loops, Is.True);
        }

        [Test]
        public void WithoutAFileTheEmotesAreEmpty()
        {
            using var root = new TempDirectory();

            Assert.That(ServerEmotes.TryLoadEmotes(new[] { root.Path }, out var emotes, out var path, out var error), Is.True);

            Assert.That(emotes, Is.Empty);
            Assert.That(path, Is.Null);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void ABrokenFileStopsTheLoadWithTheReason()
        {
            using var root = new TempDirectory();
            var tuning = Path.Combine(root.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, "emotes.json"), "{ \"emotes\": [ { \"id\": \"wave\", \"durationSeconds\": 0 } ] }");

            Assert.That(ServerEmotes.TryLoadEmotes(new[] { root.Path }, out var emotes, out _, out var error), Is.False);

            Assert.That(emotes, Is.Empty);
            Assert.That(error, Does.StartWith("cannot read emote tuning: "));
        }

        private static GameRules EmoteRules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithEmote(Emote.On);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private void StartWithTwo()
        {
            Start(EmoteRules());
            Join(1);
            Join(2);
            Step(0.2f);
        }

        private void Start(GameRules rules)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules), PingSpec.Default, "memory", null);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                map,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            var signals = new ServerCheatSignals();
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(data), signals: signals);
            _rules = new MatchRulesModule();
            _combat = new ServerCombat(_rules, movement, null, signals);
            _emotesModule = new ServerEmotes(_rules, movement, signals, _emotes, "memory/emotes.json");
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Register(_emotesModule);
            _host.Start(_context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Request(int id, string emoteId)
        {
            _host.OnMessage(new ConnectionId((ulong)id), new EmoteRequestMessage { Action = EmoteAction.Play, EmoteId = emoteId });
            Tick();
        }

        private void Place(int id, float x, float z)
        {
            _context.Bodies.Teleport(id, new Vector3(x, _alphaSpawn.Y, z), 0f, _host.TickCount);
            Tick();
        }

        private void Kill(int id)
        {
            _combat.DealDamage(null, id, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Assert.That(_rules.Session.IsAlive(id), Is.False, _log.ToString());
        }

        private void FireEast(int id)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            _host.OnMessage(new ConnectionId((ulong)id), new FireRequestMessage
            {
                WeaponId = TestRules.RifleId,
                Origin = body.EyePosition,
                Direction = Vector3.UnitX,
                ClientTick = (int)_host.TickCount,
                InterpolationAlpha = 0f,
                ShotIndex = 0,
                ShotGeneration = 0,
            });
            Tick();
        }

        private List<EmoteStateMessage> States(int connection)
        {
            return _transport.Read<EmoteStateMessage>(connection, m => new EmoteStateMessage
            {
                PlayerId = m.PlayerId,
                Playing = m.Playing,
                EmoteId = m.EmoteId,
                StopReason = m.StopReason,
            });
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Tick();
            }
        }

        private void Tick()
        {
            _match.Tick(_tickSeconds);
            _host.Tick(_tickSeconds);
        }
    }
}

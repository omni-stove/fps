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
    // ServerModules が組むモジュール一式で、箱を使う目的 (Extraction) に試合の途中で替える流れを確かめる。
    // 持ち物庫の置き場所の有無と、箱を使わない目的の試合にマップの箱を置かないことを見る
    public sealed class ServerModulesBoxModesTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);

        private TempDirectory _root;
        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private List<IServerModule> _modules;
        private ServerMatch _match;
        private ServerHost _host;
        private MatchRulesModule _rules;

        [SetUp]
        public void SetUp()
        {
            _root = new TempDirectory();
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _modules = new List<IServerModule>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            foreach (var module in _modules)
            {
                (module as IDisposable)?.Dispose();
            }

            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
            _root.Dispose();
        }

        [Test]
        public void ChangingToExtractionWithoutAStashDirectoryIsRefusedWithTheReasonOfTheLaunch()
        {
            Start();
            Join(1);
            Join(2);

            Assert.That(_match.RequestNextRules(Extraction(), out var error), Is.False);

            Assert.That(error, Is.EqualTo("cannot use the rules with objective Extraction for the next match: " + ServerLaunch.NoStashDirectoryProblem));
            Assert.That(_match.NextRules, Is.Null);

            _rules.Session.Finish(Team.Alpha);
            Step(1.1f);

            Assert.That(_rules.Objective.Objective, Is.EqualTo(MatchObjective.None), "the match goes on with the rules it had");
        }

        [Test]
        public void ChangingToExtractionWithAStashDirectoryPlacesTheMapBoxesOnlyFromThen()
        {
            var stashes = Path.Combine(_root.Path, "stashes");
            Start(ServerLaunch.StashDirectoryFlag, stashes);
            Join(1);
            Join(2);
            Step(0.1f);

            Assert.That(_log.ToString(), Does.Contain("[Loot] placed boxes=0 map=NetTest (objective None opens no boxes)"));
            Assert.That(LatestBoxCount(1), Is.Zero, "a team deathmatch shows no box");

            Assert.That(_match.RequestNextRules(Extraction(), out var error), Is.True, error);
            _rules.Session.Finish(Team.Alpha);
            Step(1.1f);

            Assert.That(_rules.Objective, Is.TypeOf<ExtractionRules>(), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Match] rules changed objective from=None to=Extraction"));
            Assert.That(_log.ToString(), Does.Contain("[Loot] placed boxes=1 map=NetTest"));
            _transport.Sent.Clear();
            Step(_tickSeconds);
            Assert.That(LatestBoxCount(1), Is.EqualTo(1), "the box of the map reaches the snapshot");
            Assert.That(Directory.Exists(stashes), Is.True);
        }

        private static GameRules Plain()
        {
            return GameRules.Default.WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f)
                .WithActions(ActionRules.Default.WithDowned(Downed.Off));
        }

        private static GameRules Extraction()
        {
            return Plain().WithObjective(MatchObjective.Extraction);
        }

        // 床 (TestMaps の箱) の上面は y=0.5。箱と脱出地点は出現地点から離して置く
        private static NetMapPoints Points()
        {
            return new NetMapPoints(
                new[] { new SpawnPoint(new Vector3(-5f, 1.6f, 0f), Team.Alpha), new SpawnPoint(new Vector3(5f, 1.6f, 0f), Team.Bravo) },
                Array.Empty<ControlPointArea>(),
                lootContainers: new[] { new NetMapLootContainer("crate", string.Empty, new Vector3(0f, 0.5f, 10f), 1.6f) },
                extractionPoints: new[] { new NetMapExtractionPoint("exit", new Vector3(0f, 0.5f, -10f), 3f) });
        }

        private void Start(params string[] args)
        {
            Assert.That(ServerLaunch.TryParse(args, _ => null, out var launch, out var parseError), Is.True, parseError);
            var settings = new NetMatchSettings(scoreLimit: 30, respawnSeconds: 0f, restartDelaySeconds: 1f);
            var data = TestRules.Data(Plain(), settings);
            var runtime = new ServerRuntime(
                TextWriter.Null, TextWriter.Null, _ => null, new ImmediateStopSignal(), new[] { _root.Path }, new[] { _root.Path }, () => _transport);
            Assert.That(ServerModules.Create(launch, runtime, data, _modules, out var problem), Is.True, problem);
            _rules = _modules.OfType<MatchRulesModule>().Single();

            _match = new ServerMatch(settings.RestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", Points()),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            _host = new ServerHost();
            foreach (var module in _modules)
            {
                _host.Register(module);
            }

            _host.Start(context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _host.Tick(_tickSeconds);
            }
        }

        private int LatestBoxCount(int id)
        {
            var count = -1;
            var snapshots = _transport.Read<SnapshotMessage>(id, snapshot =>
            {
                count = 0;
                for (var i = 0; i < snapshot.ObjectiveItems.Count; i++)
                {
                    if (snapshot.ObjectiveItems[i].Kind == ObjectiveItemKind.LootBox)
                    {
                        count++;
                    }
                }

                return snapshot;
            });
            Assert.That(snapshots, Is.Not.Empty);
            return count;
        }
    }
}

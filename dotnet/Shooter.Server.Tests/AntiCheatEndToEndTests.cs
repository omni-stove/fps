using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;
using ReportReason = Shooter.Protocol.ReportReason;

namespace Shooter.Server.Tests
{
    // ServerProgram と同じ ServerModules.Create で -bots 入りのサーバーを組み、server-data の anticheat.json を読ませて、
    // 隠す・切る・証拠を書くの 3 つが UDP のクライアントから見てつながっていることを確かめる。
    // マップは床を真ん中の高い壁で 2 つに分け、Alpha と Bravo の出現を壁の両側に置くので、敵はどちらの側の目からも見えない
    [NonParallelizable]
    public sealed class AntiCheatEndToEndTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _shotsPerTick = 8;
        private const string _version = "0.2.0";
        private static readonly DateTime _time = new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
        private static readonly Regex _evidenceSaved = new Regex(@"\[Evidence\] saved report=(\S+) target=(\d+) to (.+)$", RegexOptions.Multiline);

        private TempDirectory _directory;
        private StringWriter _log;
        private StringWriter _stdout;
        private StringWriter _startup;
        private UdpMatchTransport _transport;
        private TestClient _first;
        private TestClient _second;
        private ushort _port;
        private float _now;
        private int _inputTick;
        private MatchServer _server;
        private MatchRulesModule _rules;
        private string _antiCheatPath;

        [SetUp]
        public void SetUp()
        {
            _directory = new TempDirectory();
            _log = new StringWriter();
            _stdout = new StringWriter();
            _startup = new StringWriter();
            _transport = new UdpMatchTransport();
            _first = new TestClient();
            _second = new TestClient();
            _port = TestClient.FreePort();
            _now = 0f;
            _inputTick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _server?.Shutdown();
            _first.Dispose();
            _second.Dispose();
            _transport.Dispose();
            _log.Dispose();
            _stdout.Dispose();
            _startup.Dispose();
            _directory.Dispose();
        }

        [Test]
        public void TheStartLogNamesTheAntiCheatFileAndEveryCheckIsOn()
        {
            Start();

            Assert.That(_startup.ToString(), Does.Contain("[Server] combat tuning ").And.Contain(" anticheat=" + _antiCheatPath));
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[AntiCheat] violations on window=30s shotRejectionLimit=60 overBudgetLimit=120"));
            Assert.That(log, Does.Contain("[CheatStats] collecting window=120s"));
        }

        [Test]
        public void AnEnemyBotBehindTheWallIsSentConcealedWhileTheBotsPlayOn()
        {
            Start();
            var humanId = Join(_first, "watcher");
            Pump(() => Row(_first, humanId).HasValue && EnemyBot(humanId) != null && HasSpawned(EnemyBot(humanId).PlayerId));
            var botId = EnemyBot(humanId).PlayerId;

            // 出現した後に組んだスナップショットを見るため、出現を確かめた tick から少し進める
            var spawnedBy = _first.LatestSnapshot.Tick;
            Pump(() => _first.LatestSnapshot.Tick > spawnedBy + TickClock.DefaultTickRate);

            var row = Row(_first, botId);
            Assert.That(row.HasValue, Is.True, "the roster keeps the hidden bot");
            Assert.That(row.Value.Concealed, Is.True, "the wall hides the enemy bot from every eye of the watcher's side");
            Assert.That(row.Value.Position, Is.EqualTo(Vector3.Zero));
            Assert.That(row.Value.WeaponId, Is.Empty);
            Assert.That(row.Value.Health, Is.GreaterThan(0), "health stays in the hidden row");
            Assert.That(_server.Bodies.TryGet(botId, out var body), Is.True);
            Assert.That(body.Position.Z, Is.GreaterThan(1f), "the bot is on the far side of the wall");

            var own = Row(_first, humanId).Value;
            Assert.That(own.Concealed, Is.False, "the watcher's own row is never hidden");
            Assert.That(own.Position, Is.Not.EqualTo(Vector3.Zero));
            Assert.That(_log.ToString(), Does.Contain("[Bot] pos player=" + Text(botId)), "the bot keeps moving on the server");
        }

        [Test]
        public void AConnectionWhoseShotsAreRejectedPastTheLimitIsKicked()
        {
            Start();
            var humanId = Join(_first, "cheater");
            Pump(() => _rules.CanFire(humanId) && Row(_first, humanId).HasValue);

            var shotIndex = 0;
            Pump(() =>
            {
                // 撃てない間 (倒れている間など) の射撃は理由を出さずに捨てられ、数に入らないので、撃てるときにだけ送る
                if (_rules.CanFire(humanId) && _first.LatestSnapshot != null)
                {
                    var eye = Row(_first, humanId).Value.Position + new Vector3(0f, ServerMotorSettings.DefaultEyeY, 0f);
                    for (var i = 0; i < _shotsPerTick; i++)
                    {
                        _first.Send(new FireRequestMessage
                        {
                            WeaponId = "not-a-weapon",
                            Origin = eye,
                            Direction = Vector3.UnitZ,
                            ClientTick = _first.LatestSnapshot.Tick,
                            InterpolationAlpha = 0f,
                            ShotIndex = shotIndex++,
                            ShotGeneration = 0,
                        });
                    }
                }

                return _first.ServerDisconnectingReason != null || _first.Disconnected;
            });

            var rejected = DisconnectNotice.RejectedReason(AntiCheatSettings.RejectedDetail);
            Assert.That(_first.ServerDisconnectingReason ?? _first.DisconnectReason, Is.EqualTo(rejected));
            var log = _log.ToString();
            Assert.That(log, Does.Contain(
                "[AntiCheat] violations kicking id=" + Text(humanId) + ": reason=ShotRejected count=61 limit=60 window=30s"));
            Assert.That(log, Does.Contain("[Net] dropped client "));
            Assert.That(_server.Match.TryGetPlayer(humanId, out _), Is.False, "the kicked player has left the match");
            Assert.That(_server.Match.Players.Any(player => player.IsBot), Is.True, "the bots stay in the match");
        }

        [Test]
        public void AReportWritesTheEvidenceOfTheReportedPlayerNextToTheReportFile()
        {
            var reportFile = Path.Combine(_directory.Path, "records", ServerReports.DefaultFileName);
            Start(ServerLaunch.ReportFileFlag, reportFile);
            var reporterId = Join(_first, "reporter");
            var targetId = Join(_second, "target");
            Pump(() => HasSpawned(targetId) && Row(_second, targetId).HasValue);

            // 通報された人の直前の向きと位置 (look) が証拠に残るよう、相手に入力を送らせてから通報する
            var looked = 0;
            Pump(() =>
            {
                var input = new InputMessage();
                input.Add(new InputFrame(++_inputTick, Vector2.Zero, 0.5f, InputButtons.None));
                _second.Send(input, TransportChannel.Unreliable);
                return ++looked >= TickClock.DefaultTickRate;
            });
            _first.Send(new ReportRequestMessage { TargetPlayerId = targetId, Reason = ReportReason.Cheating, Comment = "sees through walls" });
            Pump(() => _evidenceSaved.IsMatch(_log.ToString()));

            var saved = _evidenceSaved.Match(_log.ToString());
            var reportId = saved.Groups[1].Value;
            var evidencePath = saved.Groups[3].Value.TrimEnd('\r');
            Assert.That(saved.Groups[2].Value, Is.EqualTo(Text(targetId)));
            Assert.That(Path.GetFileName(evidencePath), Is.EqualTo(reportId + ServerReportEvidenceLog.FileSuffix));
            Assert.That(Path.GetDirectoryName(evidencePath), Is.EqualTo(Path.GetDirectoryName(Path.GetFullPath(reportFile))));
            Assert.That(File.Exists(evidencePath), Is.True, evidencePath);
            Assert.That(File.ReadAllText(evidencePath), Does.Contain("\"type\":\"" + ServerReportEvidenceLog.LookType + "\""));

            _server.Shutdown();
            _server = null;
            var reports = File.ReadAllLines(reportFile);
            Assert.That(reports, Has.Length.EqualTo(1));
            Assert.That(reports[0], Does.Contain(reportId));
            Assert.That(_log.ToString(), Does.Contain(ServerReportLog.Saved(reporterId, targetId, ReportReason.Cheating)));
        }

        private void Start(params string[] extraArgs)
        {
            var root = CopyAntiCheatTuning();
            var environment = new Dictionary<string, string>
            {
                [ServerLaunch.AddressVariable] = "127.0.0.1",
                [ServerLaunch.PortVariable] = _port.ToString(CultureInfo.InvariantCulture),
            };
            var args = new List<string> { "-buildVersion", _version, "-log-format", "json", "-bots", "4" };
            args.AddRange(extraArgs);
            Assert.That(
                ServerLaunch.TryParse(args, name => environment.TryGetValue(name, out var value) ? value : null, out var launch, out var error),
                Is.True,
                error);

            var rules = GameRules.Default.WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f)
                .WithActions(ActionRules.Default.WithDowned(Downed.Off));
            var data = TestRules.Data(rules);
            var points = new NetMapPoints(
                new[]
                {
                    new SpawnPoint(new Vector3(0f, 1.1f, -20f), Team.Alpha),
                    new SpawnPoint(new Vector3(0f, 1.1f, 20f), Team.Bravo),
                },
                Array.Empty<ControlPointArea>());

            // 床より広く、余裕の距離 (横と上に 1.5) をずらしても越えて見えない高さの壁
            var wall = WorldCollider.Box(
                2, 0, WorldCollider.StaticGroup, new Vector3(0f, 15f, 0f), new Vector3(40f, 15f, 1f), Quaternion.Identity);
            var map = BotTestMaps.Flat(points, extra: new[] { wall });
            var runtime = new ServerRuntime(
                _startup, TextWriter.Null, _ => null, new ImmediateStopSignal(), new[] { root }, new[] { root }, () => _transport);
            var modules = new List<IServerModule>();
            Assert.That(ServerModules.Create(launch, runtime, data, modules, out var problem), Is.True, problem);
            _rules = modules.OfType<MatchRulesModule>().Single();
            var host = new ServerHost();
            foreach (var module in modules)
            {
                host.Register(module);
            }

            _server = new MatchServer(
                launch, _transport, host, map, _log, new ServerEventWriter(true, _stdout, _log), () => _time, data.Settings.RestartDelaySeconds, data);
            _server.Start();
        }

        // 近接・ストリーク・設置物は既定のままにして、試合の進み方を他の通信のテストとそろえるため、写すのは anticheat.json だけにする
        private string CopyAntiCheatTuning()
        {
            var source = Path.Combine(
                RepositoryRoot(), ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName, ServerCombatTuning.AntiCheatFileName);
            Assert.That(File.Exists(source), Is.True, source);
            var root = Path.Combine(_directory.Path, "root");
            var folder = Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(folder);
            _antiCheatPath = Path.GetFullPath(Path.Combine(folder, ServerCombatTuning.AntiCheatFileName));
            File.Copy(source, _antiCheatPath);
            return root;
        }

        private int Join(TestClient client, string key)
        {
            client.Connect(_port);
            Pump(() => client.Connected);
            client.SendRequest(_version, key);
            Pump(() => client.Accepted != null);
            return client.Accepted.PlayerId;
        }

        private ServerPlayer EnemyBot(int humanId)
        {
            if (!_server.Match.TryGetPlayer(humanId, out var human))
            {
                return null;
            }

            return _server.Match.Players.FirstOrDefault(player => player.IsBot && player.Team != human.Team);
        }

        private bool HasSpawned(int playerId)
        {
            return _server.Bodies.TryGet(playerId, out var body) && body.HasSpawned;
        }

        private static PlayerSnapshot? Row(TestClient client, int playerId)
        {
            var snapshot = client.LatestSnapshot;
            if (snapshot == null)
            {
                return null;
            }

            for (var i = 0; i < snapshot.Players.Count; i++)
            {
                if (snapshot.Players[i].PlayerId == playerId)
                {
                    return snapshot.Players[i];
                }
            }

            return null;
        }

        // 1 tick ごとに眠ると数十秒ぶんの試合に時間がかかるので、通信を回すのは数 tick おきにする
        private void Pump(Func<bool> until)
        {
            var watch = Stopwatch.StartNew();
            var ticks = 0;
            do
            {
                _now += _tickSeconds;
                _server.Tick(_now, _tickSeconds);
                _first.Poll();
                _second.Poll();
                if (until())
                {
                    return;
                }

                if (++ticks % 8 == 0)
                {
                    Thread.Sleep(1);
                }
            }
            while (watch.Elapsed < _timeout);

            Assert.Fail("timed out waiting for the server and the clients\n" + _log);
        }

        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName)))
                {
                    return directory.FullName;
                }
            }

            Assert.Fail(ServerMapData.DataDirectoryName + " is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // server-data のルール (BattleRoyale・Extraction) と書き出した NetBattleRoyale のマップを、make server-dotnet と同じ口
    // (ServerRulesData.TryLoad・ServerMapData.TryLoad・ServerModules.Create) で読み、Bot だけで決着・脱出まで進むことをログで確かめる。
    // Bot は人が 1 人もいないと置かれない (BotRoster.DesiredBots) ので、動かない人を 1 人だけ入れて、あとは Bot に任せる
    public sealed class BoxModesEndToEndTests
    {
        private const string _mapName = "NetBattleRoyale";
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _watcher = 1;
        private const string _bots = "7";

        // 時間切れでも決着 (順位を付けて終わる) するので、エリアが閉じ切ってからこれだけ待っても決まらなければ失敗にする
        private const float _battleRoyaleGraceSeconds = 20f;

        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Regex _extracted = new Regex(
            @"^\[Extraction\] ended outcome=Extracted\b.* player=(\d+) account=(\S+)$", RegexOptions.CultureInvariant);

        private TempDirectory _directory;
        private LineLog _log;
        private DiscardingTransport _transport;
        private List<IServerModule> _modules;
        private ServerMatch _match;
        private ServerHost _host;
        private ServerMapData _map;
        private ServerRulesData _data;
        private int _scanned;

        [SetUp]
        public void SetUp()
        {
            _directory = new TempDirectory();
            _log = new LineLog();
            _transport = new DiscardingTransport();
            _modules = new List<IServerModule>();
            _scanned = 0;
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
            _directory.Dispose();
        }

        // 例の battle-royale.json は時間の制限が無く、拾う武器が表に無い checkout では最後の分隊が決まらないことがあるので、
        // 時間の制限だけをエリアの段階の合計より少し後に置き換え、ほかの値は例のまま使う
        [Test]
        public void BotsPlayABattleRoyaleOnTheNetMapUntilItIsDecided()
        {
            var root = CopyServerData("BattleRoyale");
            var zoneSeconds = LimitBattleRoyaleTime(root);

            Start(root, "BattleRoyale", "-bots", _bots);
            JoinWatcher();

            var ended = RunUntil(line => line.StartsWith("[BattleRoyale] state=Ended", StringComparison.Ordinal), zoneSeconds + _battleRoyaleGraceSeconds);

            Assert.That(ended, Is.Not.Null, "the battle royale was not decided\n" + Tail());
            Assert.That(ended, Does.Match(@"winner=(squad\d+|" + Regex.Escape(MatchLog.Draw) + ") squads=\\d+"));
            Assert.That(_log.Lines, Has.Some.StartsWith("[BattleRoyale] begin squads="));
            Assert.That(_log.Lines, Has.Some.StartsWith("[BattleRoyale] squad player=" + Text(BotRoster.FirstBotPlayerId) + " squad="), "a bot plays in a squad");
            Assert.That(
                _log.Lines,
                Has.Some.EqualTo(ServerLootLog.Placed(_mapName, _map.Points.LootContainers.Count)),
                "the boxes of the map are placed for the battle royale");
            Assert.That(_log.Lines, Has.Some.StartsWith("[Bot] added player=" + Text(BotRoster.FirstBotPlayerId)));
        }

        [Test]
        public void BotsRaidTheNetMapUntilOneExtractsAndTheirStashIsSaved()
        {
            var root = CopyServerData("Extraction");
            var stashes = Path.Combine(_directory.Path, "stashes");

            Start(root, "Extraction", "-bots", _bots, ServerLaunch.StashDirectoryFlag, stashes);
            JoinWatcher();

            var extracted = RunUntil(line => _extracted.IsMatch(line), _data.Extraction.RaidSeconds + 1f);

            Assert.That(extracted, Is.Not.Null, "no bot extracted before the raid time ran out\n" + Tail());
            var match = _extracted.Match(extracted);
            var player = int.Parse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture);
            var account = match.Groups[2].Value;
            Assert.That(BotRoster.IsBotId(player), Is.True, "only the bots move; the watcher stands still");
            Assert.That(account, Is.EqualTo(ExtractionRules.BotAccountPrefix + Text(player)));
            Assert.That(File.Exists(Path.Combine(stashes, account + ServerStashStore.Extension)), Is.True, "the bot's stash is saved where the launch says");
            Assert.That(_log.Lines, Has.Some.StartsWith("[Stash] saved player=" + Text(player) + " account=" + account));
            Assert.That(_log.Lines, Has.Some.EqualTo(ServerLootLog.Placed(_mapName, _map.Points.LootContainers.Count)));
        }

        // 例のルール・調整値と書き出したマップを一時フォルダに写し、テストが書き換えても checkout の server-data を汚さない
        private string CopyServerData(string rulesName)
        {
            var source = Path.Combine(RepositoryRoot(), ServerMapData.DataDirectoryName);
            var maps = Path.Combine(source, ServerMapData.MapsDirectoryName);
            foreach (var required in new[] { _mapName + NetMapPoints.Extension, _mapName + ServerMapData.CollisionExtension })
            {
                if (!File.Exists(Path.Combine(maps, required)))
                {
                    Assert.Ignore(Path.Combine(maps, required) + " has not been exported; run make export-server-rules in Unity and commit server-data");
                }
            }

            var rulesFile = Path.Combine(source, ServerRulesData.RulesDirectoryName, rulesName + NetMatchFile.Extension);
            Assert.That(File.Exists(rulesFile), Is.True, rulesFile);

            var root = Path.Combine(_directory.Path, "root");
            var target = Path.Combine(root, ServerMapData.DataDirectoryName);
            Copy(rulesFile, Path.Combine(target, ServerRulesData.RulesDirectoryName));
            foreach (var file in Directory.GetFiles(Path.Combine(source, ServerRulesData.TuningDirectoryName)))
            {
                Copy(file, Path.Combine(target, ServerRulesData.TuningDirectoryName));
            }

            foreach (var file in Directory.GetFiles(maps, _mapName + ".*"))
            {
                Copy(file, Path.Combine(target, ServerMapData.MapsDirectoryName));
            }

            return root;
        }

        // 返すのはエリアの段階の待ちと縮みの合計 (秒)
        private static float LimitBattleRoyaleTime(string root)
        {
            var path = Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName, ServerRulesData.BattleRoyaleTuningFileName);
            Assert.That(File.Exists(path), Is.True, path);
            var tuning = JsonNode.Parse(File.ReadAllText(path)).AsObject();
            var zoneSeconds = 0f;
            foreach (var phase in tuning["zone"]["phases"].AsArray())
            {
                zoneSeconds += phase["waitSeconds"].GetValue<float>() + phase["shrinkSeconds"].GetValue<float>();
            }

            tuning["timeLimitSeconds"] = zoneSeconds + (_battleRoyaleGraceSeconds / 2f);
            File.WriteAllText(path, tuning.ToJsonString());
            return zoneSeconds;
        }

        private void Start(string root, string rulesName, params string[] args)
        {
            var roots = new[] { root };
            Assert.That(ServerRulesData.TryLoad(rulesName, roots, out _data, out var error), Is.True, error);
            Assert.That(ServerMapData.TryLoad(_mapName, roots, out _map, out error), Is.True, error);
            Assert.That(_map.Points, Is.Not.Null);
            var launchArgs = new List<string> { ServerLaunch.RulesFlag, rulesName };
            launchArgs.AddRange(args);
            Assert.That(ServerLaunch.TryParse(launchArgs, _ => null, out var launch, out error), Is.True, error);
            var runtime = new ServerRuntime(TextWriter.Null, TextWriter.Null, _ => null, new ImmediateStopSignal(), roots, roots, () => _transport);
            Assert.That(ServerModules.Create(launch, runtime, _data, _modules, out error), Is.True, error);

            _match = new ServerMatch(_data.Settings.RestartDelaySeconds, _log, new ServerEventWriter(false, TextWriter.Null, _log), () => _time);
            var context = new ServerContext(
                launch.Options,
                "1.0.0",
                _log,
                _map,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                _data);
            _host = new ServerHost();
            foreach (var module in _modules)
            {
                _host.Register(module);
            }

            _host.Start(context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる。入力を送らないので、出た所で動かない
        private void JoinWatcher()
        {
            var connection = new ConnectionId((ulong)_watcher);
            _match.Join(connection, "watcher");
            _host.OnConnected(connection);
        }

        // 1 tick ずつ進め、その tick に増えた行だけを見る。見つけた行を返し、試合の時間で limitSeconds を過ぎたら null
        private string RunUntil(Func<string, bool> found, float limitSeconds)
        {
            for (var elapsed = 0f; elapsed < limitSeconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _host.Tick(_tickSeconds);
                var lines = _log.Lines;
                for (; _scanned < lines.Count; _scanned++)
                {
                    if (found(lines[_scanned]))
                    {
                        return lines[_scanned];
                    }
                }
            }

            return null;
        }

        private string Tail()
        {
            return string.Join("\n", _log.Lines.Where(line => !line.StartsWith("[Bot] pos", StringComparison.Ordinal)).TakeLast(60));
        }

        private static void Copy(string file, string directory)
        {
            Directory.CreateDirectory(directory);
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
        }

        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ServerMapData.DataDirectoryName, ServerRulesData.RulesDirectoryName)))
                {
                    return directory.FullName;
                }
            }

            Assert.Fail(ServerMapData.DataDirectoryName + " is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        // 数分ぶんの試合の行を StringWriter に溜めて毎 tick 読み直すと遅いので、行に分けて溜め、増えた分だけを見られるようにする
        private sealed class LineLog : TextWriter
        {
            private readonly StringBuilder _line = new StringBuilder();

            public List<string> Lines { get; } = new List<string>();

            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                if (value == '\n')
                {
                    Lines.Add(_line.ToString().TrimEnd('\r'));
                    _line.Clear();
                    return;
                }

                _line.Append(value);
            }
        }

        // 数分ぶんのスナップショットを残すと大きくなるので、送った物は捨てる
        private sealed class DiscardingTransport : IMatchTransport
        {
            public bool IsActive => true;

            public void Listen(string address, ushort port)
            {
            }

            public void Connect(string address, ushort port)
            {
            }

            public void Disconnect(ConnectionId connection, string reason)
            {
            }

            public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
            {
            }

            public int GetMaxPayloadSize(TransportChannel channel)
            {
                return channel == TransportChannel.Unreliable ? RecordingTransport.UnreliableLimit : ushort.MaxValue;
            }

            public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
            {
                milliseconds = 0;
                return false;
            }

            public bool TryReceive(out TransportEvent transportEvent)
            {
                transportEvent = default;
                return false;
            }

            public void Shutdown(string reason)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}

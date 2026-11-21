using System;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // オフラインが読むルールの JSON (GameRulesJson) を、Unity の書き出し (NetMatchFile.Write) と同じ形で server-data に置き、
    // 専用サーバーの読み口 (ServerRulesData.TryLoad) で読み直して、actions とそれで決まる結果がオフラインと同じになるかを確かめる
    public sealed class OfflineOnlineActionsTests
    {
        private const string _rulesName = "AllActions";

        private const string _offlineJson = "{\n"
            + "  \"actions\": {\n"
            + "    \"downed\": \"Revive\",\n"
            + "    \"melee\": \"On\",\n"
            + "    \"ping\": \"On\",\n"
            + "    \"prone\": \"On\",\n"
            + "    \"slide\": \"On\",\n"
            + "    \"mantle\": \"On\",\n"
            + "    \"zipline\": \"On\",\n"
            + "    \"stamina\": \"On\",\n"
            + "    \"stagger\": \"On\",\n"
            + "    \"suppression\": \"On\",\n"
            + "    \"emote\": \"On\",\n"
            + "    \"stealth\": \"On\"\n"
            + "  }\n"
            + "}\n";

        private static readonly string[] _stealthOnly = { "stealth" };
        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

        private TempDirectory _root;
        private GameRules _offline;
        private ServerRulesData _online;

        [SetUp]
        public void SetUp()
        {
            _root = new TempDirectory();
            _offline = GameRulesJson.Parse(_offlineJson, "offline rules");
            var directory = Path.Combine(_root.Path, ServerMapData.DataDirectoryName, ServerRulesData.RulesDirectoryName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, _rulesName + NetMatchFile.Extension),
                NetMatchFile.Write(TestRules.File(_offline, name: _rulesName)));
            Assert.That(ServerRulesData.TryLoad(_rulesName, new[] { _root.Path }, out _online, out var error), Is.True, error);
        }

        [TearDown]
        public void TearDown()
        {
            _root.Dispose();
        }

        [Test]
        public void TheServerReadsTheSameActionsAsOffline()
        {
            Assert.That(GameRulesJson.Write(_online.Rules), Is.EqualTo(GameRulesJson.Write(_offline)));
            Assert.That(_online.Rules.Actions.Slide, Is.EqualTo(Slide.On));
            Assert.That(_online.Rules.Actions.Stealth, Is.EqualTo(Stealth.On));
        }

        [Test]
        public void OnlyStealthIsLeftUnsupported()
        {
            Assert.That(ServerUnsupportedActions.EnabledIn(_offline), Is.EqualTo(_stealthOnly));
            Assert.That(ServerUnsupportedActions.EnabledIn(_online.Rules), Is.EqualTo(_stealthOnly));
        }

        // 滑る・よじ登る・ジップラインは共有の移動の計算がルールで決めるので、サーバーの移動に渡る値がオフラインと同じか
        [Test]
        public void TheServerMovementMovesByTheOfflineActions()
        {
            var actions = ServerModules.MotorSettingsFrom(_online).Rules.Actions;

            Assert.That(actions.Slide, Is.EqualTo(_offline.Actions.Slide));
            Assert.That(actions.Mantle, Is.EqualTo(_offline.Actions.Mantle));
            Assert.That(actions.Zipline, Is.EqualTo(_offline.Actions.Zipline));
            Assert.That(actions.Stamina, Is.EqualTo(_offline.Actions.Stamina));
        }

        // クライアントが輪や近接の枠を出すかは、スナップショットで届くルールで決める
        [Test]
        public void TheClientIsSentTheSameRulesAsOffline()
        {
            var sent = MatchRulesSync.From(_online.Rules);

            Assert.That(sent.SameAs(MatchRulesSync.From(_offline)), Is.True);
            Assert.That(sent.Emote, Is.EqualTo(Emote.On));
        }

        // よろけ・スタミナ・制圧は、オフラインの体が持つ PlayerStatusMeters と、サーバーの ServerPlayerStatus で同じ答えになるか
        [Test]
        public void TheSameHitsGiveTheSameStatusOfflineAndOnline()
        {
            var tuning = ServerPlayerStatusTuning.Default;
            var offline = new PlayerStatusMeters(_offline.Actions, tuning.Stamina, tuning.Stagger.Player, tuning.Suppression);
            var server = new ServerPlayerStatus(_online.Rules, tuning);

            var offlineVerdict = offline.Damaged(null, DamageKind.Melee, 30);
            server.Damaged(1, null, DamageKind.Melee, 30);
            Assert.That(server.TryGetMeters(1, out var online), Is.True);
            offline.AddNearMiss(0.5f);
            online.AddNearMiss(0.5f);

            Assert.That(offlineVerdict.HasValue, Is.True);
            Assert.That(offline.StaggerKind, Is.EqualTo(StaggerKind.Stumble));
            Assert.That(online.StaggerKind, Is.EqualTo(offline.StaggerKind));
            Assert.That(online.StaminaStatus.HasValue, Is.True, "stamina is on");
            Assert.That(online.StaminaStatus, Is.EqualTo(offline.StaminaStatus));
            Assert.That(online.SuppressionLevel, Is.GreaterThan(0f));
            Assert.That(online.SuppressionLevel, Is.EqualTo(offline.SuppressionLevel));
        }

        [Test]
        public void StartingTheRulesWarnsOnlyAboutStealth()
        {
            using var log = new StringWriter();
            using var json = new StringWriter();
            using var transport = new RecordingTransport();
            var match = new ServerMatch(_online.Settings.RestartDelaySeconds, log, new ServerEventWriter(true, json, log), () => _time);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                log,
                TestMaps.Data("NetTest", TestRules.Points(0)),
                match,
                new ServerMessenger(transport),
                new StubControl(),
                _online);
            var module = new MatchRulesModule();

            module.Start(context);
            module.Stop();

            Assert.That(log.ToString(), Does.Contain("[Rules] warning: actions stealth are on in rules " + _rulesName));
            Assert.That(log.ToString(), Does.Not.Contain("actions slide"));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class ServerTelemetryTests
    {
        private const float _tickSeconds = 1f / 60f;
        private const string _fileName = "server-telemetry.jsonl";
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);

        private TempDirectory _temp;
        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private MatchRulesModule _rules;
        private ServerTelemetry _module;
        private long _tick;

        private string FilePath => Path.Combine(_temp.Path, _fileName);

        [SetUp]
        public void SetUp()
        {
            _temp = new TempDirectory();
            _log = new StringWriter();
            _transport = new RecordingTransport();
            _tick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _module?.Dispose();
            _rules?.Stop();
            _transport.Dispose();
            _log.Dispose();
            _temp.Dispose();
        }

        [Test]
        public void WithConsentAnUnhandledExceptionLeavesOneLine()
        {
            Start(consent: true);

            Assert.That(_module.RecordUnhandled(new InvalidOperationException("tick failed"), terminating: true), Is.True);

            var lines = ReadLines();
            Assert.That(lines, Has.Length.EqualTo(1));
            var values = Parse(lines[0]);
            Assert.That(values["type"], Is.EqualTo("crash"));
            Assert.That(values["time"], Is.EqualTo("2026-11-18T09:00:00.000Z"));
            Assert.That(values["source"], Is.EqualTo("server"));
            Assert.That(values["version"], Is.EqualTo("1.0.0"));
            Assert.That(values["exception"], Is.EqualTo("System.InvalidOperationException"));
            Assert.That(values["message"], Is.EqualTo("tick failed"));
            Assert.That(_log.ToString(), Does.Contain("[Telemetry] recording to " + _fileName + " (sink=LocalFile)"));
        }

        [Test]
        public void WithoutConsentNothingIsLeft()
        {
            Start(consent: false);

            Assert.That(_module.RecordUnhandled(new InvalidOperationException("tick failed"), terminating: true), Is.False);
            JoinAndWin();
            _module.Stop();

            Assert.That(File.Exists(FilePath), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Telemetry] not recording: no consent"));
        }

        [Test]
        public void TheEndOfAMatchWritesASummary()
        {
            Start(consent: true);

            JoinAndWin();

            var lines = ReadLines();
            Assert.That(lines, Has.Length.EqualTo(1));
            var values = Parse(lines[0]);
            Assert.That(values["type"], Is.EqualTo("matchSummary"));
            Assert.That(values["matchId"], Is.EqualTo(_match.MatchId));
            Assert.That(values["mode"], Is.EqualTo("None"));
            Assert.That(values["map"], Is.EqualTo("NetTest"));
            Assert.That(values["winner"], Is.EqualTo("0"));
            Assert.That(values["score0"], Is.EqualTo("2"));
            Assert.That(values["score1"], Is.EqualTo("0"));
            Assert.That(values["humans"], Is.EqualTo("2"));
            Assert.That(values["bots"], Is.EqualTo("0"));
            Assert.That(values.ContainsKey("ownSide"), Is.False, "the server has no own side");
            var duration = double.Parse(values["durationSeconds"], CultureInfo.InvariantCulture);
            Assert.That(duration, Is.InRange(1.1, 1.2), "the two kills are 1.1 seconds of ticks apart");
        }

        [Test]
        public void StoppingDetachesFromTheMatch()
        {
            Start(consent: true);
            _module.Stop();

            JoinAndWin();

            Assert.That(File.Exists(FilePath), Is.False);
            Assert.That(_module.RecordUnhandled(new InvalidOperationException("late"), terminating: false), Is.False);
        }

        [Test]
        public void TheSpecIsReadFromTheTuningFolderOrDefaults()
        {
            Assert.That(ServerTelemetry.TryLoadSpec(_temp.Path, out var fallback, out var error), Is.True, error);
            Assert.That(fallback, Is.SameAs(TelemetrySpec.Default));

            var folder = Path.Combine(_temp.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "telemetry.json"), "{\"sink\": \"None\"}");
            Assert.That(ServerTelemetry.TryLoadSpec(_temp.Path, out var off, out error), Is.True, error);
            Assert.That(off.IsOff, Is.True);

            File.WriteAllText(Path.Combine(folder, "telemetry.json"), "{\"sink\": \"Http\"}");
            Assert.That(ServerTelemetry.TryLoadSpec(_temp.Path, out var bad, out error), Is.False);
            Assert.That(bad, Is.Null);
            Assert.That(error, Does.StartWith("cannot read telemetry tuning: "));
            Assert.That(error, Does.Contain("endpoint"));
        }

        [Test]
        public void AFileSinkNeedsAFolder()
        {
            Assert.Throws<ArgumentException>(() => new ServerTelemetry(TelemetrySpec.Default, " ", true, () => _time));
            Assert.DoesNotThrow(() => new ServerTelemetry(new TelemetrySpec(sink: TelemetrySink.None), null, true, () => _time).Dispose());
        }

        private static GameRules Plain()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private void Start(bool consent)
        {
            var settings = new NetMatchSettings(scoreLimit: 2, respawnSeconds: 1f);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, new ServerEventWriter(false, _log, _log), () => _time);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", TestRules.Points()),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                TestRules.Data(Plain(), settings));
            _rules = new MatchRulesModule();
            _rules.Start(context);
            _module = new ServerTelemetry(new TelemetrySpec(fileName: _fileName), _temp.Path, consent, () => _time, () => 0d);
            _module.Start(context);
        }

        private void JoinAndWin()
        {
            _match.Join(new ConnectionId(1), "device1");
            _match.Join(new ConnectionId(2), "device2");
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_rules.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Advance(1.1f);
            Assert.That(_rules.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
        }

        private void Advance(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _rules.Tick(++_tick, _tickSeconds);
                _module.Tick(_tick, _tickSeconds);
            }
        }

        private string[] ReadLines()
        {
            using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                var text = reader.ReadToEnd();
                return text.Length == 0 ? Array.Empty<string>() : text.TrimEnd('\n').Split('\n');
            }
        }

        private static IReadOnlyDictionary<string, string> Parse(string line)
        {
            Assert.That(FlatJsonObject.TryParse(line, out var values, out var error), Is.True, error);
            return values;
        }
    }
}

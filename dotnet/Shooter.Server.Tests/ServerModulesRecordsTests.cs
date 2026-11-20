using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.EosSdk;

namespace Shooter.Server.Tests
{
    // 通報・統計・テレメトリは起動の引数で頼んだときだけ、試合のモジュールとスナップショットの後に足す
    public sealed class ServerModulesRecordsTests
    {
        public const int MatchModules = 14;

        private TempDirectory _roots;
        private TempDirectory _library;
        private TempDirectory _records;
        private Dictionary<string, string> _environment;
        private List<IServerModule> _modules;
        private StringWriter _output;

        [SetUp]
        public void SetUp()
        {
            _roots = new TempDirectory();
            _library = new TempDirectory();
            _records = new TempDirectory();
            _environment = new Dictionary<string, string>();
            _modules = new List<IServerModule>();
            _output = new StringWriter();
        }

        [TearDown]
        public void TearDown()
        {
            TearDownModules();
            _roots.Dispose();
            _library.Dispose();
            _records.Dispose();
            _output.Dispose();
        }

        [Test]
        public void WithoutTheFlagsNothingIsKeptAndTheLogSaysHowToTurnThemOn()
        {
            Assert.That(Create(), Is.Null);

            Assert.That(_modules, Has.Count.EqualTo(MatchModules));
            Assert.That(_modules, Has.None.InstanceOf<ServerReports>());
            Assert.That(_modules, Has.None.InstanceOf<ServerStatsModule>());
            Assert.That(_modules, Has.None.InstanceOf<ServerTelemetry>());
            var log = _output.ToString();
            Assert.That(log, Does.Contain("[Report] reports are not kept (pass --report-file <path> to keep them)"));
            Assert.That(log, Does.Contain("[Stats] sending match stats to EOS is off: not asked (pass --stats to send them)"));
            Assert.That(log, Does.Contain("[Telemetry] not recording: no consent (pass --telemetry to agree)"));
        }

        [Test]
        public void AReportFileAddsTheReportsAfterTheSnapshots()
        {
            var path = Path.Combine(_records.Path, "reports", ServerReports.DefaultFileName);

            Assert.That(Create("--report-file", path), Is.Null);

            Assert.That(_modules, Has.Count.EqualTo(MatchModules + 1));
            Assert.That(_modules[MatchModules - 1], Is.InstanceOf<ServerSnapshots>());
            Assert.That(_modules[MatchModules], Is.InstanceOf<ServerReports>());
            Assert.That(((ServerReports)_modules[MatchModules]).Path, Is.EqualTo(Path.GetFullPath(path)));
            Assert.That(((ServerReports)_modules[MatchModules]).RepeatSeconds, Is.EqualTo(ServerReports.DefaultRepeatSeconds));
            Assert.That(_output.ToString(), Does.Not.Contain("reports are not kept"));
        }

        [Test]
        public void TheTelemetryKeepsItsFileInTheGivenFolder()
        {
            Assert.That(Create("--telemetry", "--telemetry-dir", _records.Path), Is.Null);

            Assert.That(_modules, Has.Count.EqualTo(MatchModules + 1));
            var telemetry = (ServerTelemetry)_modules[MatchModules];
            Assert.That(telemetry.Path, Is.EqualTo(Path.Combine(Path.GetFullPath(_records.Path), TelemetrySpec.DefaultFileName)));
            Assert.That(_output.ToString(), Does.Contain("[Telemetry] tuning default"));
        }

        [Test]
        public void TheTelemetryWithAFileSinkNeedsAFolder()
        {
            Assert.That(
                Create("--telemetry"),
                Is.EqualTo("[Telemetry] --telemetry keeps the telemetry in a file (sink LocalFile); pass --telemetry-dir <folder>, "
                    + "or set the sink of telemetry.json to Http or None"));
            Assert.That(_modules, Has.None.InstanceOf<ServerTelemetry>());
        }

        [Test]
        public void TheTelemetryReadsItsSinkFromServerData()
        {
            var path = WriteTuning(ServerTelemetry.TuningFileName, "{\"sink\": \"None\"}");

            Assert.That(Create("--telemetry"), Is.Null);

            var telemetry = (ServerTelemetry)_modules[MatchModules];
            Assert.That(telemetry.Path, Is.Null, "the None sink keeps no file");
            Assert.That(_output.ToString(), Does.Contain("[Telemetry] tuning " + path));
        }

        [Test]
        public void AnUnreadableTelemetryTuningStopsTheStartOnlyWhenAgreed()
        {
            WriteTuning(ServerTelemetry.TuningFileName, "{\"sink\": \"Http\"}");

            Assert.That(Create(), Is.Null, "without the consent the file is not read");
            TearDownModules();

            Assert.That(Create("--telemetry"), Does.StartWith("[Telemetry] cannot read telemetry tuning: "));
            Assert.That(_modules, Has.None.InstanceOf<ServerTelemetry>());
        }

        [Test]
        public void TheStatsNeedEosCredentials()
        {
            _environment[EosCredentials.ProductIdVariable] = "p";

            var error = Create("--stats");

            Assert.That(error, Does.StartWith("[Stats] --stats needs EOS credentials; missing EOS_SANDBOX_ID,"));
            Assert.That(error, Does.Contain(EosCredentials.LocalFileName));
            Assert.That(_modules, Has.None.InstanceOf<ServerStatsModule>());
        }

        [Test]
        public void WithoutTheSdkOrItsLibraryTheStatsCannotBeSent()
        {
            WriteLocalFile();

            var error = Create("--stats");

            Assert.That(error, Does.StartWith("[Stats] --stats cannot use EOS: "));
            Assert.That(error, EosSdkBuild.Included ? Does.Contain("native library") : Does.Contain("not in this build"));
            Assert.That(_modules, Has.None.InstanceOf<ServerStatsModule>());
        }

        [Test]
        public void WithTheSdkAndCredentialsTheStatsModuleIsAdded()
        {
            if (!EosSdkBuild.Included || !EosSdkBuild.HasNativeLibrary(AppContext.BaseDirectory))
            {
                Assert.Pass("this build does not include the EOS SDK and its native library");
            }

            WriteLocalFile();

            Assert.That(CreateWithLibrary(AppContext.BaseDirectory, "--stats"), Is.Null);
            Assert.That(_modules, Has.Count.EqualTo(MatchModules + 1));
            Assert.That(_modules[MatchModules], Is.InstanceOf<ServerStatsModule>());
            Assert.That(_output.ToString(), Does.Contain("[Stats] tuning default"));
        }

        [Test]
        public void AllThreeAreAddedInTheOrderReportsStatsTelemetry()
        {
            if (!EosSdkBuild.Included || !EosSdkBuild.HasNativeLibrary(AppContext.BaseDirectory))
            {
                Assert.Pass("this build does not include the EOS SDK and its native library");
            }

            WriteLocalFile();

            Assert.That(
                CreateWithLibrary(
                    AppContext.BaseDirectory,
                    "--report-file", Path.Combine(_records.Path, "reports.jsonl"), "--stats", "--telemetry", "--telemetry-dir", _records.Path),
                Is.Null);
            Assert.That(_modules, Has.Count.EqualTo(MatchModules + 3));
            Assert.That(_modules[MatchModules], Is.InstanceOf<ServerReports>());
            Assert.That(_modules[MatchModules + 1], Is.InstanceOf<ServerStatsModule>());
            Assert.That(_modules[MatchModules + 2], Is.InstanceOf<ServerTelemetry>());
        }

        private void TearDownModules()
        {
            foreach (var module in _modules)
            {
                (module as IDisposable)?.Dispose();
            }

            _modules.Clear();
        }

        private string WriteTuning(string fileName, string json)
        {
            var folder = Path.Combine(_roots.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(folder);
            var path = Path.GetFullPath(Path.Combine(folder, fileName));
            File.WriteAllText(path, json);
            return path;
        }

        private void WriteLocalFile()
        {
            var values = new List<KeyValuePair<string, string>>();
            foreach (var name in EosCredentials.VariableNames)
            {
                values.Add(new KeyValuePair<string, string>(name, "x"));
            }

            File.WriteAllText(Path.Combine(_roots.Path, EosCredentials.LocalFileName), FlatJsonObject.Format(values));
        }

        private string Create(params string[] args)
        {
            return CreateWithLibrary(_library.Path, args);
        }

        private string CreateWithLibrary(string libraryDirectory, params string[] args)
        {
            Assert.That(ServerLaunch.TryParse(args, _ => null, out var launch, out var parseError), Is.True, parseError);
            var runtime = new ServerRuntime(
                _output,
                TextWriter.Null,
                name => _environment.TryGetValue(name, out var value) ? value : null,
                new ImmediateStopSignal(),
                new[] { _roots.Path },
                new[] { _roots.Path },
                () => throw new InvalidOperationException("no transport in this test"));
            return ServerModules.Create(launch, runtime, _modules, libraryDirectory, out var error) ? null : error;
        }
    }
}

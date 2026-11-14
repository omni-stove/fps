using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.EosSdk;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    // SDK を置いた手元でも置いていない CI でも同じテストが通るよう、どちらのビルドかで期待を分ける
    public sealed class ServerModulesTests
    {
        private TempDirectory _roots;
        private TempDirectory _library;
        private Dictionary<string, string> _environment;
        private List<IServerModule> _modules;

        [SetUp]
        public void SetUp()
        {
            _roots = new TempDirectory();
            _library = new TempDirectory();
            _environment = new Dictionary<string, string>();
            _modules = new List<IServerModule>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var module in _modules)
            {
                (module as IDisposable)?.Dispose();
            }

            _roots.Dispose();
            _library.Dispose();
        }

        [Test]
        public void WithoutRegisterLobbyAddsMovementSnapshotsAndRules()
        {
            Assert.That(Create(_library.Path), Is.Null);
            Assert.That(_modules, Has.Count.EqualTo(3));
            Assert.That(_modules[0], Is.InstanceOf<ServerMovement>());
            Assert.That(_modules[1], Is.InstanceOf<MatchRulesModule>());
            Assert.That(_modules[2], Is.InstanceOf<ServerSnapshots>());
        }

        [Test]
        public void MissingCredentialsAreNamed()
        {
            _environment[EosCredentials.ProductIdVariable] = "p";

            var error = Create(_library.Path, "-register-lobby", "203.0.113.10:7777");

            Assert.That(error, Does.StartWith("[Lobby] -register-lobby needs EOS credentials; missing EOS_SANDBOX_ID,"));
            Assert.That(error, Does.Contain(EosCredentials.LocalFileName));
            Assert.That(_modules, Has.None.InstanceOf<ServerLobbyModule>());
        }

        [Test]
        public void AnUnreadableLocalFileIsReported()
        {
            File.WriteAllText(Path.Combine(_roots.Path, EosCredentials.LocalFileName), "[1]");

            var error = Create(_library.Path, "-register-lobby", "203.0.113.10:7777");

            Assert.That(error, Does.StartWith("[Lobby] cannot load EOS credentials: "));
        }

        [Test]
        public void WithoutTheSdkOrItsLibraryTheLobbyCannotBeUsed()
        {
            WriteLocalFile();

            var error = Create(_library.Path, "-register-lobby", "203.0.113.10:7777");

            Assert.That(error, Does.StartWith("[Lobby] -register-lobby cannot use EOS: "));
            Assert.That(error, EosSdkBuild.Included ? Does.Contain("native library") : Does.Contain("not in this build"));
            Assert.That(_modules, Has.None.InstanceOf<ServerLobbyModule>());
        }

        [Test]
        public void WithTheSdkAndCredentialsTheLobbyModuleIsAdded()
        {
            if (!EosSdkBuild.Included || !EosSdkBuild.HasNativeLibrary(AppContext.BaseDirectory))
            {
                Assert.Pass("this build does not include the EOS SDK and its native library");
            }

            WriteLocalFile();

            Assert.That(Create(AppContext.BaseDirectory, "-register-lobby", "203.0.113.10:7777"), Is.Null);
            Assert.That(_modules, Has.Count.EqualTo(4));
            Assert.That(_modules[3], Is.InstanceOf<ServerLobbyModule>());
        }

        [Test]
        public void MovementUsesTheMotorValuesAndRulesOfTheRulesFile()
        {
            var motor = new NetMotorSettings(
                new MotorSpec(6f, 9f, 1.5f, 18f, 0.2f, 3f), new StanceGeometry(1.8f, 0.9f, 1.6f, 1.1f, 0.4f), CapsuleMoverSpec.Default);
            var rules = GameRules.Default.WithObjective(MatchObjective.Domination);
            var data = new ServerRulesData("Domination", new NetMatchFile(
                "Domination", rules, NetMatchSettings.Default, "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle }, new[] { WeaponCategory.Handgun } }),
                new[] { TestRules.Rifle(), TestRules.Handgun() }, 0.4f, null, null, motor), PingSpec.Default, "memory", null);

            var settings = ServerModules.MotorSettingsFrom(data);

            Assert.That(settings.Motor, Is.SameAs(motor.Motor));
            Assert.That(settings.Stance, Is.SameAs(motor.Stance));
            Assert.That(settings.Mover, Is.SameAs(motor.Mover));
            Assert.That(settings.Rules, Is.SameAs(rules));
            Assert.That(settings.KillPlaneY, Is.EqualTo(ServerMotorSettings.Default.KillPlaneY));
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

        private string Create(string libraryDirectory, params string[] args)
        {
            Assert.That(ServerLaunch.TryParse(args, _ => null, out var launch, out var parseError), Is.True, parseError);
            var runtime = new ServerRuntime(
                TextWriter.Null,
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

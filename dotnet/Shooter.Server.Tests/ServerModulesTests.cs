using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.EosSdk;

namespace Shooter.Server.Tests
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
        public void WithoutRegisterLobbyAddsTheMatchAndCombatModulesAndSnapshotsLast()
        {
            Assert.That(Create(_library.Path), Is.Null);
            Assert.That(_modules, Has.Count.EqualTo(12));
            Assert.That(_modules[0], Is.InstanceOf<ServerAntiCheat>());
            Assert.That(((ServerAntiCheat)_modules[0]).Enabled, Is.False);
            Assert.That(_modules[1], Is.InstanceOf<ServerMovement>());
            Assert.That(_modules[2], Is.InstanceOf<MatchRulesModule>());
            Assert.That(_modules[3], Is.InstanceOf<ServerVehicles>());
            Assert.That(_modules[4], Is.InstanceOf<ServerCombat>());
            Assert.That(_modules[5], Is.InstanceOf<ServerInventories>());
            Assert.That(_modules[6], Is.InstanceOf<ServerBots>());
            Assert.That(_modules[7], Is.InstanceOf<ServerMelee>());
            Assert.That(_modules[8], Is.InstanceOf<ServerStreaks>());
            Assert.That(_modules[9], Is.InstanceOf<ServerDeployables>());
            Assert.That(_modules[10], Is.InstanceOf<ServerKillCam>());
            Assert.That(_modules[11], Is.InstanceOf<ServerSnapshots>());
        }

        [Test]
        public void WithoutTuningFilesTheCombatModulesUseTheDefaults()
        {
            Assert.That(ServerCombatTuning.TryLoad(new[] { _roots.Path }, out var tuning, out var error), Is.True, error);

            Assert.That(tuning.Melee, Is.SameAs(MeleeSpec.Default));
            Assert.That(tuning.Streaks, Is.SameAs(StreakSpec.Default));
            Assert.That(tuning.Deployables.Count, Is.Zero);
            Assert.That(tuning.LogLine, Is.EqualTo("[Server] combat tuning melee=default streaks=default deployables=none"));
        }

        [Test]
        public void TheCombatModulesReadTheirTuningFromServerData()
        {
            var tuningDirectory = Path.Combine(_roots.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuningDirectory);
            File.WriteAllText(Path.Combine(tuningDirectory, ServerCombatTuning.MeleeFileName), "{\"damage\": 80, \"reach\": 2.5}");
            File.WriteAllText(
                Path.Combine(tuningDirectory, ServerCombatTuning.StreaksFileName),
                "{\"rewards\": [{\"id\": \"uav\", \"killsRequired\": 2}]}");

            Assert.That(ServerCombatTuning.TryLoad(new[] { _roots.Path }, out var tuning, out var error), Is.True, error);

            Assert.That(tuning.Melee.Damage, Is.EqualTo(80));
            Assert.That(tuning.Melee.Reach, Is.EqualTo(2.5f));
            Assert.That(tuning.Streaks.Rewards.Count, Is.EqualTo(1));
            Assert.That(tuning.Streaks.Rewards[0].KillsRequired, Is.EqualTo(2));
            Assert.That(tuning.MeleePath, Is.EqualTo(Path.GetFullPath(Path.Combine(tuningDirectory, ServerCombatTuning.MeleeFileName))));
        }

        [Test]
        public void AnUnreadableTuningFileStopsTheStart()
        {
            var tuningDirectory = Path.Combine(_roots.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuningDirectory);
            File.WriteAllText(Path.Combine(tuningDirectory, ServerCombatTuning.MeleeFileName), "{\"dmg\": 10}");

            Assert.That(ServerCombatTuning.TryLoad(new[] { _roots.Path }, out var tuning, out var error), Is.False);

            Assert.That(tuning, Is.Null);
            Assert.That(error, Does.StartWith("cannot read melee tuning: "));
        }

        [Test]
        public void OnlyAGunGameCountsMeleeKillsWithTheKnifeId()
        {
            var gunGame = new NetMatchSettings(gunGame: new GunGameSpec(
                600f, 2f, new[] { TestRules.RifleId }, new[] { "knife", "axe" }, true));

            Assert.That(ServerModules.MeleeWeaponIdFor(TestRules.Data(GameRules.Default.WithObjective(MatchObjective.GunGame), gunGame)), Is.EqualTo("knife"));
            Assert.That(ServerModules.MeleeWeaponIdFor(TestRules.Data(GameRules.Default, gunGame)), Is.Null);
            Assert.That(ServerModules.MeleeWeaponIdFor(TestRules.Data(GameRules.Default.WithObjective(MatchObjective.GunGame), new NetMatchSettings(
                gunGame: new GunGameSpec(600f, 2f, new[] { TestRules.RifleId }, Array.Empty<string>(), true)))), Is.Null);
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
            Assert.That(_modules, Has.Count.EqualTo(13));
            Assert.That(_modules[12], Is.InstanceOf<ServerLobbyModule>());
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

        [Test]
        public void MovementUsesTheSwimAndWallRunValuesOfTheRulesFile()
        {
            var swim = new SwimTuning(2f, 8f, 30f);
            var wallRun = new WallRunTuning(true, 2.5f, 4f, 0.5f, 9f);
            var motor = new NetMotorSettings(
                MotorSpec.Default, NetMotorSettings.Default.Stance, CapsuleMoverSpec.Default, ProneSpec.Default, swim, wallRun);
            var data = new ServerRulesData("TeamDeathmatch", new NetMatchFile(
                "TeamDeathmatch", GameRules.Default, NetMatchSettings.Default, "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle }, new[] { WeaponCategory.Handgun } }),
                new[] { TestRules.Rifle(), TestRules.Handgun() }, 0.4f, null, null, motor), PingSpec.Default, "memory", null);

            var settings = ServerModules.MotorSettingsFrom(data);

            Assert.That(settings.Swim, Is.SameAs(swim));
            Assert.That(settings.WallRun, Is.SameAs(wallRun));
            Assert.That(settings.WithRules(GameRules.Default).Swim, Is.SameAs(swim), "changing the rules keeps the swim values");
            Assert.That(settings.WithSpawnPosition(Vector3.One).WallRun, Is.SameAs(wallRun), "moving the spawn keeps the wall run values");
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

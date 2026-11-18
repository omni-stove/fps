using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Server.Eos;

namespace Shooter.Server
{
    internal static class ServerModules
    {
        public static bool Create(
            ServerLaunch launch, ServerRuntime runtime, ServerRulesData rules, ICollection<IServerModule> modules, out string error)
        {
            ArgumentNullException.ThrowIfNull(rules);
            ArgumentNullException.ThrowIfNull(runtime);
            if (!ServerCombatTuning.TryLoad(runtime.MapSearchRoots, out var tuning, out var tuningError))
            {
                error = ServerProgram.LogTag + " " + tuningError;
                return false;
            }

            runtime.Output.WriteLine(tuning.LogLine);
            return Create(launch, runtime, modules, AppContext.BaseDirectory, MotorSettingsFrom(rules), tuning, MeleeWeaponIdFor(rules), out error);
        }

        // Gun Game は近接で倒したかを武器の Id (meleeWeaponIds) で見分けるので、近接の一撃をその先頭の Id で数えさせる。
        // それ以外の試合はオフラインの MeleeController と同じく持っている武器の Id で数える
        public static string MeleeWeaponIdFor(ServerRulesData rules)
        {
            ArgumentNullException.ThrowIfNull(rules);
            var melee = rules.Settings.GunGame.MeleeWeaponIds;
            return rules.Rules.Objective == MatchObjective.GunGame && melee.Count > 0 ? melee[0] : null;
        }

        // 移動はクライアントの予測と同じ値で動かすため、ルールのファイルに書き出した NetPlayer.prefab の移動の値と体の寸法を使う。
        public static ServerMotorSettings MotorSettingsFrom(ServerRulesData rules)
        {
            ArgumentNullException.ThrowIfNull(rules);
            var motor = rules.Motor;
            var defaults = ServerMotorSettings.Default;
            return new ServerMotorSettings(
                motor.Motor, motor.Stance, motor.Mover, motor.Prone, motor.Swim, motor.WallRun, rules.Rules, defaults.KillPlaneY, defaults.SpawnPosition);
        }

        public static bool Create(
            ServerLaunch launch, ServerRuntime runtime, ICollection<IServerModule> modules, string libraryDirectory, out string error)
        {
            return Create(launch, runtime, modules, libraryDirectory, ServerMotorSettings.Default, ServerCombatTuning.Default, null, out error);
        }

        public static bool Create(
            ServerLaunch launch,
            ServerRuntime runtime,
            ICollection<IServerModule> modules,
            string libraryDirectory,
            ServerMotorSettings motorSettings,
            ServerCombatTuning tuning,
            string meleeWeaponId,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(launch);
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(modules);
            ArgumentNullException.ThrowIfNull(motorSettings);
            ArgumentNullException.ThrowIfNull(tuning);

            if (!TryCreateAntiCheat(launch, runtime, libraryDirectory, out var antiCheat, out error))
            {
                return false;
            }

            // Hello は承認の直後に送り合うので、他のモジュールより先に接続を知らせる
            modules.Add(antiCheat);

            // スナップショットは移動が tick の入力を処理し、ルールと戦闘がその tick を進めた後の状態を載せるので、どれより後にも登録する。
            // 戦闘はルールが出現させた後の位置を当たり判定に残すので、ルールより後
            // 乗り物は乗っている人を座席へ置き直してから戦闘に当たり判定を残させるので、戦闘より先
            var movement = new ServerMovement(motorSettings);
            var rules = new MatchRulesModule();
            var vehicles = new ServerVehicles(rules, movement);
            var combat = new ServerCombat(rules, movement, vehicles);
            modules.Add(movement);
            modules.Add(rules);
            modules.Add(vehicles);
            modules.Add(combat);

            // 持ち物は出現 (ルール) の後に持たせ直し、その tick のうちに持ち主へ送る
            modules.Add(new ServerInventories(rules, combat));

            // Bot は戦闘がこの tick の当たり判定を記録した後の位置へ撃ち、スナップショットには動いた後の位置を載せる
            modules.Add(new ServerBots(rules, movement, combat));

            // 近接・ストリーク・設置物は戦闘の DealDamage・Explode でダメージを与え、キルカムは戦闘が出すキルでクリップを送るので、戦闘より後。
            // キルカムは Bot が動いた後の姿勢を残すので、Bot より後
            var streaks = new ServerStreaks(rules, combat, tuning.Streaks);
            var deployables = new ServerDeployables(rules, combat, tuning.Deployables, tuning.DeployablesPath);
            modules.Add(new ServerMelee(combat, movement, tuning.Melee, meleeWeaponId));
            modules.Add(streaks);
            modules.Add(deployables);
            modules.Add(new ServerKillCam(rules, combat));
            modules.Add(new ServerSnapshots(
                motorSettings.Rules, new ISnapshotContributor[] { movement, rules, combat, vehicles, streaks, deployables }));

            // 通報・統計・テレメトリは試合の状態を変えず、ルールが出したキルと試合の始まり・終わりを読むだけなので、スナップショットより後でよい
            if (!TryAddRecords(launch, runtime, libraryDirectory, rules, modules, out error))
            {
                return false;
            }

            if (!launch.Options.RegistersLobby)
            {
                return true;
            }

            var flag = NetLaunchOptions.RegisterLobbyFlag;
            if (!TryLoadCredentials(runtime, ServerLobbyService.LogPrefix, flag, out var credentials, out error))
            {
                return false;
            }

            if (!EosServerLobby.TryPrepare(credentials, launch.Version, libraryDirectory, out var createBackend, out var sdkError))
            {
                error = ServerLobbyService.LogPrefix + flag + " cannot use EOS: " + sdkError;
                return false;
            }

            modules.Add(new ServerLobbyModule(createBackend, launch.Options.RegisterLobbyEndpoint) { Region = launch.Region });
            return true;
        }

        private static bool TryAddRecords(
            ServerLaunch launch,
            ServerRuntime runtime,
            string libraryDirectory,
            MatchRulesModule rules,
            ICollection<IServerModule> modules,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(launch);
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(rules);
            ArgumentNullException.ThrowIfNull(modules);

            var records = launch.Records;
            var output = runtime.Output;
            if (records.ReportFile != null)
            {
                modules.Add(new ServerReports(records.ReportFile, () => DateTime.UtcNow));
            }
            else
            {
                output.WriteLine(ServerRecordsLog.ReportsOff());
            }

            if (!TryAddStats(launch, runtime, libraryDirectory, rules, modules, out error))
            {
                return false;
            }

            return TryAddTelemetry(records, runtime, modules, out error);
        }

        // 統計は EOS の ProductUserId ごとに送るので、認証情報の無いまま --stats で起動させると、送れたつもりで何も残らない
        private static bool TryAddStats(
            ServerLaunch launch,
            ServerRuntime runtime,
            string libraryDirectory,
            MatchRulesModule rules,
            ICollection<IServerModule> modules,
            out string error)
        {
            if (!launch.Records.SendsStats)
            {
                runtime.Output.WriteLine(ServerRecordsLog.StatsNotAsked());
                error = null;
                return true;
            }

            var prefix = StatsIngestLog.Tag + " ";
            var flag = ServerLaunch.StatsFlag;
            if (!TryLoadCredentials(runtime, prefix, flag, out var credentials, out error))
            {
                return false;
            }

            if (!EosServerStats.TryPrepare(credentials, launch.Version, libraryDirectory, out var createBackend, out var sdkError))
            {
                error = prefix + flag + " cannot use EOS: " + sdkError;
                return false;
            }

            var root = RootWithTuning(runtime.MapSearchRoots, ServerStatsModule.TuningFileName);
            var spec = LeaderboardSpec.Default;
            if (root != null && !ServerStatsModule.TryLoadSpec(root, out spec, out var specError))
            {
                error = prefix + specError;
                return false;
            }

            runtime.Output.WriteLine(ServerRecordsLog.LeaderboardTuning(TuningPath(root, ServerStatsModule.TuningFileName)));
            modules.Add(new ServerStatsModule(rules, createBackend, spec));
            error = null;
            return true;
        }

        private static bool TryAddTelemetry(
            ServerRecordsLaunch records, ServerRuntime runtime, ICollection<IServerModule> modules, out string error)
        {
            // 同意が無ければ何も残さないので、壊れた調整値のファイルで起動を止めないよう、読みもしない
            if (!records.TelemetryConsent)
            {
                runtime.Output.WriteLine(ServerRecordsLog.TelemetryNotAsked());
                error = null;
                return true;
            }

            var root = RootWithTuning(runtime.MapSearchRoots, ServerTelemetry.TuningFileName);
            var spec = TelemetrySpec.Default;
            if (root != null && !ServerTelemetry.TryLoadSpec(root, out spec, out var specError))
            {
                error = TelemetryLog.Tag + " " + specError;
                return false;
            }

            if (spec.Sink == TelemetrySink.LocalFile && records.TelemetryDirectory == null)
            {
                error = ServerRecordsLog.TelemetryNeedsFolder();
                return false;
            }

            runtime.Output.WriteLine(ServerRecordsLog.TelemetryTuning(TuningPath(root, ServerTelemetry.TuningFileName)));
            modules.Add(new ServerTelemetry(spec, records.TelemetryDirectory, true, () => DateTime.UtcNow));
            error = null;
            return true;
        }

        // ルールや近接の調整値 (ServerCombatTuning) と同じく、探す場所を前から順に見て最初に見つかったファイルを使う
        private static string RootWithTuning(IReadOnlyList<string> searchRoots, string fileName)
        {
            foreach (var root in searchRoots)
            {
                if (File.Exists(TuningPath(root, fileName)))
                {
                    return root;
                }
            }

            return null;
        }

        private static string TuningPath(string root, string fileName)
        {
            return root == null
                ? null
                : Path.GetFullPath(Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName, fileName));
        }

        private static bool TryCreateAntiCheat(
            ServerLaunch launch, ServerRuntime runtime, string libraryDirectory, out ServerAntiCheat antiCheat, out string error)
        {
            antiCheat = null;
            var settings = launch.AntiCheat;
            if (!settings.Enabled)
            {
                antiCheat = new ServerAntiCheat(settings, null);
                error = null;
                return true;
            }

            var prefix = AntiCheatSettings.LogTag + " ";
            var flag = AntiCheatSettings.EacFlag;
            if (!TryLoadCredentials(runtime, prefix, flag, out var credentials, out error))
            {
                return false;
            }

            if (!EosServerAntiCheat.TryPrepare(credentials, launch.Version, libraryDirectory, out var createBackend, out var sdkError))
            {
                error = prefix + flag + " cannot use EOS: " + sdkError;
                return false;
            }

            antiCheat = new ServerAntiCheat(settings, createBackend);
            return true;
        }

        private static bool TryLoadCredentials(ServerRuntime runtime, string prefix, string flag, out EosCredentials credentials, out string error)
        {
            if (!EosCredentialSource.TryLoad(runtime.Environment, runtime.CredentialSearchRoots, out credentials, out var missing, out var loadError))
            {
                error = prefix + "cannot load EOS credentials: " + loadError;
                return false;
            }

            if (credentials == null)
            {
                error = prefix + flag + " needs EOS credentials; missing " + string.Join(", ", missing)
                    + " (set them as environment variables or in " + EosCredentials.LocalFileName + ")";
                return false;
            }

            error = null;
            return true;
        }
    }
}

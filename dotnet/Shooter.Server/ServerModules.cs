using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Server.Eos;

namespace Shooter.Server
{
    /// <summary>起動引数から、このプロセスで動かすモジュールを決める。</summary>
    internal static class ServerModules
    {
        public static bool Create(
            ServerLaunch launch, ServerRuntime runtime, ServerRulesData rules, ICollection<IServerModule> modules, out string error)
        {
            ArgumentNullException.ThrowIfNull(rules);
            return Create(launch, runtime, modules, AppContext.BaseDirectory, MotorSettingsFrom(rules), out error);
        }

        /// <summary>移動はクライアントの予測と同じ値で動かすため、ルールのファイルに書き出した NetPlayer.prefab の移動の値と体の寸法を使う。</summary>
        public static ServerMotorSettings MotorSettingsFrom(ServerRulesData rules)
        {
            ArgumentNullException.ThrowIfNull(rules);
            var motor = rules.Motor;
            var defaults = ServerMotorSettings.Default;
            return new ServerMotorSettings(
                motor.Motor, motor.Stance, motor.Mover, motor.Prone, rules.Rules, defaults.KillPlaneY, defaults.SpawnPosition);
        }

        /// <param name="launch">起動設定。</param>
        /// <param name="runtime">環境変数と認証情報のファイルの置き場所。</param>
        /// <param name="modules">足す先。</param>
        /// <param name="libraryDirectory">EOS のネイティブのライブラリを探すフォルダ。</param>
        /// <param name="error">足せないときの理由。</param>
        public static bool Create(
            ServerLaunch launch, ServerRuntime runtime, ICollection<IServerModule> modules, string libraryDirectory, out string error)
        {
            return Create(launch, runtime, modules, libraryDirectory, ServerMotorSettings.Default, out error);
        }

        public static bool Create(
            ServerLaunch launch,
            ServerRuntime runtime,
            ICollection<IServerModule> modules,
            string libraryDirectory,
            ServerMotorSettings motorSettings,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(launch);
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(modules);
            ArgumentNullException.ThrowIfNull(motorSettings);

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
            modules.Add(new ServerSnapshots(motorSettings.Rules, new ISnapshotContributor[] { movement, rules, combat, vehicles }));

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

            modules.Add(new ServerLobbyModule(createBackend, launch.Options.RegisterLobbyEndpoint));
            return true;
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

using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Server.Eos;

namespace Fps.Server
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
            return new ServerMotorSettings(motor.Motor, motor.Stance, motor.Mover, rules.Rules, defaults.KillPlaneY, defaults.SpawnPosition);
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

            // スナップショットは移動が tick の入力を処理し、ルールがその tick を進めた後の状態を載せるので、両方より後に登録する
            var movement = new ServerMovement(motorSettings);
            var rules = new MatchRulesModule();
            modules.Add(movement);
            modules.Add(rules);
            modules.Add(new ServerSnapshots(motorSettings.Rules, new ISnapshotContributor[] { movement, rules }));

            error = null;
            if (!launch.Options.RegistersLobby)
            {
                return true;
            }

            var flag = NetLaunchOptions.RegisterLobbyFlag;
            if (!EosCredentialSource.TryLoad(runtime.Environment, runtime.CredentialSearchRoots, out var credentials, out var missing, out var loadError))
            {
                error = ServerLobbyService.LogPrefix + "cannot load EOS credentials: " + loadError;
                return false;
            }

            if (credentials == null)
            {
                error = ServerLobbyService.LogPrefix + flag + " needs EOS credentials; missing " + string.Join(", ", missing)
                    + " (set them as environment variables or in " + EosCredentials.LocalFileName + ")";
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
    }
}

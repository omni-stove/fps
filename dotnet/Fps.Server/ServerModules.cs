using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Server.Eos;

namespace Fps.Server
{
    /// <summary>起動引数から、このプロセスで動かすモジュールを決める。</summary>
    internal static class ServerModules
    {
        public static bool Create(ServerLaunch launch, ServerRuntime runtime, ICollection<IServerModule> modules, out string error)
        {
            return Create(launch, runtime, modules, AppContext.BaseDirectory, out error);
        }

        /// <param name="launch">起動設定。</param>
        /// <param name="runtime">環境変数と認証情報のファイルの置き場所。</param>
        /// <param name="modules">足す先。</param>
        /// <param name="libraryDirectory">EOS のネイティブのライブラリを探すフォルダ。</param>
        /// <param name="error">足せないときの理由。</param>
        public static bool Create(
            ServerLaunch launch, ServerRuntime runtime, ICollection<IServerModule> modules, string libraryDirectory, out string error)
        {
            ArgumentNullException.ThrowIfNull(launch);
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(modules);

            error = null;
            if (!launch.Options.RegistersLobby)
            {
                modules.Add(new MatchRulesModule());
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
            modules.Add(new MatchRulesModule());
            return true;
        }
    }
}

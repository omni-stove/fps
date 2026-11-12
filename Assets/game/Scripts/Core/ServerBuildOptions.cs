using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 専用サーバーのビルド設定。コマンドライン引数 <c>-serverPlatform macos|linux</c> から作り、
    /// 出力先 <c>Builds/server-&lt;platform&gt;/</c> と実行ファイルのパスを決める。
    /// </summary>
    public sealed class ServerBuildOptions
    {
        public const string PlatformFlag = "-serverPlatform";

        /// <summary>ビルドの出力をまとめるディレクトリ (プロジェクトのルートからの相対パス、gitignore 済み)。</summary>
        public const string OutputRoot = "Builds";

        public const string ExecutableName = "fps-server";

        public ServerPlatform Platform { get; }

        /// <summary>出力先のディレクトリ。例: <c>Builds/server-linux</c>。</summary>
        public string OutputDirectory => OutputRoot + "/server-" + PlatformName(Platform);

        /// <summary>BuildPipeline に渡す実行ファイルのパス。Linux は Unity の慣例どおり <c>.x86_64</c> を付ける。</summary>
        public string ExecutablePath => OutputDirectory + "/" + ExecutableName + ExecutableExtension(Platform);

        public ServerBuildOptions(ServerPlatform platform)
        {
            if (!Enum.IsDefined(typeof(ServerPlatform), platform))
            {
                throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform");
            }

            Platform = platform;
        }

        /// <summary>
        /// コマンドライン引数を解析する。<c>-serverPlatform</c> が無ければ <paramref name="defaultPlatform"/> を使う。
        /// 知らない引数 (Unity 自身の <c>-batchmode</c> など) は無視する。
        /// </summary>
        /// <param name="args">コマンドライン引数。</param>
        /// <param name="defaultPlatform">-serverPlatform が無いときの OS。</param>
        /// <param name="options">成功したときの設定。失敗したときは null。</param>
        /// <param name="error">失敗したときの理由。成功したときは null。</param>
        public static bool TryParse(
            IReadOnlyList<string> args,
            ServerPlatform defaultPlatform,
            out ServerBuildOptions options,
            out string error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (!Enum.IsDefined(typeof(ServerPlatform), defaultPlatform))
            {
                throw new ArgumentOutOfRangeException(nameof(defaultPlatform), defaultPlatform, "unknown platform");
            }

            options = null;
            string platformText = null;
            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], PlatformFlag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= args.Count || string.IsNullOrEmpty(args[i + 1]) || args[i + 1][0] == '-')
                {
                    error = PlatformFlag + " requires a value";
                    return false;
                }

                if (platformText != null)
                {
                    error = PlatformFlag + " is given more than once";
                    return false;
                }

                platformText = args[++i];
            }

            var platform = defaultPlatform;
            if (platformText != null && !TryParsePlatform(platformText, out platform))
            {
                error = PlatformFlag + " must be macos or linux (got '" + platformText + "')";
                return false;
            }

            options = new ServerBuildOptions(platform);
            error = null;
            return true;
        }

        /// <summary>出力先やログに使う小文字の名前 (macos / linux)。</summary>
        public static string PlatformName(ServerPlatform platform)
        {
            switch (platform)
            {
                case ServerPlatform.MacOS:
                    return "macos";
                case ServerPlatform.Linux:
                    return "linux";
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform");
            }
        }

        public override string ToString()
        {
            return PlatformName(Platform) + " -> " + ExecutablePath;
        }

        private static string ExecutableExtension(ServerPlatform platform)
        {
            return platform == ServerPlatform.Linux ? ".x86_64" : "";
        }

        private static bool TryParsePlatform(string text, out ServerPlatform platform)
        {
            foreach (ServerPlatform candidate in Enum.GetValues(typeof(ServerPlatform)))
            {
                if (string.Equals(text, PlatformName(candidate), StringComparison.OrdinalIgnoreCase))
                {
                    platform = candidate;
                    return true;
                }
            }

            platform = default;
            return false;
        }
    }
}

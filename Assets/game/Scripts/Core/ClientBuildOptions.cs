using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// クライアント (プレイヤー) のビルド設定。コマンドライン引数 <c>-clientPlatform windows|macos</c> から作り、
    /// 出力先 <c>Builds/client-&lt;platform&gt;/</c> と実行ファイル (macOS は .app バンドル) のパスを決める。
    /// </summary>
    public sealed class ClientBuildOptions
    {
        public const string PlatformFlag = "-clientPlatform";

        /// <summary>ビルドの出力をまとめるディレクトリ (プロジェクトのルートからの相対パス、gitignore 済み)。</summary>
        public const string OutputRoot = "Builds";

        /// <summary>実行ファイルの名前。PlayerSettings.productName と同じにして、Windows の <c>fps_Data</c> と名前をそろえる。</summary>
        public const string ExecutableName = "fps";

        public ClientPlatform Platform { get; }

        /// <summary>出力先のディレクトリ。例: <c>Builds/client-windows</c>。</summary>
        public string OutputDirectory => OutputRoot + "/client-" + PlatformName(Platform);

        /// <summary>BuildPipeline に渡すパス。Windows は <c>fps.exe</c>、macOS は <c>fps.app</c> (バンドルのディレクトリ)。</summary>
        public string ExecutablePath => OutputDirectory + "/" + ExecutableName + ExecutableExtension(Platform);

        public ClientBuildOptions(ClientPlatform platform)
        {
            if (!Enum.IsDefined(typeof(ClientPlatform), platform))
            {
                throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform");
            }

            Platform = platform;
        }

        /// <summary>
        /// コマンドライン引数を解析する。<c>-clientPlatform</c> が無ければ <paramref name="defaultPlatform"/> を使う。
        /// 知らない引数 (Unity 自身の <c>-batchmode</c> など) は無視する。
        /// </summary>
        /// <param name="args">コマンドライン引数。</param>
        /// <param name="defaultPlatform">-clientPlatform が無いときの OS。</param>
        /// <param name="options">成功したときの設定。失敗したときは null。</param>
        /// <param name="error">失敗したときの理由。成功したときは null。</param>
        public static bool TryParse(
            IReadOnlyList<string> args,
            ClientPlatform defaultPlatform,
            out ClientBuildOptions options,
            out string error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (!Enum.IsDefined(typeof(ClientPlatform), defaultPlatform))
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
                error = PlatformFlag + " must be windows or macos (got '" + platformText + "')";
                return false;
            }

            options = new ClientBuildOptions(platform);
            error = null;
            return true;
        }

        /// <summary>出力先やログ、配布する zip の名前に使う小文字の名前 (windows / macos)。</summary>
        public static string PlatformName(ClientPlatform platform)
        {
            switch (platform)
            {
                case ClientPlatform.Windows:
                    return "windows";
                case ClientPlatform.MacOS:
                    return "macos";
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform");
            }
        }

        public override string ToString()
        {
            return PlatformName(Platform) + " -> " + ExecutablePath;
        }

        private static string ExecutableExtension(ClientPlatform platform)
        {
            return platform == ClientPlatform.Windows ? ".exe" : ".app";
        }

        private static bool TryParsePlatform(string text, out ClientPlatform platform)
        {
            foreach (ClientPlatform candidate in Enum.GetValues(typeof(ClientPlatform)))
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

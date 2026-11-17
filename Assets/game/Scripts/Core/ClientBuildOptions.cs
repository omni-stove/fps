using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class ClientBuildOptions
    {
        public const string PlatformFlag = "-clientPlatform";

        public const string OutputRoot = "Builds";

        // PlayerSettings.productName と同じにして、Windows の shooter_Data と名前をそろえる。
        public const string ExecutableName = "shooter";

        public ClientPlatform Platform { get; }

        public string OutputDirectory => OutputRoot + "/client-" + PlatformName(Platform);

        public string ExecutablePath => OutputDirectory + "/" + ExecutableName + ExecutableExtension(Platform);

        public ClientBuildOptions(ClientPlatform platform)
        {
            if (!Enum.IsDefined(typeof(ClientPlatform), platform))
            {
                throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform");
            }

            Platform = platform;
        }

        // 知らない引数を拒まないのは、Unity 自身の -batchmode なども同じ引数に並ぶため。
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
                error = PlatformFlag + " must be windows, macos or webgl (got '" + platformText + "')";
                return false;
            }

            options = new ClientBuildOptions(platform);
            error = null;
            return true;
        }

        public static string PlatformName(ClientPlatform platform)
        {
            switch (platform)
            {
                case ClientPlatform.Windows:
                    return "windows";
                case ClientPlatform.MacOS:
                    return "macos";
                case ClientPlatform.WebGL:
                    return "webgl";
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
            switch (platform)
            {
                case ClientPlatform.Windows:
                    return ".exe";
                case ClientPlatform.MacOS:
                    return ".app";
                default:
                    return string.Empty;
            }
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

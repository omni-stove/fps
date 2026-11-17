using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class ServerBuildOptions
    {
        public const string PlatformFlag = "-serverPlatform";

        public const string OutputRoot = "Builds";

        public const string ExecutableName = "shooter-server";

        public ServerPlatform Platform { get; }

        public string OutputDirectory => OutputRoot + "/server-" + PlatformName(Platform);

        // Linux は Unity の慣例どおり .x86_64 を付ける。
        public string ExecutablePath => OutputDirectory + "/" + ExecutableName + ExecutableExtension(Platform);

        public ServerBuildOptions(ServerPlatform platform)
        {
            if (!Enum.IsDefined(typeof(ServerPlatform), platform))
            {
                throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform");
            }

            Platform = platform;
        }

        // Unity 自身の -batchmode なども混ざるので、知らない引数は無視する。
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

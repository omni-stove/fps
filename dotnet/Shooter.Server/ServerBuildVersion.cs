using System;
using System.Collections.Generic;
using System.Reflection;

namespace Shooter.Server
{
    // Unity のサーバーが版をビルドに持つ (Application.version) のと同じく、引数なしで起動しても版が分かるよう、アセンブリに焼き込んだ版を読む。
    internal static class ServerBuildVersion
    {
        public const string MetadataKey = "ShooterBuildVersion";

        public static string Embedded()
        {
            return Find(typeof(ServerBuildVersion).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>());
        }

        // 検証と正規化は ServerLaunch が引数の版と同じ規則で行うので、ここではしない。
        public static string Find(IEnumerable<AssemblyMetadataAttribute> metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            foreach (var entry in metadata)
            {
                if (entry.Key == MetadataKey && !string.IsNullOrWhiteSpace(entry.Value))
                {
                    return entry.Value;
                }
            }

            return null;
        }
    }
}

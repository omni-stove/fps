using System;
using System.Collections.Generic;
using System.Reflection;

namespace Fps.Server
{
    /// <summary>
    /// <c>dotnet publish -p:FpsBuildVersion=&lt;版&gt;</c> でアセンブリに焼き込んだ版を読む。
    /// Unity のサーバーが版をビルドに持つ (Application.version) のと同じく、引数なしで起動しても版が分かるようにする。
    /// </summary>
    internal static class ServerBuildVersion
    {
        public const string MetadataKey = "FpsBuildVersion";

        public static string Embedded()
        {
            return Find(typeof(ServerBuildVersion).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>());
        }

        /// <summary>焼き込んだ版。無いか空なら null。検証と正規化は <see cref="ServerLaunch"/> が引数の版と同じ規則で行う。</summary>
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

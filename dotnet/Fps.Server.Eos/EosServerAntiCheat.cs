using System;
using System.IO;
using Fps.Core;
using Fps.EosSdk;

namespace Fps.Server.Eos
{
    /// <summary>SDK の無いビルドでも呼べる入り口。SDK の型は <c>#if EOS_SDK</c> の中にだけ置く。</summary>
    internal static class EosServerAntiCheat
    {
        /// <summary>
        /// プラットフォームはまだ作らず、モジュールの起動で作る。起動の失敗をサーバーの起動の失敗として 1 か所で知らせるため。
        /// </summary>
        public static bool TryPrepare(
            EosCredentials credentials,
            string version,
            string libraryDirectory,
            out Func<TextWriter, IAntiCheatServerBackend> createBackend,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            ArgumentNullException.ThrowIfNull(libraryDirectory);

            createBackend = null;
            if (!EosSdkBuild.Included)
            {
                error = "the EOS C# SDK is not in this build; place it in dotnet/ThirdParty/EOSSDK and rebuild (dotnet/ThirdParty/EOSSDK/README.md)";
                return false;
            }

            if (!EosSdkBuild.HasNativeLibrary(libraryDirectory))
            {
                error = "the EOS native library " + EosSdkBuild.NativeLibraryFile + " is not next to the server; place it in dotnet/ThirdParty/EOSSDK/Bin/<rid> and rebuild";
                return false;
            }

#if EOS_SDK
            createBackend = log => new EosAntiCheatServerBackend(credentials, version, log);
            error = null;
            return true;
#else
            error = "the EOS C# SDK is not in this build";
            return false;
#endif
        }
    }
}

using System;
using System.IO;
using Shooter.Core;
using Shooter.EosSdk;

namespace Shooter.Server.Eos
{
    // SDK の無いビルドでも呼べるよう、SDK の型は #if EOS_SDK の中にだけ置く。
    internal static class EosServerLobby
    {
        // プラットフォームはまだ作らず最初のログインで作るのは、失敗をログインの失敗としてやり直せるようにするため。
        public static bool TryPrepare(
            EosCredentials credentials,
            string version,
            string libraryDirectory,
            out Func<TextWriter, IServerLobbyBackend> createBackend,
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
            createBackend = log => new EosLobbyBackend(credentials, version, ServerLobby.DisplayName, log);
            error = null;
            return true;
#else
            error = "the EOS C# SDK is not in this build";
            return false;
#endif
        }
    }
}

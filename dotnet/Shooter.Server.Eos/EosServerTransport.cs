using System;
using System.IO;
using Shooter.Core;
using Shooter.EosSdk;
using Shooter.Protocol;

namespace Shooter.Server.Eos
{
    // SDK の無いビルドでも呼べるよう、SDK の型は #if EOS_SDK の中にだけ置く。
    internal static class EosServerTransport
    {
        public static bool TryCreate(
            EosCredentials credentials,
            string version,
            string libraryDirectory,
            TextWriter log,
            out IMatchTransport transport,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            ArgumentNullException.ThrowIfNull(libraryDirectory);
            ArgumentNullException.ThrowIfNull(log);

            transport = null;
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
            transport = new EosMatchTransport(new EosServerP2P(credentials, version, log), new EosTransportOptions(), log.WriteLine);
            error = null;
            return true;
#else
            error = "the EOS C# SDK is not in this build";
            return false;
#endif
        }
    }
}

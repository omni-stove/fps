using System;
using System.IO;
using Shooter.Core;
using Shooter.EosSdk;
using Shooter.Protocol;

namespace Shooter.Server.Eos
{
    /// <summary><c>-transport eos</c> の通信路の、SDK の無いビルドでも呼べる入り口。SDK の型は <c>#if EOS_SDK</c> の中にだけ置く。</summary>
    internal static class EosServerTransport
    {
        /// <param name="credentials">EOS の認証情報。</param>
        /// <param name="version">サーバーの版。</param>
        /// <param name="libraryDirectory">ネイティブのライブラリを探すフォルダ (実行ファイルの隣)。</param>
        /// <param name="log">ログの出力先。</param>
        /// <param name="transport">作った通信路。待ち受けたときにログインする。</param>
        /// <param name="error">作れないときの理由。</param>
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

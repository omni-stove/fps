using System;
using System.IO;
using Fps.Core;
using Fps.EosSdk;

namespace Fps.Server.Eos
{
    /// <summary>SDK の無いビルドでも呼べる入り口。SDK の型は <c>#if EOS_SDK</c> の中にだけ置く。</summary>
    internal static class EosServerLobby
    {
        /// <summary>
        /// SDK とネイティブのライブラリが揃っていれば、ロビーのバックエンドを作る関数を返す。
        /// プラットフォームはまだ作らず、最初のログインで作る (失敗をログインの失敗としてやり直せるように)。
        /// </summary>
        /// <param name="credentials">EOS の認証情報。</param>
        /// <param name="version">サーバーの版。</param>
        /// <param name="libraryDirectory">ネイティブのライブラリを探すフォルダ (実行ファイルの隣)。</param>
        /// <param name="createBackend">ログの出力先からバックエンドを作る関数。</param>
        /// <param name="error">作れないときの理由。</param>
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

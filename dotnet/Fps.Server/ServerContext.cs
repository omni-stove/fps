using System;
using System.IO;
using Fps.Core;

namespace Fps.Server
{
    /// <summary><see cref="IServerModule.Start"/> に渡す、サーバー全体で共有する起動時の情報。</summary>
    internal sealed class ServerContext
    {
        public ServerContext(NetLaunchOptions options, string version, TextWriter log)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Version = version;
            Log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>コマンドライン引数から読んだ起動設定。</summary>
        public NetLaunchOptions Options { get; }

        /// <summary>サーバーの版。<c>-buildVersion</c> が無ければ null。</summary>
        public string Version { get; }

        /// <summary>ログの出力先。</summary>
        public TextWriter Log { get; }
    }
}

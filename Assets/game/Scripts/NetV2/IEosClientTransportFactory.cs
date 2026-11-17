using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    /// <summary>
    /// クライアントが EOS の通信路とクイックマッチのロビーの検索を作る口。Shooter.NetV2 が PlayEveryWare のプラグインに依存しないよう、
    /// プラグインを使う Shooter.NetEos が実装して <see cref="NetV2Eos.Factory"/> に入れる。
    /// </summary>
    public interface IEosClientTransportFactory
    {
        /// <summary>認証情報を読む。欠けていれば credentials を null にして true、ファイルが壊れていれば false。</summary>
        bool TryLoadCredentials(out EosCredentials credentials, out IReadOnlyList<string> missing, out string error);

        /// <summary>まだログインしない通信路を作る。Connect でログインし、サーバーの ProductUserId へつなぐ。</summary>
        IMatchTransport Create(EosCredentials credentials, Action<string> log);

        /// <summary>まだログインしないロビーのサービスを作る。クイックマッチ (<see cref="QuickMatchSearch"/>) が専用サーバーのロビーを探すのに使う。</summary>
        ILobbyBackend CreateLobbyBackend(EosCredentials credentials);
    }
}

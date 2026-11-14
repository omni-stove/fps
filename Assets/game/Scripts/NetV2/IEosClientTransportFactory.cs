using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Protocol;

namespace Fps.NetV2
{
    /// <summary>
    /// <c>-netstack v2 -transport eos</c> のクライアントが EOS の通信路を作る口。Fps.NetV2 が PlayEveryWare のプラグインに依存しないよう、
    /// プラグインを使う Fps.NetEos が実装して <see cref="NetV2Eos.Factory"/> に入れる。
    /// </summary>
    public interface IEosClientTransportFactory
    {
        /// <summary>認証情報を読む。欠けていれば credentials を null にして true、ファイルが壊れていれば false。</summary>
        bool TryLoadCredentials(out EosCredentials credentials, out IReadOnlyList<string> missing, out string error);

        /// <summary>まだログインしない通信路を作る。Connect でログインし、サーバーの ProductUserId へつなぐ。</summary>
        IMatchTransport Create(EosCredentials credentials, Action<string> log);
    }
}

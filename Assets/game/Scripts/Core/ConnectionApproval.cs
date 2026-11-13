using System;

namespace Fps.Core
{
    /// <summary>
    /// サーバーが接続の承認データ (<see cref="ConnectionPayload"/>) を見て、クライアントを入れるか決める。
    /// 版がサーバーと同じなら入れ、違えば <see cref="DisconnectNotice.VersionMismatchReason"/>、読めなければ
    /// <see cref="DisconnectNotice.RejectedReason"/> (<see cref="InvalidPayloadDetail"/>) を理由に断る。
    /// </summary>
    /// <remarks>
    /// 版が違うと送り合うデータの形が合う保証が無く、試合の途中で壊れるより入る前に断るほうがよい。
    /// 版を載せない古いビルドは承認データが読めないので、版を比べられないまま断る。
    /// </remarks>
    public static class ConnectionApproval
    {
        /// <summary>承認データが読めなかったときに送る、断った訳の符号。</summary>
        public const string InvalidPayloadDetail = "invalid-payload";

        /// <param name="serverVersion">サーバーの版。正規化済みであること (<see cref="ConnectionPayload.IsNormalizedVersion"/>)。</param>
        /// <param name="payload">クライアントが送った承認データ。null も受ける。</param>
        /// <exception cref="ArgumentException">serverVersion が正規化済みの版でない。</exception>
        public static ConnectionVerdict Judge(string serverVersion, byte[] payload)
        {
            if (!ConnectionPayload.IsNormalizedVersion(serverVersion))
            {
                throw new ArgumentException("server version '" + serverVersion + "' is not a normalized build version", nameof(serverVersion));
            }

            if (!ConnectionPayload.TryDecode(payload, out var clientVersion, out var key))
            {
                var length = payload == null ? 0 : payload.Length;
                return new ConnectionVerdict(
                    false, null, null, DisconnectNotice.RejectedReason(InvalidPayloadDetail),
                    "unreadable connection payload (" + length + " bytes; expected <version>/<rejoin key>)");
            }

            if (!string.Equals(clientVersion, serverVersion, StringComparison.Ordinal))
            {
                return new ConnectionVerdict(
                    false, clientVersion, null, DisconnectNotice.VersionMismatchReason(serverVersion),
                    "version mismatch (client " + clientVersion + ", server " + serverVersion + ")");
            }

            return new ConnectionVerdict(true, clientVersion, key, null, null);
        }
    }
}

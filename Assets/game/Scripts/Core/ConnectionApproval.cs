using System;

namespace Shooter.Core
{
    /// <summary>
    /// サーバーが接続の承認データ (<see cref="ConnectionPayload"/>) を見て、クライアントを入れるか決める。
    /// 版がサーバーと同じなら入れ、違えば <see cref="DisconnectNotice.VersionMismatchReason"/>、読めなければ
    /// <see cref="DisconnectNotice.RejectedReason"/> (<see cref="InvalidPayloadDetail"/>) を理由に断る。
    /// サーバーが止まる準備中なら、誰でも <see cref="DisconnectNotice.ServerDrainingDetail"/> を理由に断る。
    /// 非公開の試合では、参加コードが合わなければ <see cref="DisconnectNotice.JoinCodeMismatchDetail"/> を理由に断る。
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
            return Judge(serverVersion, payload, false);
        }

        /// <param name="serverVersion">サーバーの版。正規化済みであること (<see cref="ConnectionPayload.IsNormalizedVersion"/>)。</param>
        /// <param name="payload">クライアントが送った承認データ。null も受ける。</param>
        /// <param name="draining">
        /// サーバーが止まる準備中 (<see cref="ServerDrain.IsDraining"/>) なら true。承認データによらず
        /// <see cref="DisconnectNotice.ServerDrainingDetail"/> を理由に断る。
        /// </param>
        /// <exception cref="ArgumentException">serverVersion が正規化済みの版でない。</exception>
        public static ConnectionVerdict Judge(string serverVersion, byte[] payload, bool draining)
        {
            return Judge(serverVersion, payload, draining, NetMatchSettings.Default);
        }

        /// <param name="serverVersion">サーバーの版。正規化済みであること (<see cref="ConnectionPayload.IsNormalizedVersion"/>)。</param>
        /// <param name="payload">クライアントが送った承認データ。null も受ける。</param>
        /// <param name="draining">サーバーが止まる準備中 (<see cref="ServerDrain.IsDraining"/>) なら true。</param>
        /// <param name="match">
        /// サーバーの試合の設定。非公開 (<see cref="NetMatchSettings.PrivateMatch"/>) なら、版が合っていても承認データの参加コード
        /// (<see cref="ConnectionExtras.JoinCode"/>) が <see cref="NetMatchSettings.JoinCode"/> と揃えて同じ (<see cref="JoinCode.Matches"/>) でなければ
        /// <see cref="DisconnectNotice.JoinCodeMismatchDetail"/> を理由に断る。試合にコードが無ければ誰も入れない。
        /// </param>
        /// <exception cref="ArgumentNullException">match が null。</exception>
        /// <exception cref="ArgumentException">serverVersion が正規化済みの版でない。</exception>
        public static ConnectionVerdict Judge(string serverVersion, byte[] payload, bool draining, NetMatchSettings match)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            if (!ConnectionPayload.IsNormalizedVersion(serverVersion))
            {
                throw new ArgumentException("server version '" + serverVersion + "' is not a normalized build version", nameof(serverVersion));
            }

            // 版の違う相手も、版を合わせて来直したところで入れないので、止まる準備中であることを先に伝える
            if (draining)
            {
                var drainingClientVersion = ConnectionPayload.TryDecode(payload, out var decoded, out _) ? decoded : null;
                return new ConnectionVerdict(
                    false, drainingClientVersion, null, DisconnectNotice.RejectedReason(DisconnectNotice.ServerDrainingDetail),
                    "the server is draining before it stops");
            }

            if (!ConnectionPayload.TryDecode(payload, out var clientVersion, out var key, out var extras))
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

            // 版の違う相手はコードが合っても入れないので、コードは版を比べた後に見る。ログにはコードそのものを残さない
            if (match.PrivateMatch && !JoinCode.Matches(match.JoinCode, extras.JoinCode))
            {
                return new ConnectionVerdict(
                    false, clientVersion, null, DisconnectNotice.RejectedReason(DisconnectNotice.JoinCodeMismatchDetail),
                    match.JoinCode == null
                        ? "the private match has no join code to let anyone in"
                        : extras.JoinCode == null ? "no join code for the private match" : "wrong join code for the private match");
            }

            return new ConnectionVerdict(true, clientVersion, key, null, null);
        }
    }
}

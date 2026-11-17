using System;

namespace Shooter.Core
{
    // 版が違うと送り合うデータの形が合う保証が無いので、試合の途中で壊れるより入る前に断る。
    public static class ConnectionApproval
    {
        public const string InvalidPayloadDetail = "invalid-payload";

        public static ConnectionVerdict Judge(string serverVersion, byte[] payload)
        {
            return Judge(serverVersion, payload, false);
        }

        public static ConnectionVerdict Judge(string serverVersion, byte[] payload, bool draining)
        {
            return Judge(serverVersion, payload, draining, NetMatchSettings.Default);
        }

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

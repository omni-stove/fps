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

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public static class LobbySelector
    {
        public static LobbyInfo PickServer(IReadOnlyList<LobbyInfo> lobbies, string version)
        {
            return PickServer(lobbies, version, 1);
        }

        public static LobbyInfo PickServer(IReadOnlyList<LobbyInfo> lobbies, string version, int partySize)
        {
            if (lobbies == null)
            {
                throw new ArgumentNullException(nameof(lobbies));
            }

            ThrowIfInvalidPartySize(partySize);
            LobbyInfo best = null;
            long bestPlayers = 0;
            foreach (var lobby in lobbies)
            {
                if (lobby == null || ServerRejection(lobby, version, partySize) != null)
                {
                    continue;
                }

                lobby.TryGetNumber(ServerLobby.PlayersKey, out var players);
                if (best == null
                    || players > bestPlayers
                    || (players == bestPlayers && string.CompareOrdinal(lobby.Id, best.Id) < 0))
                {
                    best = lobby;
                    bestPlayers = players;
                }
            }

            return best;
        }

        public static string ServerRejection(LobbyInfo lobby, string version)
        {
            return ServerRejection(lobby, version, 1);
        }

        public static string ServerRejection(LobbyInfo lobby, string version, int partySize)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            ThrowIfInvalidPartySize(partySize);

            if (lobby.IsPrivate)
            {
                return "private";
            }

            return Rejection(lobby, version, partySize, out _);
        }

        // 非公開のサーバーはコードを知る人のためのものなので、Quick Match と違って private では弾かない。
        public static JoinCodeOutcome PickByJoinCode(
            IReadOnlyList<LobbyInfo> lobbies, string code, string version, int partySize, out LobbyInfo picked)
        {
            if (lobbies == null)
            {
                throw new ArgumentNullException(nameof(lobbies));
            }

            ThrowIfInvalidPartySize(partySize);
            picked = null;
            var full = false;
            var otherVersion = false;
            var broken = false;
            foreach (var lobby in lobbies)
            {
                if (lobby == null || !JoinCode.Matches(lobby.JoinCode, code))
                {
                    continue;
                }

                if (Rejection(lobby, version, partySize, out var kind) == null)
                {
                    // コードは試合ごとに作るので重なるのはまれだが、重なっても検索の返す順によらず同じサーバーを選ぶ
                    if (picked == null || string.CompareOrdinal(lobby.Id, picked.Id) < 0)
                    {
                        picked = lobby;
                    }

                    continue;
                }

                full |= kind == RejectionKind.Full;
                otherVersion |= kind == RejectionKind.Version;
                broken |= kind == RejectionKind.Broken;
            }

            if (picked != null)
            {
                return JoinCodeOutcome.Joined;
            }

            if (full)
            {
                return JoinCodeOutcome.Full;
            }

            if (otherVersion)
            {
                return JoinCodeOutcome.VersionMismatch;
            }

            return broken ? JoinCodeOutcome.Failed : JoinCodeOutcome.NotFound;
        }

        public static string DescribeNoServer(IReadOnlyList<LobbyInfo> lobbies, string version)
        {
            return DescribeNoServer(lobbies, version, 1);
        }

        public static string DescribeNoServer(IReadOnlyList<LobbyInfo> lobbies, string version, int partySize)
        {
            if (lobbies == null)
            {
                throw new ArgumentNullException(nameof(lobbies));
            }

            ThrowIfInvalidPartySize(partySize);

            var reasons = new List<string>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var found = 0;
            foreach (var lobby in lobbies)
            {
                if (lobby == null)
                {
                    continue;
                }

                found++;
                var reason = ServerRejection(lobby, version, partySize);
                if (reason == null)
                {
                    continue;
                }

                if (counts.TryGetValue(reason, out var count))
                {
                    counts[reason] = count + 1;
                }
                else
                {
                    reasons.Add(reason);
                    counts.Add(reason, 1);
                }
            }

            if (found == 0)
            {
                return "no server lobby found";
            }

            var parts = new List<string>(reasons.Count);
            foreach (var reason in reasons)
            {
                parts.Add(reason + " x" + counts[reason].ToString(CultureInfo.InvariantCulture));
            }

            return "no server to join among " + found.ToString(CultureInfo.InvariantCulture)
                + " found (" + string.Join(", ", parts) + ")";
        }

        private static string Rejection(LobbyInfo lobby, string version, int partySize, out RejectionKind kind)
        {
            // バケットにも版は入っているが、切り詰めた版が衝突したときや古いサーバーの残りを、属性でも弾く
            var expected = LobbyLaunchOptions.BucketVersion(version);
            if (!lobby.TryGetText(ServerLobby.VersionKey, out var serverVersion))
            {
                kind = RejectionKind.Broken;
                return "no version";
            }

            if (!string.Equals(serverVersion, expected, StringComparison.Ordinal))
            {
                kind = RejectionKind.Version;
                return "version " + serverVersion + " (this build is " + expected + ")";
            }

            if (!lobby.TryGetText(ServerLobby.ServerKey, out var endpoint) || !NetLaunchOptions.TryParseEndpoint(endpoint, out _, out _))
            {
                kind = RejectionKind.Broken;
                return "no server address";
            }

            if (!lobby.TryGetNumber(ServerLobby.PlayersKey, out var players) || players < 0)
            {
                kind = RejectionKind.Broken;
                return "no player count";
            }

            var free = ServerLobby.PlayerSlots(lobby) - players;
            if (free <= 0)
            {
                kind = RejectionKind.Full;
                return "full";
            }

            if (free < partySize)
            {
                kind = RejectionKind.Full;
                return "room for " + free.ToString(CultureInfo.InvariantCulture)
                    + " (party of " + partySize.ToString(CultureInfo.InvariantCulture) + ")";
            }

            kind = RejectionKind.None;
            return null;
        }

        private static void ThrowIfInvalidPartySize(int partySize)
        {
            if (partySize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(partySize), partySize, "must be at least 1");
            }
        }

        private enum RejectionKind
        {
            None,
            Version,
            Broken,
            Full,
        }
    }
}

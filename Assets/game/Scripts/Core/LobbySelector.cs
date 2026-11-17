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

            // バケットにも版は入っているが、切り詰めた版が衝突したときや古いサーバーの残りを、属性でも弾く
            var expected = LobbyLaunchOptions.BucketVersion(version);
            if (!lobby.TryGetText(ServerLobby.VersionKey, out var serverVersion))
            {
                return "no version";
            }

            if (!string.Equals(serverVersion, expected, StringComparison.Ordinal))
            {
                return "version " + serverVersion + " (this build is " + expected + ")";
            }

            if (!lobby.TryGetText(ServerLobby.ServerKey, out var endpoint) || !NetLaunchOptions.TryParseEndpoint(endpoint, out _, out _))
            {
                return "no server address";
            }

            if (!lobby.TryGetNumber(ServerLobby.PlayersKey, out var players) || players < 0)
            {
                return "no player count";
            }

            var free = ServerLobby.PlayerSlots(lobby) - players;
            if (free <= 0)
            {
                return "full";
            }

            if (free < partySize)
            {
                return "room for " + free.ToString(CultureInfo.InvariantCulture)
                    + " (party of " + partySize.ToString(CultureInfo.InvariantCulture) + ")";
            }

            return null;
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

        private static void ThrowIfInvalidPartySize(int partySize)
        {
            if (partySize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(partySize), partySize, "must be at least 1");
            }
        }
    }
}

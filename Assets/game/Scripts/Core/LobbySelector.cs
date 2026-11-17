using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    /// <summary>検索で見つかった専用サーバーのロビーから、つなぐものを選ぶ。</summary>
    public static class LobbySelector
    {
        /// <summary>
        /// 専用サーバーが <c>-register-lobby</c> で載せたロビー (<see cref="ServerLobby"/>) のうち、つなげるものの中で
        /// いちばん人数 (<see cref="ServerLobby.PlayersKey"/>) の多いもの (同じなら ID の順で先のもの) を返す。
        /// つなげるのは <see cref="ServerRejection"/> が null のもの。
        /// </summary>
        /// <param name="lobbies">検索結果。null の要素は無視する。</param>
        /// <param name="version">自分のビルドの版 (Application.version)。</param>
        /// <returns>つなぐサーバーのロビー。無ければ null。</returns>
        public static LobbyInfo PickServer(IReadOnlyList<LobbyInfo> lobbies, string version)
        {
            if (lobbies == null)
            {
                throw new ArgumentNullException(nameof(lobbies));
            }

            LobbyInfo best = null;
            long bestPlayers = 0;
            foreach (var lobby in lobbies)
            {
                if (lobby == null || ServerRejection(lobby, version) != null)
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

        /// <summary>
        /// サーバーのロビーにつなげない理由。つなげるなら null。
        /// 版 (<see cref="ServerLobby.VersionKey"/>) が自分と違う、人数 (<see cref="ServerLobby.PlayersKey"/>) が
        /// 入れるプレイヤーの数 (<see cref="ServerLobby.PlayerSlots"/>) に達している、アドレス (<see cref="ServerLobby.ServerKey"/>) が無いか IPv4:port でない、のどれか。
        /// </summary>
        /// <param name="lobby">検索で見つかったロビー。</param>
        /// <param name="version">自分のビルドの版 (Application.version)。空なら "0" として比べる。</param>
        public static string ServerRejection(LobbyInfo lobby, string version)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
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

            if (players >= ServerLobby.PlayerSlots(lobby))
            {
                return "full";
            }

            return null;
        }

        /// <summary>
        /// つなげるサーバーが無かったときのログ・案内向けの理由。<c>no server to join among 3 found (full x2, version 0.2 (this build is 0.3) x1)</c> の形。
        /// </summary>
        /// <param name="lobbies">検索結果。null の要素は無視する。</param>
        /// <param name="version">自分のビルドの版。</param>
        public static string DescribeNoServer(IReadOnlyList<LobbyInfo> lobbies, string version)
        {
            if (lobbies == null)
            {
                throw new ArgumentNullException(nameof(lobbies));
            }

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
                var reason = ServerRejection(lobby, version);
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
    }
}

using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>検索で見つかったロビーから、参加するものを選ぶ。</summary>
    public static class LobbySelector
    {
        /// <summary>
        /// 空きがあり、自分がオーナーでもメンバーでもないロビーのうち、いちばん人数の多いもの
        /// (同じなら ID の順で先のもの) を返す。人の集まっているロビーから埋めて、試合が早く始まるようにする。
        /// </summary>
        /// <param name="lobbies">検索結果。null の要素は無視する。</param>
        /// <param name="localUserId">自分の ProductUserId。</param>
        /// <returns>参加できるロビー。無ければ null。</returns>
        public static LobbyInfo PickJoinable(IReadOnlyList<LobbyInfo> lobbies, string localUserId)
        {
            if (lobbies == null)
            {
                throw new ArgumentNullException(nameof(lobbies));
            }

            if (string.IsNullOrWhiteSpace(localUserId))
            {
                throw new ArgumentException("must not be null or blank", nameof(localUserId));
            }

            LobbyInfo best = null;
            foreach (var lobby in lobbies)
            {
                // 自分のロビー (前回の起動の残りを含む) には、Host への接続が自分自身になるので入らない
                if (lobby == null || lobby.FreeSlots <= 0 || lobby.IsOwnedBy(localUserId) || lobby.HasMember(localUserId))
                {
                    continue;
                }

                if (best == null
                    || lobby.MemberCount > best.MemberCount
                    || (lobby.MemberCount == best.MemberCount && string.CompareOrdinal(lobby.Id, best.Id) < 0))
                {
                    best = lobby;
                }
            }

            return best;
        }
    }
}

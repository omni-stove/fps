using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// オンラインのキルフィードの引き先。Id はプレイヤー Id (人はクライアント Id、Bot は Bot の Id) なので、名前は <see cref="BotRoster.KillFeedName"/>、
    /// チームは試合の <see cref="NetTeamDeathmatch.Players"/>、武器の名前は試合のロードアウト (グレネードは <see cref="GrenadeThrower.Name"/>) から引き、
    /// 自分のクライアントの行を強調する。
    /// </summary>
    public sealed class NetKillFeedNames : IKillFeedNames
    {
        private readonly Func<int, Team?> _teamOf;
        private readonly Func<string, LocalizedText> _weaponNameOf;
        private readonly int? _localPlayerId;

        /// <param name="teamOf">プレイヤー Id のチーム。分からなければ null。null なら誰のチームも分からない。</param>
        /// <param name="weaponNameOf">武器の Id の名前。分からなければ <c>default</c>。null なら武器名を出さない。</param>
        /// <param name="localPlayerId">この画面のプレイヤーの Id。無ければ null (どの行も強調しない)。</param>
        public NetKillFeedNames(Func<int, Team?> teamOf, Func<string, LocalizedText> weaponNameOf, int? localPlayerId)
        {
            _teamOf = teamOf;
            _weaponNameOf = weaponNameOf;
            _localPlayerId = localPlayerId;
        }

        /// <summary>
        /// match の参加者とロードアウトから引く。match が null なら、チームも武器名も分からないまま名前と強調だけを出す。
        /// ロードアウトが不正なら警告を 1 度出して武器名を出さない (試合そのものはサーバーが止める)。
        /// </summary>
        public static NetKillFeedNames For(NetTeamDeathmatch match, ulong localClientId)
        {
            int? localId = localClientId <= int.MaxValue ? (int)localClientId : (int?)null;
            if (match == null)
            {
                return new NetKillFeedNames(null, WeaponNames(null, null), localId);
            }

            return new NetKillFeedNames(id => TeamOf(match, id), WeaponNames(match.EffectiveLoadout, match.EffectiveCatalog), localId);
        }

        public KillFeedLine Describe(KillFeedEntry entry)
        {
            var killer = NameOf(entry.KillerId);
            var victim = entry.IsSuicide ? killer : NameOf(entry.VictimId);
            var weapon = entry.WeaponId.Length > 0 && _weaponNameOf != null ? _weaponNameOf(entry.WeaponId) : default;
            return new KillFeedLine(entry, killer, victim, weapon);
        }

        private KillFeedName NameOf(int playerId)
        {
            var team = _teamOf != null ? _teamOf(playerId) : null;
            return new KillFeedName(BotRoster.KillFeedName(playerId), team, playerId == _localPlayerId);
        }

        // 抜けた人は行が消えているのでチームが分からない (名前は Id のまま出す)
        private static Team? TeamOf(NetTeamDeathmatch match, int playerId)
        {
            if (match == null || playerId < 0 || match.Players == null)
            {
                return null;
            }

            var players = match.Players;
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].ClientId == (ulong)playerId)
                {
                    return players[i].Team;
                }
            }

            return null;
        }

        /// <summary>
        /// 武器の Id の名前 (<see cref="WeaponSpec.Name"/>) を引く関数。グレネード (<see cref="GrenadeThrower.WeaponId"/>) はロードアウトに無いので先に <see cref="GrenadeThrower.Name"/> にする。
        /// プレイヤーはカタログの武器も選べるので、クライアントの射撃と同じく Loadout とカタログの両方から引く。どちらも無ければグレネード以外は <c>default</c>。
        /// </summary>
        internal static Func<string, LocalizedText> WeaponNames(LoadoutDefinition loadout, WeaponCatalogAsset catalog)
        {
            var names = new Dictionary<string, LocalizedText>(StringComparer.Ordinal);
            var warned = false;
            return id =>
            {
                if (string.Equals(id, GrenadeThrower.WeaponId, StringComparison.Ordinal))
                {
                    return GrenadeThrower.Name;
                }

                if (loadout == null && catalog == null)
                {
                    return default;
                }

                if (names.TryGetValue(id, out var cached))
                {
                    return cached;
                }

                if (NetTeamDeathmatch.TryFindWeaponSpec(loadout, catalog, id, out var spec, out var error))
                {
                    names[id] = spec.Name;
                    return spec.Name;
                }

                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning(KillFeedView.LogTag + " cannot read weapon name '" + id + "': " + error);
                }

                return default;
            };
        }
    }
}

using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、接続したプレイヤーを試合に加える (チームは Session が交互に決める)・抜けたプレイヤーを外す処理と、
    /// 参加者と同期する一覧の引き方。
    /// </summary>
    public sealed partial class NetTeamDeathmatch
    {
        /// <summary>プレイヤーの NetworkObject が消えるとき (切断を含む) にサーバーで呼ぶ。</summary>
        internal void ServerLeave(NetPlayer player)
        {
            if (!IsServer || player == null || !IsJoined(player))
            {
                return;
            }

            var playerId = player.ServerPlayerId;
            player.ServerPlayerId = NetPlayer.NotJoined;
            _playerById.Remove(playerId);
            _lagCompensator?.Remove(playerId);
            // 連射の制限と一緒に選んだ並びも捨てる。同じクライアント Id で入り直したら既定の並びから
            _serverLoadout?.Remove(playerId);
            _lastKillers.Remove(playerId);
            ForgetDowned(playerId);
            ForgetGrenades(playerId);
            if (_session != null && _session.HasPlayer(playerId))
            {
                _session.Leave(playerId);
            }

            Debug.Log(MatchLog.Left(playerId));

            // 停止中は配る先がもう無いので、同期する一覧には触らない
            if (NetworkManager != null && NetworkManager.ShutdownInProgress)
            {
                return;
            }

            var index = FindRow(player.OwnerClientId);
            if (index >= 0)
            {
                _players.RemoveAt(index);
            }

            RemoveSpawnProtectionRow(player.OwnerClientId);
        }

        /// <summary>まだ試合に入っていない接続済みプレイヤーを、接続順に加える。</summary>
        private void JoinNewPlayers()
        {
            var clients = NetworkManager.ConnectedClientsList;
            for (var i = 0; i < clients.Count; i++)
            {
                var client = clients[i];
                var playerObject = client.PlayerObject;
                if (playerObject == null || !playerObject.IsSpawned || _rejectedClients.Contains(client.ClientId))
                {
                    continue;
                }

                var player = playerObject.GetComponent<NetPlayer>();
                if (player == null)
                {
                    _rejectedClients.Add(client.ClientId);
                    Debug.LogError("[Match] player object of client " + client.ClientId + " has no NetPlayer; run Fps.Editor.NetSceneBuilder.Build");
                    continue;
                }

                if (IsJoined(player))
                {
                    continue;
                }

                if (player.Weapon == null)
                {
                    // 弾数はサーバーが NetPlayerWeapon で持つので、無いと撃てない。古いプレハブのまま起動したとき
                    _rejectedClients.Add(client.ClientId);
                    Debug.LogError("[Match] player object of client " + client.ClientId + " has no NetPlayerWeapon; run Fps.Editor.NetSceneBuilder.Build");
                    continue;
                }

                if (client.ClientId > int.MaxValue)
                {
                    _rejectedClients.Add(client.ClientId);
                    Debug.LogError("[Match] client id " + client.ClientId + " does not fit a player id");
                    continue;
                }

                var playerId = (int)client.ClientId;
                if (_session.HasPlayer(playerId))
                {
                    // 同じクライアントの前のプレイヤーオブジェクトが抜け損ねている。二重に数えないよう加えない
                    _rejectedClients.Add(client.ClientId);
                    Debug.LogError("[Match] client " + client.ClientId + " is already in the match with another player object");
                    continue;
                }

                player.ServerPlayerId = playerId;
                _playerById[playerId] = player;

                // 武器は Join の中の出現 (HandleSpawned) で持たせる
                Debug.Log(MatchLog.Joined(playerId, _session.NextTeam));
                _session.Join(playerId);
            }
        }

        private bool IsJoined(NetPlayer player)
        {
            return player.ServerPlayerId != NetPlayer.NotJoined
                && _playerById.TryGetValue(player.ServerPlayerId, out var joined)
                && joined == player;
        }

        private int FindRow(ulong clientId)
        {
            for (var i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == clientId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

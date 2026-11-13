using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、接続したプレイヤーを試合に加える・抜けたプレイヤーを外す処理と、
    /// 参加者と同期する一覧の引き方。
    /// </summary>
    /// <remarks>
    /// チームは Session が人数の少ないほう (同じなら点の低いほう) に決める。接続の承認データで届いた鍵 (<see cref="NetConnectionKeys"/>:
    /// EOS では ProductUserId、UDP では起動時に作る端末の Id) を付けて加えるので、切断した人が
    /// <see cref="ReconnectGraceSeconds"/> 秒のうちに同じ鍵でつなぎ直せば、同じチームで前のキル数・デス数のまま戻る。
    /// 前の接続が切れたとサーバーが気づく前につなぎ直してきたときは、前のプレイヤーが抜けるまで加えずに待つ。
    /// </remarks>
    public sealed partial class NetTeamDeathmatch
    {
        // 同じ鍵の前の接続が抜けるのを待っているクライアント。待っている間に何度も警告を出さないため
        private readonly HashSet<ulong> _waitingForKey = new HashSet<ulong>();

        [SerializeField, Min(0f), Tooltip("切断した人のチーム・キル数・デス数を残す秒数。この間に同じ鍵でつなぎ直せば引き継ぐ。0 なら残さない")]
        private float _reconnectGraceSeconds = TeamDeathmatchSession.DefaultReconnectGraceSeconds;

        /// <summary>切断した人の成績を残す秒数。0 以上の有限値。試合の開始時 (Spawn 時) に読む。</summary>
        public float ReconnectGraceSeconds
        {
            get => _reconnectGraceSeconds;
            set => _reconnectGraceSeconds = value;
        }

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
            var held = _session != null && _session.HasPlayer(playerId) && _session.Leave(playerId);

            Debug.Log(MatchLog.Left(playerId));
            if (held)
            {
                Debug.Log(MatchLog.Held(playerId, _session.ReconnectGraceSeconds));
            }

            ServerEventsPlayerLeft(playerId);
            BotsPlayerLeft(playerId);
            IdlePlayerLeft(playerId);
            ObjectivePlayerLeft(playerId);

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
            NetConnectionKeys keys = null;
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

                // 承認を通さずに組んだ NetworkManager (テストなど) では鍵が無く、抜けても成績を残さない
                if (keys == null)
                {
                    NetworkManager.TryGetComponent(out keys);
                }

                var key = keys != null && keys.TryGet(client.ClientId, out var found) ? found : null;
                if (_session.IsKeyInUse(key))
                {
                    if (_waitingForKey.Add(client.ClientId))
                    {
                        Debug.LogWarning("[Match] client " + client.ClientId
                            + " reconnected before its previous connection left; waiting for that player to leave");
                    }

                    continue;
                }

                _waitingForKey.Remove(client.ClientId);
                player.ServerPlayerId = playerId;
                _playerById[playerId] = player;

                // 武器は Join の中の出現 (HandleSpawned) で持たせる
                var held = _session.FindHeld(key);
                var team = held.HasValue ? held.Value.Team : _session.NextTeam;
                Debug.Log(MatchLog.Joined(playerId, team));
                if (held.HasValue)
                {
                    Debug.Log(MatchLog.Rejoined(playerId, held.Value));
                }

                ServerEventsPlayerJoined(playerId, key, team, held.HasValue);
                _session.Join(playerId, key);
                BotsPlayerJoined(playerId, player);
                IdlePlayerJoined(playerId, player);
                ObjectivePlayerJoined(playerId, player);
            }
        }

        private void HandleHeldExpired(HeldPlayer held)
        {
            Debug.Log(MatchLog.HoldExpired(held.PlayerId));
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

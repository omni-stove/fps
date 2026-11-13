using System;
using Fps.Core;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetPlayer"/> のうち、サーバーが動かす Bot (クライアントを持たないプレイヤー) かどうかと、成績の行で使う Id。
    /// </summary>
    /// <remarks>
    /// Bot はサーバーが持ち主のまま Spawn するので、<see cref="NetworkBehaviour.IsOwner"/> はサーバー (ホストを含む) で true になる。
    /// 視点・HUD・入力の読み取りは <see cref="IsLocallyControlled"/> で人の持ち主にだけ付ける。
    /// Bot の印は Spawn の前にサーバーが <see cref="ServerMarkBot"/> で付け、Spawn の時点で同期値に書いて全員に配る。
    /// </remarks>
    public sealed partial class NetPlayer
    {
        // Bot のプレイヤー Id。人なら NotJoined。Spawn の時点で全員に届くよう、サーバーが OnNetworkSpawn で書く
        private readonly NetworkVariable<int> _botId = new NetworkVariable<int>(NotJoined);

        // Spawn 前に付けた Bot の印。Spawn 前に書いた同期値が Spawn で配られるかに頼らないよう、OnNetworkSpawn まで持っておく。
        // 同じオブジェクトの他の部品 (NetworkPlayerMotor) の OnNetworkSpawn が先に走っても IsBot が読めるよう、同期値とは別に持つ
        private int _pendingBotId = NotJoined;

        /// <summary>サーバーが動かす Bot か。</summary>
        public bool IsBot => _pendingBotId != NotJoined || _botId.Value != NotJoined;

        /// <summary>
        /// 試合の成績の行 (<see cref="NetTeamDeathmatch.Players"/> の <see cref="NetPlayerStats.ClientId"/>) と出現直後の無敵の行で、
        /// このプレイヤーを指す Id。人ならクライアント Id、Bot なら Bot のプレイヤー Id (<see cref="BotRoster.FirstBotPlayerId"/> 以上)。
        /// どちらもサーバーのプレイヤー Id と同じ値。
        /// </summary>
        public ulong RosterId
        {
            get
            {
                var botId = _pendingBotId != NotJoined ? _pendingBotId : _botId.Value;
                return botId != NotJoined ? (ulong)botId : OwnerClientId;
            }
        }

        /// <summary>このプロセスで人が操作しているプレイヤーか。持ち主で、Bot でなければ true。</summary>
        public bool IsLocallyControlled => IsOwner && !IsBot;

        /// <summary>
        /// サーバーで、Spawn する前に Bot の印を付ける。playerId は <see cref="BotRoster.IsBotId(int)"/> を満たす Bot のプレイヤー Id。
        /// </summary>
        /// <exception cref="InvalidOperationException">もう Spawn している。</exception>
        /// <exception cref="ArgumentOutOfRangeException">playerId が Bot の Id でない。</exception>
        internal void ServerMarkBot(int playerId)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("a bot must be marked before " + name + " is spawned");
            }

            if (!BotRoster.IsBotId(playerId))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "not a bot player id");
            }

            _pendingBotId = playerId;
        }

        /// <summary>
        /// サーバーで、次に受け入れる弾の発射番号と訂正の世代。Bot は予測しないので、この番号を添えて撃てば訂正が起きない。
        /// </summary>
        internal void ServerNextShotNumber(out int index, out int generation)
        {
            RequireServer();
            index = _serverShots.NextIndex;
            generation = _serverShots.Generation;
        }

        // OnNetworkSpawn の最初に呼ぶ。視点・HUD を付けるかの判断より前に、クライアントへ配る値をそろえる
        private void SpawnBotMark()
        {
            if (IsServer && _pendingBotId != NotJoined && _botId.Value != _pendingBotId)
            {
                _botId.Value = _pendingBotId;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、入力が <see cref="GameRules.IdleKickSeconds"/> 秒無いプレイヤーをサーバーから切る処理。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>ルールの <see cref="GameRules.IdleKickSeconds"/> が 0 なら何もしない。</item>
    /// <item>サーバーが受け取った移動の入力 (<see cref="NetworkPlayerMotor.ServerInputReceived"/>) が前の入力から変わった
    /// (<see cref="NetworkPlayerMotor.IsActivity"/>) ときと、射撃が届いたときに、その人の時計を戻す (<see cref="IdleTracker.Touch"/>)。</item>
    /// <item>試合中で、生きていてダウンしていない間だけ数える。ダウン中・倒れている間・試合の合間は止める (<see cref="IsIdleCounted"/>)。</item>
    /// <item>残り <see cref="IdleTracker.WarningSeconds"/> 秒で持ち主に警告を送り (<see cref="NetPlayer.ServerSendIdleWarning"/>)、
    /// 入力があったか数えるのをやめたら消させ、0 になったら <see cref="DisconnectNotice.IdleKickedReason"/> を付けて切る。</item>
    /// <item>数えるのはリモートのクライアントが持つプレイヤーだけ。サーバー (ホスト) が持つプレイヤー、つまりホスト自身と
    /// サーバーだけで動く Bot は切れないので数えない。</item>
    /// </list>
    /// 差し込み口はどれもサーバーでだけ、試合が動いている (Session がある) 間に呼ぶ。
    /// </remarks>
    public sealed partial class NetTeamDeathmatch
    {
        // IdleKickSeconds が 0 のルールでは null
        private IdleTracker _idleTracker;
        private readonly Dictionary<int, IdleWatch> _idleWatches = new Dictionary<int, IdleWatch>();

        // 回している間に切断でプレイヤーが抜けても回し続けられるよう、Id を写してから回す
        private readonly List<int> _idleIds = new List<int>();

        /// <summary>サーバーで放置を数えている間の <see cref="IdleTracker"/>。ルールで切らない・試合が動いていなければ null。</summary>
        internal IdleTracker ServerIdleTracker => _idleTracker;

        /// <summary>サーバーで試合を始められた (Session を作った) ときに呼ぶ。<see cref="ServerRules"/> が読める。</summary>
        partial void IdleServerStarted();

        /// <summary>サーバーの毎フレーム、Session を進めた直後に呼ぶ。</summary>
        /// <param name="deltaSeconds">前のフレームからの秒数。</param>
        partial void IdleServerTick(float deltaSeconds);

        /// <summary>プレイヤーを試合に加えた直後に呼ぶ。</summary>
        /// <param name="playerId">加えたプレイヤーの Id (クライアント Id と同じ値)。</param>
        /// <param name="player">加えたプレイヤー。</param>
        partial void IdlePlayerJoined(int playerId, NetPlayer player);

        /// <summary>プレイヤーを試合から外した直後に呼ぶ (切断を含む)。</summary>
        /// <param name="playerId">外したプレイヤーの Id。</param>
        partial void IdlePlayerLeft(int playerId);

        /// <summary>試合に参加しているプレイヤーの射撃が届いたとき、検査の前に呼ぶ (捨てる射撃でも操作はしている)。</summary>
        /// <param name="playerId">撃ったプレイヤーの Id。</param>
        partial void IdlePlayerFired(int playerId);

        /// <summary>プレイヤーが倒されたときに呼ぶ。自滅なら killerId と victimId が同じ。</summary>
        partial void IdlePlayerKilled(int killerId, int victimId);

        /// <summary>試合が終わって次の試合のために点数などを戻した直後に呼ぶ。</summary>
        partial void IdleMatchRestarted();

        /// <summary>試合が終わったときに呼ぶ。</summary>
        /// <param name="winner">勝ったチーム (<c>ObjectiveDecideWinner</c> の後の値)。引き分けなら null。</param>
        partial void IdleMatchEnded(Team? winner);

        /// <summary>サーバーで試合を Despawn するとき、Session を手放す前に呼ぶ。</summary>
        partial void IdleServerStopped();

        /// <summary>
        /// playerId の放置の時間を数えるか。試合中 (<see cref="GameModeState.Playing"/>) で、参加していて、生きていてダウンしていなければ true。
        /// </summary>
        internal static bool IsIdleCounted(TeamDeathmatchSession session, int playerId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return session.State == GameModeState.Playing
                && session.HasPlayer(playerId)
                && session.IsAlive(playerId)
                && !session.IsDowned(playerId);
        }

        partial void IdleServerStarted()
        {
            var seconds = ServerRules.IdleKickSeconds;
            if (!(seconds > 0f))
            {
                Debug.Log("[Match] idle kick off");
                return;
            }

            _idleTracker = new IdleTracker(seconds);
            Debug.Log("[Match] idle kick seconds=" + seconds.ToString("0.##", CultureInfo.InvariantCulture)
                + " warning=" + _idleTracker.WarningSeconds.ToString("0.##", CultureInfo.InvariantCulture));
        }

        partial void IdleServerTick(float deltaSeconds)
        {
            if (_idleTracker == null || _idleWatches.Count == 0)
            {
                return;
            }

            _idleIds.Clear();
            _idleIds.AddRange(_idleWatches.Keys);
            foreach (var playerId in _idleIds)
            {
                if (!_idleWatches.TryGetValue(playerId, out var watch))
                {
                    continue;
                }

                ReportIdle(playerId, watch, _idleTracker.SetCounting(playerId, IsIdleCounted(_session, playerId)));
                ReportIdle(playerId, watch, _idleTracker.Advance(playerId, deltaSeconds));
            }
        }

        partial void IdlePlayerJoined(int playerId, NetPlayer player)
        {
            if (_idleTracker == null || player == null || player.OwnerClientId == NetworkManager.ServerClientId)
            {
                return;
            }

            var motor = player.GetComponent<NetworkPlayerMotor>();
            if (motor == null)
            {
                // 入力が見えないまま数えると、操作している人まで切ってしまう
                Debug.LogWarning("[Match] player " + playerId.ToString(CultureInfo.InvariantCulture)
                    + " has no NetworkPlayerMotor; it is never kicked for idling; run Fps.Editor.NetSceneBuilder.Build");
                return;
            }

            ForgetIdle(playerId);
            var watch = new IdleWatch(player, motor);
            watch.Handler = input => HandleIdleInput(playerId, watch, input);
            motor.ServerInputReceived += watch.Handler;
            _idleWatches.Add(playerId, watch);
            _idleTracker.Add(playerId);
        }

        partial void IdlePlayerLeft(int playerId)
        {
            ForgetIdle(playerId);
        }

        partial void IdlePlayerFired(int playerId)
        {
            TouchIdle(playerId);
        }

        partial void IdleServerStopped()
        {
            _idleIds.Clear();
            _idleIds.AddRange(_idleWatches.Keys);
            foreach (var playerId in _idleIds)
            {
                ForgetIdle(playerId);
            }

            _idleIds.Clear();
            _idleTracker = null;
        }

        private void HandleIdleInput(int playerId, IdleWatch watch, MotorInput input)
        {
            var previous = watch.LastInput;
            watch.LastInput = input;
            if (NetworkPlayerMotor.IsActivity(previous, input))
            {
                TouchIdle(playerId);
            }
        }

        private void TouchIdle(int playerId)
        {
            if (_idleTracker != null && _idleWatches.TryGetValue(playerId, out var watch))
            {
                ReportIdle(playerId, watch, _idleTracker.Touch(playerId));
            }
        }

        private void ForgetIdle(int playerId)
        {
            if (_idleWatches.TryGetValue(playerId, out var watch))
            {
                // 破棄済みのモーターでも C# のイベントは外せる
                if (!ReferenceEquals(watch.Motor, null))
                {
                    watch.Motor.ServerInputReceived -= watch.Handler;
                }

                _idleWatches.Remove(playerId);
            }

            _idleTracker?.Remove(playerId);
        }

        private void ReportIdle(int playerId, IdleWatch watch, IdleChange change)
        {
            var player = watch.Player;
            if (player == null || !player.IsSpawned)
            {
                return;
            }

            var id = playerId.ToString(CultureInfo.InvariantCulture);
            switch (change)
            {
                case IdleChange.Warned:
                    var remaining = _idleTracker.GetRemaining(playerId) ?? 0f;
                    Debug.Log("[Match] idle warning player=" + id + " seconds=" + remaining.ToString("0.##", CultureInfo.InvariantCulture));
                    player.ServerSendIdleWarning(remaining);
                    break;
                case IdleChange.WarningCleared:
                    Debug.Log("[Match] idle warning cleared player=" + id);
                    player.ServerClearIdleWarning();
                    break;
                case IdleChange.Kicked:
                    KickIdle(playerId, player);
                    break;
            }
        }

        private void KickIdle(int playerId, NetPlayer player)
        {
            var manager = NetworkManager;
            if (manager == null || !manager.IsListening || manager.ShutdownInProgress)
            {
                return;
            }

            Debug.Log("[Match] idle kick player=" + playerId.ToString(CultureInfo.InvariantCulture)
                + " client=" + player.OwnerClientId.ToString(CultureInfo.InvariantCulture)
                + " after=" + _idleTracker.KickSeconds.ToString("0.##", CultureInfo.InvariantCulture) + "s");

            // 切断でプレイヤーが Despawn され、ServerLeave から IdlePlayerLeft が呼ばれて数えるのをやめる
            manager.DisconnectClient(player.OwnerClientId, DisconnectNotice.IdleKickedReason);
        }

        private sealed class IdleWatch
        {
            public IdleWatch(NetPlayer player, NetworkPlayerMotor motor)
            {
                Player = player;
                Motor = motor;
            }

            public NetPlayer Player { get; }

            public NetworkPlayerMotor Motor { get; }

            public Action<MotorInput> Handler { get; set; }

            public MotorInput LastInput { get; set; }
        }
    }
}

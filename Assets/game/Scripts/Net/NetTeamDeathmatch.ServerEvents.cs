using System;
using System.Collections.Generic;
using Fps.Core;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、試合のイベント (開始・参加・離脱・キル・終了・印) を <see cref="ServerEventLog"/> の
    /// 1 行の JSON にして渡す処理。渡す先は <see cref="NetBootstrap"/> が <c>-log-format json</c> のときだけ
    /// <see cref="SetServerEventWriter"/> で入れ、入っていなければ JSON は作らない。
    /// </summary>
    /// <remarks>
    /// 試合の Id は Session を作ったときと次の試合を始めるとき (<see cref="TeamDeathmatchSession.Restart"/>) に作り直す。
    /// 次の試合に残った人には参加のイベントを出し直さないので、その試合の顔ぶれは match_started の players で分かる。
    /// </remarks>
    public sealed partial class NetTeamDeathmatch
    {
        // 抜けるときには NetConnectionKeys が先に鍵を忘れていることがあるので、入ったときに覚えておく
        private readonly Dictionary<int, string> _productUserIds = new Dictionary<int, string>();

        private Action<string> _serverEventWriter;
        private bool _keysAreProductUserIds;
        private string _matchId;

        /// <summary>
        /// 試合のイベントの JSON の行を受け取る先を入れる。null なら JSON を作らない。
        /// </summary>
        /// <param name="writer">1 行ずつ (改行なしで) 受け取る。</param>
        /// <param name="keysAreProductUserIds">
        /// 接続の鍵 (<see cref="NetConnectionKeys"/>) が EOS の ProductUserId か。EOS で待ち受けるサーバーだけ true。
        /// UDP の鍵は端末の Id で、ProductUserId と同じ形をしていても出さない。
        /// </param>
        internal void SetServerEventWriter(Action<string> writer, bool keysAreProductUserIds)
        {
            _serverEventWriter = writer;
            _keysAreProductUserIds = writer != null && keysAreProductUserIds;
        }

        private void ServerEventsSessionCreated()
        {
            _matchId = ServerEventLog.NewMatchId();
            _productUserIds.Clear();
        }

        private void ServerEventsMatchRestarted()
        {
            _matchId = ServerEventLog.NewMatchId();
        }

        private void ServerEventsMatchStarted(int playerCount)
        {
            if (WritesServerEvents)
            {
                _serverEventWriter(ServerEventLog.MatchStarted(DateTime.UtcNow, _matchId, playerCount));
            }
        }

        private void ServerEventsPlayerJoined(int playerId, string key, Team team, bool rejoined)
        {
            if (_keysAreProductUserIds && NetLaunchOptions.IsProductUserId(key))
            {
                _productUserIds[playerId] = key;
            }
            else
            {
                _productUserIds.Remove(playerId);
            }

            if (WritesServerEvents)
            {
                _serverEventWriter(ServerEventLog.PlayerJoined(
                    DateTime.UtcNow, _matchId, playerId, ProductUserIdOf(playerId), team, rejoined));
            }
        }

        private void ServerEventsPlayerLeft(int playerId)
        {
            if (WritesServerEvents)
            {
                _serverEventWriter(ServerEventLog.PlayerLeft(DateTime.UtcNow, _matchId, playerId, ProductUserIdOf(playerId)));
            }

            _productUserIds.Remove(playerId);
        }

        private void ServerEventsKilled(int killerId, int victimId, bool teamKill)
        {
            if (WritesServerEvents)
            {
                _serverEventWriter(ServerEventLog.Kill(
                    DateTime.UtcNow, _matchId, killerId, ProductUserIdOf(killerId), victimId, ProductUserIdOf(victimId), teamKill));
            }
        }

        private void ServerEventsMatchEnded(Team? winner)
        {
            if (WritesServerEvents)
            {
                _serverEventWriter(ServerEventLog.MatchEnded(
                    DateTime.UtcNow, _matchId, winner, _session.GetScore(Team.Alpha), _session.GetScore(Team.Bravo)));
            }
        }

        private void ServerEventsPingPlaced(SharedPing ping, bool refreshed)
        {
            if (WritesServerEvents)
            {
                var marker = ping.Marker;
                _serverEventWriter(ServerEventLog.PingPlaced(
                    DateTime.UtcNow, _matchId, ping.PingerId, ProductUserIdOf(ping.PingerId), ping.Team, marker.Id, marker.Kind,
                    marker.Kind == PingKind.Enemy ? marker.TargetKey : null, refreshed));
            }
        }

        private void ServerEventsPingRefused(int playerId, PingRefusal reason)
        {
            if (WritesServerEvents)
            {
                _serverEventWriter(ServerEventLog.PingRefused(DateTime.UtcNow, _matchId, playerId, ProductUserIdOf(playerId), reason));
            }
        }

        // Session を作れなかった試合 (設定の誤り) では Id が無く、どのイベントも起きないはずだが、空の Id で書かないよう確かめる
        private bool WritesServerEvents => _serverEventWriter != null && _matchId != null;

        private string ProductUserIdOf(int playerId)
        {
            return _productUserIds.TryGetValue(playerId, out var productUserId) ? productUserId : null;
        }
    }
}

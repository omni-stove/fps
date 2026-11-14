using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    // 入力が GameRules.IdleKickSeconds 秒無い人を切る。試合中で、生きていてダウンしていない間だけ数える
    internal sealed partial class MatchRulesModule
    {
        private const InputButtons _pressedButtons = InputButtons.Jump | InputButtons.Cover | InputButtons.Dodge;
        private const InputButtons _heldButtons = InputButtons.Sprint | InputButtons.Crouch | InputButtons.Aim;

        private readonly Dictionary<int, InputFrame> _lastInputs = new Dictionary<int, InputFrame>();
        private readonly List<int> _idleIds = new List<int>();
        private readonly IdleWarningMessage _idleWarning = new IdleWarningMessage();

        // IdleKickSeconds が 0 のルールでは null
        private IdleTracker _idleTracker;

        internal IdleTracker IdleTracker => _idleTracker;

        /// <summary>
        /// 前の入力から操作が変わったか。押した瞬間だけ立つボタン (ジャンプ・カバー・回避) は押されていれば操作とする。
        /// Unity の NetworkPlayerMotor.IsActivity と同じ見方。
        /// </summary>
        internal static bool IsActivity(InputFrame previous, InputFrame current)
        {
            return (current.Buttons & _pressedButtons) != 0
                || current.Move != previous.Move
                || current.Yaw != previous.Yaw
                || (current.Buttons & _heldButtons) != (previous.Buttons & _heldButtons);
        }

        private void PrepareIdle(GameRules rules)
        {
            var seconds = rules.IdleKickSeconds;
            if (!(seconds > 0f))
            {
                _log.WriteLine(MatchLog.Tag + " idle kick off");
                return;
            }

            _idleTracker = new IdleTracker(seconds);
            _log.WriteLine(MatchLog.Tag + " idle kick seconds=" + seconds.ToString("0.##", CultureInfo.InvariantCulture)
                + " warning=" + _idleTracker.WarningSeconds.ToString("0.##", CultureInfo.InvariantCulture));
        }

        private void IdlePlayerJoined(int playerId)
        {
            if (_idleTracker == null)
            {
                return;
            }

            _lastInputs.Remove(playerId);
            _idleTracker.Remove(playerId);
            _idleTracker.Add(playerId);
        }

        private void IdlePlayerLeft(int playerId)
        {
            _lastInputs.Remove(playerId);
            _idleTracker?.Remove(playerId);
        }

        private void ObserveInput(int playerId, InputMessage input)
        {
            if (_idleTracker == null || input.Count == 0)
            {
                return;
            }

            var active = false;
            var hasPrevious = _lastInputs.TryGetValue(playerId, out var previous);
            for (var i = 0; i < input.Count; i++)
            {
                var frame = input[i];

                // 届き直した古い入力で時計を戻さない
                if (hasPrevious && frame.Tick <= previous.Tick)
                {
                    continue;
                }

                active |= !hasPrevious || IsActivity(previous, frame);
                previous = frame;
                hasPrevious = true;
            }

            _lastInputs[playerId] = previous;
            if (active)
            {
                TouchIdle(playerId);
            }
        }

        private void TouchIdle(int playerId)
        {
            if (_idleTracker != null && _idleTracker.Contains(playerId))
            {
                ReportIdle(playerId, _idleTracker.Touch(playerId));
            }
        }

        private void TickIdle(float deltaSeconds)
        {
            if (_idleTracker == null || _idleTracker.Count == 0)
            {
                return;
            }

            _idleIds.Clear();
            _idleIds.AddRange(_idleTracker.PlayerIds);
            foreach (var playerId in _idleIds)
            {
                if (!_idleTracker.Contains(playerId))
                {
                    continue;
                }

                var counted = _session.State == GameModeState.Playing && _session.HasPlayer(playerId)
                    && _session.IsAlive(playerId) && !_session.IsDowned(playerId);
                ReportIdle(playerId, _idleTracker.SetCounting(playerId, counted));
                ReportIdle(playerId, _idleTracker.Advance(playerId, deltaSeconds));
            }
        }

        private void ReportIdle(int playerId, IdleChange change)
        {
            if (!_match.TryGetPlayer(playerId, out var player))
            {
                return;
            }

            var id = playerId.ToString(CultureInfo.InvariantCulture);
            switch (change)
            {
                case IdleChange.Warned:
                    var remaining = _idleTracker.GetRemaining(playerId) ?? 0f;
                    _log.WriteLine(MatchLog.Tag + " idle warning player=" + id + " seconds=" + remaining.ToString("0.##", CultureInfo.InvariantCulture));
                    _idleWarning.Active = true;
                    _idleWarning.RemainingSeconds = remaining;
                    _context.Messenger.Send(player.Connection, TransportChannel.Reliable, _idleWarning);
                    break;
                case IdleChange.WarningCleared:
                    _log.WriteLine(MatchLog.Tag + " idle warning cleared player=" + id);
                    _idleWarning.Active = false;
                    _idleWarning.RemainingSeconds = 0f;
                    _context.Messenger.Send(player.Connection, TransportChannel.Reliable, _idleWarning);
                    break;
                case IdleChange.Kicked:
                    _log.WriteLine(MatchLog.Tag + " idle kick player=" + id
                        + " client=" + player.Connection.Value.ToString(CultureInfo.InvariantCulture)
                        + " after=" + _idleTracker.KickSeconds.ToString("0.##", CultureInfo.InvariantCulture) + "s");

                    // 切ると ServerMatch.Leave から HandlePlayerLeft が呼ばれて数えるのをやめる
                    _context.Control.Kick(player.Connection, DisconnectNotice.IdleKickedReason, "idle for " + _idleTracker.KickSeconds.ToString("0.##", CultureInfo.InvariantCulture) + "s");
                    break;
            }
        }
    }
}

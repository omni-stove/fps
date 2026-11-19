using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 投票をするかと数え方は、言い出すたびに今のルールの NetMatchSettings.VoteKick から読む。
    // 次の試合でルールが替わったときに、前のルールの設定のまま言い出せないようにするため。
    internal sealed partial class MatchRulesModule
    {
        // 票の動きが無くても残りの秒をクライアントで数え直さずに済むよう、決まった間隔で送り直す (マップの投票と同じ)
        private const float _voteKickSendInterval = 1f;

        private readonly VoteKick _voteKick = new VoteKick();
        private readonly VoteKickStateMessage _voteKickState = new VoteKickStateMessage();
        private readonly List<int> _voteKickPlayers = new List<int>();
        private float _voteKickSendElapsed;
        private float _nextVoteKickRefusalLogTime;

        internal VoteKick CurrentVoteKick => _voteKick;

        internal VoteKickCall ServerCallVoteKick(int callerId, int targetId)
        {
            _voteKickPlayers.Clear();
            foreach (var player in _match.Players)
            {
                if (!player.IsBot && _session.HasPlayer(player.PlayerId))
                {
                    _voteKickPlayers.Add(player.PlayerId);
                }
            }

            var callerKey = _match.TryGetPlayer(callerId, out var caller) ? KeyOf(caller.RejoinKey) : null;
            var result = _voteKick.Call(_data.Settings.VoteKick, callerId, callerKey, targetId, _voteKickPlayers);
            if (result != VoteKickCall.Started)
            {
                RefuseVoteKick(VoteKickLog.Called(callerId, targetId, result));
                return result;
            }

            _log.WriteLine(VoteKickLog.Called(callerId, targetId, result));
            if (_voteKick.IsOpen)
            {
                _log.WriteLine(VoteKickLog.Opened(_voteKick));
            }

            _voteKickSendElapsed = 0f;
            VoteKickChanged();
            return result;
        }

        internal VoteKickBallot ServerVoteKickBallot(int voterId, int targetId, bool inFavor)
        {
            var result = _voteKick.Vote(voterId, targetId, inFavor);
            switch (result)
            {
                case VoteKickBallot.Cast:
                case VoteKickBallot.Changed:
                    _log.WriteLine(VoteKickLog.Voted(voterId, targetId, inFavor, result));
                    VoteKickChanged();
                    break;
                case VoteKickBallot.NoVote:
                case VoteKickBallot.OtherTarget:
                case VoteKickBallot.NotVoter:
                    RefuseVoteKick(VoteKickLog.Voted(voterId, targetId, inFavor, result));
                    break;
            }

            return result;
        }

        // 言い出しと票を中身で分けるのは、VoteKickRequestMessage が両方を同じ形で送るため (進行中の投票の対象でない人への賛成が言い出し)
        partial void OnVoteKickRequest(ServerPlayer player, VoteKickRequestMessage request)
        {
            var target = request.TargetPlayerId;
            var votesOnOpen = _voteKick.IsOpen && _voteKick.TargetId == target;
            if (!votesOnOpen && request.InFavor)
            {
                ServerCallVoteKick(player.PlayerId, target);
                return;
            }

            ServerVoteKickBallot(player.PlayerId, target, request.InFavor);
        }

        partial void PrepareVoteKick()
        {
            _log.WriteLine(VoteKickLog.Settings(_data.Settings.VoteKick));
        }

        partial void TickVoteKick(float deltaSeconds)
        {
            if (_voteKick.Tick(deltaSeconds))
            {
                VoteKickChanged();
                return;
            }

            if (!_voteKick.IsOpen)
            {
                return;
            }

            // 次の試合のルールで投票キックを切ったなら、前のルールで始めた投票で外さない
            if (!_data.Settings.VoteKick.Enabled)
            {
                _voteKick.Cancel();
                VoteKickChanged();
                return;
            }

            _voteKickSendElapsed += deltaSeconds;
            if (_voteKickSendElapsed >= _voteKickSendInterval)
            {
                _voteKickSendElapsed -= _voteKickSendInterval;
                SendVoteKickToAll();
            }
        }

        private void VoteKickPlayerJoined(ServerPlayer player)
        {
            if (_voteKick.IsOpen && player.Connection.IsValid)
            {
                FillVoteKickState();
                _context.Messenger.Send(player.Connection, TransportChannel.Reliable, _voteKickState);
            }
        }

        private void VoteKickPlayerLeft(int playerId)
        {
            if (!_voteKick.Leave(playerId))
            {
                return;
            }

            _log.WriteLine(VoteKickLog.Left(playerId));
            VoteKickChanged();
        }

        private void StopVoteKick()
        {
            _voteKick.Cancel();
        }

        private void VoteKickChanged()
        {
            if (_voteKick.IsOpen)
            {
                SendVoteKickToAll();
                return;
            }

            var result = _voteKick.Result
                ?? throw new InvalidOperationException("a vote kick closed without a result");
            _log.WriteLine(VoteKickLog.Decided(result));
            FillDecidedVoteKickState(result);
            SendToAll(TransportChannel.Reliable, _voteKickState);
            if (result.Passed)
            {
                KickByVote(result);
            }
        }

        // 外したことを先に全員へ送ってから切るのは、外された人にも結果が届くようにするため (切るのは IServerControl が tick の終わりにする)
        private void KickByVote(VoteKickResult result)
        {
            if (!_match.TryGetPlayer(result.TargetId, out var target) || target.IsBot)
            {
                return;
            }

            var barred = _match.BarUntilNextMatch(target.RejoinKey);
            _log.WriteLine(VoteKickLog.Kicked(target.PlayerId, barred));
            _context.Control.Kick(target.Connection, DisconnectNotice.VoteKickedReason, VoteKickLog.KickedLogText(result));
        }

        // 断った要求を送り続けるクライアントでログを埋めないよう、チャット・マップの投票と同じく間引く
        private void RefuseVoteKick(string line)
        {
            if (_now < _nextVoteKickRefusalLogTime)
            {
                return;
            }

            _nextVoteKickRefusalLogTime = _now + _rejectWarningInterval;
            _log.WriteLine(line);
        }

        private void SendVoteKickToAll()
        {
            FillVoteKickState();
            SendToAll(TransportChannel.Reliable, _voteKickState);
        }

        private void FillVoteKickState()
        {
            _voteKickState.TargetPlayerId = _voteKick.TargetId;
            _voteKickState.CallerPlayerId = _voteKick.CallerId;
            _voteKickState.YesVotes = _voteKick.YesVotes;
            _voteKickState.RequiredVotes = _voteKick.RequiredVotes;
            _voteKickState.RemainingSeconds = _voteKick.RemainingSeconds;
            _voteKickState.Outcome = VoteKickOutcome.Undecided;
        }

        private void FillDecidedVoteKickState(VoteKickResult result)
        {
            _voteKickState.TargetPlayerId = result.TargetId;
            _voteKickState.CallerPlayerId = result.CallerId;
            _voteKickState.YesVotes = result.YesVotes;
            _voteKickState.RequiredVotes = Math.Max(1, result.RequiredVotes);
            _voteKickState.RemainingSeconds = 0f;
            _voteKickState.Outcome = result.Passed ? VoteKickOutcome.Passed : VoteKickOutcome.Failed;
        }
    }
}

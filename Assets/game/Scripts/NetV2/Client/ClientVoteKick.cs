using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 送るのは部品 (NetV2VoteKickPart) に任せて要求を作るだけにするのは、NetV2Client が送り先を渡さずに同じ物を作って持つため。
    public sealed class ClientVoteKick
    {
        public event Action<ClientVoteKickState> StateReceived;

        public VoteKickStatus? Status { get; private set; }

        public bool IsOpen => Status.HasValue;

        public bool Handle(IMessage message)
        {
            if (!(message is VoteKickStateMessage state))
            {
                return false;
            }

            Receive(state);
            return true;
        }

        // 表から読んだメッセージは値を確かめ済みなので、false になるのはコードで組んだ範囲外の値だけ。そのときは投票を閉じる
        public bool Receive(VoteKickStateMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var wellFormed = IsWellFormed(message);
            Status = wellFormed ? ToStatus(message) : null;
            StateReceived?.Invoke(ClientVoteKickState.From(message));
            return wellFormed;
        }

        public static VoteKickRequestMessage CallRequest(int targetId)
        {
            if (targetId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetId), targetId, "targetId must not be negative");
            }

            return new VoteKickRequestMessage { TargetPlayerId = targetId, InFavor = true };
        }

        // 進行中の投票が無いときは送る物が無いので空を返す。サーバーから見れば対象を付けた賛成の 1 票は言い出しになるため、空のまま送らせない
        public VoteKickRequestMessage VoteRequest(bool inFavor)
        {
            if (!Status.HasValue)
            {
                return null;
            }

            return new VoteKickRequestMessage { TargetPlayerId = Status.Value.TargetId, InFavor = inFavor };
        }

        public void Reset()
        {
            Status = null;
        }

        public static VoteKickStatus? ToStatus(VoteKickStateMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (message.Outcome != VoteKickOutcome.Undecided)
            {
                return null;
            }

            return new VoteKickStatus(
                message.TargetPlayerId, message.CallerPlayerId, message.YesVotes, message.RequiredVotes, message.RemainingSeconds);
        }

        // VoteKickStatus は範囲外の値を例外で拒むので、先に確かめて投票ごと閉じる
        private static bool IsWellFormed(VoteKickStateMessage message)
        {
            var seconds = message.RemainingSeconds;
            return message.TargetPlayerId >= 0
                && message.CallerPlayerId >= 0
                && message.YesVotes >= 0
                && message.RequiredVotes >= 1
                && !float.IsNaN(seconds)
                && !float.IsInfinity(seconds)
                && seconds >= 0f
                && Enum.IsDefined(typeof(VoteKickOutcome), message.Outcome);
        }
    }
}

namespace Shooter.Runtime
{
    public interface IVoteKickSender
    {
        void Call(int targetId);

        // 対象を渡さないのは、投票は進行中の 1 件 (GameSignals.VoteKick の TargetId) にしか入れられないため。
        void Vote(bool inFavor);
    }
}

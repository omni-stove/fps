namespace Shooter.Runtime
{
    public interface IMapVoteSender
    {
        // マップ Id でなく並びの番号で選ぶのは、投票の知らせがサーバーへ番号で届くため。番号は MapVoteStatus.Candidates の並び。
        void Vote(int candidateIndex);
    }
}

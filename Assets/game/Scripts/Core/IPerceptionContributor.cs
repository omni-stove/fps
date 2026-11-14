namespace Fps.Core
{
    public interface IPerceptionContributor
    {
        // 知覚を書き換えた写しを返させるのは、EnemyPerception が readonly struct で、前の contributor の結果を次へ順に渡すため。
        EnemyPerception Contribute(EnemyPerception perception);
    }
}

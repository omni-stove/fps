namespace Fps.Core
{
    // IPerceptionContributor (EnemyAgent が知覚を組み立てた直後に通す口) と分けるのは、こちらは判断と同じ tick の中で brain と経過秒を要るため。
    public interface IBrainPerceptionContributor
    {
        // 状態機械が判断する前に呼ぶ。判断の後に呼ぶと、書き換えた知覚 (気づきの度合いなど) がその tick の判断に効かず 1 tick 遅れる。
        // 書き換えないなら perception をそのまま返す。
        EnemyPerception Contribute(EnemyBrain brain, float deltaSeconds, EnemyPerception perception);
    }
}

namespace Fps.Core
{
    public interface IEnemyBehavior
    {
        // 状態機械が状態と経過時間を進めた後に呼ぶ。前に呼ぶと、上書きした tick に見失いや反応の時間が進まず、外したときに判断が飛ぶ。
        // 上書きしないなら false を返し、decision には planned をそのまま入れる。
        bool TryOverride(EnemyBrain brain, float deltaSeconds, EnemyPerception perception, EnemyDecision planned, out EnemyDecision decision);
    }
}

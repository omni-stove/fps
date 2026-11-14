namespace Fps.Core
{
    // 遮蔽点に着いた後だけ効かせる。向かう途中 (Moving) で止めると、撃たれている開けた場所で立ち止まってしまう。
    public sealed class SuppressedCoverBehavior : IEnemyBehavior
    {
        public bool IsHolding { get; private set; }

        public bool TryOverride(EnemyBrain brain, float deltaSeconds, EnemyPerception perception, EnemyDecision planned, out EnemyDecision decision)
        {
            decision = planned;
            var point = brain.CurrentCoverPoint;
            var settled = brain.CoverPhase == EnemyCoverPhase.Hiding || brain.CoverPhase == EnemyCoverPhase.Peeking;
            IsHolding = perception.IsSuppressed && brain.State == EnemyState.TakeCover && settled && point.HasValue;
            if (!IsHolding)
            {
                return false;
            }

            // 隠れている最中の判断は元からしゃがんで撃たないので、そのまま使う (移動先を足すと着いた後の立ち位置の扱いが変わる)。
            if (brain.CoverPhase == EnemyCoverPhase.Hiding)
            {
                return false;
            }

            // 覗く段階を判断の中で止めずに上書きで隠すのは、制圧が解けた tick にそのまま覗きの続きへ戻れるようにするため。
            decision = new EnemyDecision(point.Value.Position, planned.LookAt, false, true);
            return true;
        }
    }
}

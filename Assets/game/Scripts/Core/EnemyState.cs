namespace Shooter.Core
{
    public enum EnemyState
    {
        Idle,

        Patrol,

        Chase,

        Attack,

        Search,

        Dead,

        // 値を変えないよう、既存の状態の後ろに足している。
        TakeCover,
    }
}

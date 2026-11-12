namespace Fps.Core
{
    /// <summary>キャンペーンの敵の状態。</summary>
    public enum EnemyState
    {
        /// <summary>巡回ルートが無く、その場で待機している。</summary>
        Idle,

        /// <summary>巡回ルートを順に回っている。</summary>
        Patrol,

        /// <summary>目標を追っている。見失った直後は最後に見た位置へ向かう。</summary>
        Chase,

        /// <summary>目標が攻撃範囲内に見えていて、その場で狙っている。</summary>
        Attack,

        /// <summary>見失った位置や音のした位置を調べている。</summary>
        Search,

        /// <summary>死亡。以後は何も決定しない。</summary>
        Dead,
    }
}

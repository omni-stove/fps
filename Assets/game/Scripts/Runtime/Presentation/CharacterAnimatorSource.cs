namespace Fps.Runtime
{
    /// <summary><see cref="CharacterAnimator"/> がどの部品から状態を読むか。</summary>
    public enum CharacterAnimatorSource
    {
        /// <summary>読み元が無い (まだ探していないか、プレイヤーでも敵でもない)。</summary>
        None,

        /// <summary>PlayerController・WeaponController・AimDownSights・CoverController・Damageable。</summary>
        Player,

        /// <summary>EnemyAgent・NavMeshAgent・Damageable。</summary>
        Enemy,
    }
}

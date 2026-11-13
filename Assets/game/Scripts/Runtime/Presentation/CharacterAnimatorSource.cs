namespace Fps.Runtime
{
    /// <summary><see cref="CharacterAnimator"/> がどの部品から状態を読むか。</summary>
    public enum CharacterAnimatorSource
    {
        /// <summary>読み元が無い (まだ探していないか、プレイヤーでも敵でもなく <see cref="ICharacterPoseSource"/> も無い)。</summary>
        None,

        /// <summary>PlayerController・WeaponController・AimDownSights・CoverController・Damageable。</summary>
        Player,

        /// <summary>EnemyAgent・NavMeshAgent・Damageable。</summary>
        Enemy,

        /// <summary>同じ GameObject の <see cref="ICharacterPoseSource"/> (ネット対戦の NetPlayer)。速さは位置の変化から求める。</summary>
        Network,
    }
}

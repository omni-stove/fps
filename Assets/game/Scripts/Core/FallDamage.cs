namespace Fps.Core
{
    /// <summary>
    /// 高い所から落ちたときにダメージを受けるか。
    /// </summary>
    public enum FallDamage
    {
        /// <summary>落ちてもダメージを受けない。</summary>
        Off,

        /// <summary>着地の速さに応じてダメージを受ける (<see cref="MotorSpec.SafeFallSpeed"/>・<see cref="MotorSpec.LethalFallSpeed"/>)。</summary>
        On,
    }
}

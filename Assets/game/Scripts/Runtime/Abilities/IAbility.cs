namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="AbilityHost"/> の枠に差し込む能力の振る舞い。プレイヤー 1 人の 1 枠ごとに <see cref="AbilityDefinition.CreateAbility"/> が作る。
    /// 回数・待ち時間・効果が出るまでの秒数は枠 (<see cref="Fps.Core.AbilitySlot"/>) が持つので、ここでは持たない。
    /// 呼ばれる順は <see cref="CanUse"/> → <see cref="Begin"/> → (毎フレーム) <see cref="Tick"/> → <see cref="End"/>。
    /// 移動を変えたいときは <see cref="AbilityContext.OverrideMovement"/> を使う (その間 <see cref="Fps.Core.PlayerActionConditions.AbilityMoving"/> が立つ)。
    /// </summary>
    public interface IAbility
    {
        /// <summary>
        /// 枠の回数と待ち時間を満たしたうえで、今使えるか (狙った先が無いなど能力ごとの条件)。false なら回数を減らさずに断る。
        /// </summary>
        bool CanUse(AbilityContext context);

        /// <summary>使い始めたときに 1 度呼ばれる。回数は減らし済み。効果が出るのは <see cref="AbilityContext.EffectStartedThisFrame"/> の Tick から。</summary>
        void Begin(AbilityContext context);

        /// <summary>
        /// 使っている間、毎フレーム呼ばれる。続けるなら true、終えるなら false を返す。
        /// 効果が出るまでの秒数を待っている間も呼ばれる (<see cref="AbilityContext.Phase"/> で見分ける)。
        /// </summary>
        bool Tick(AbilityContext context, float deltaSeconds);

        /// <summary>
        /// 使い終えたときに 1 度呼ばれる。<see cref="Begin"/> と必ず対になる。
        /// interrupted は、<see cref="Tick"/> が false を返したのでなく、ルールが Off になった・倒れた・枠を替えた・例外が出たなどで止められたとき true。
        /// </summary>
        void End(AbilityContext context, bool interrupted);
    }
}

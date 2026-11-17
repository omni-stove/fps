namespace Fps.Runtime
{
    /// <summary>
    /// 使った後にもう一度押すと出す効果 (例: 置いた C4 の起爆) を持つ能力。<see cref="IAbility"/> と一緒に実装する。
    /// 枠の回数や待ち時間で使えないときも、<see cref="AbilityHost"/> は押されたら <see cref="TryFollowUp"/> を呼ぶ。
    /// </summary>
    public interface IAbilityFollowUp
    {
        /// <summary>枠の回数を使わずに、今出せる効果を出す。出せるものが無ければ何もせず false。</summary>
        bool TryFollowUp(AbilityContext context);

        /// <summary>今押すと何ができるか。HUD の枠の行に添える案内は UI がこれから作る。出せる効果が無ければ false。</summary>
        bool TryGetFollowUp(AbilityContext context, out AbilityFollowUp followUp);
    }
}

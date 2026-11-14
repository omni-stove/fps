namespace Fps.Runtime
{
    /// <summary>能力の枠ごとの回数と待ち時間の表示。ルールで能力を使わない間と、枠がすべて空の間は隠す。</summary>
    public interface IAbilitySlots
    {
        /// <summary>host の枠を出し始める。前に出していたものは手放す。null なら出すのをやめる。</summary>
        void Show(AbilityHost host);
    }
}

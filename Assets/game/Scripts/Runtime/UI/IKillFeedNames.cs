using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="KillFeedView"/> が <see cref="CombatEvents.Killed"/> の 1 件を、名前・チーム・武器の表示名の付いた行にするための引き先。
    /// Id の意味がオフライン (<see cref="CombatEvents.OfflineId"/>) とオンライン (プレイヤー Id) で違うので、それぞれが実装する。
    /// </summary>
    public interface IKillFeedNames
    {
        /// <summary>entry の倒した側・倒された側の名前と武器の表示名を引いて行にする。分からない名前でも例外にせず仮の名前にする。</summary>
        KillFeedLine Describe(KillFeedEntry entry);
    }
}

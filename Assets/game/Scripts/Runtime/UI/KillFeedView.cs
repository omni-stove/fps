using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 誰が誰を倒したかを並べるビュー。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// 今は要素を受け取るだけで何も出さない (<see cref="CombatEvents.Killed"/> を出す側の配線が先)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KillFeedView : HudElementView
    {
        /// <summary>Hud.uxml のキルフィードの要素。</summary>
        public const string UxmlName = "kill-feed";

        public override string ElementName => UxmlName;
    }
}

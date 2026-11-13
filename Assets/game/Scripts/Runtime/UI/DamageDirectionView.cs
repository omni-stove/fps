using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 撃たれた方向を画面に出すビュー。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// 今は要素を受け取るだけで何も出さない (<see cref="CombatEvents.DamageTaken"/> を出す側の配線が先)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageDirectionView : HudElementView
    {
        /// <summary>Hud.uxml の被弾方向の要素。</summary>
        public const string UxmlName = "damage-direction";

        public override string ElementName => UxmlName;
    }
}

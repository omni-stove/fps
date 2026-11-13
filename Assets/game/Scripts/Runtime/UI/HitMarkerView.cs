using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 弾が当たったことを照準の周りに出すビュー。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// 今は要素を受け取るだけで何も出さない (<see cref="CombatEvents.HitConfirmed"/> を出す側の配線が先)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitMarkerView : HudElementView
    {
        /// <summary>Hud.uxml のヒットマーカーの要素。</summary>
        public const string UxmlName = "hit-marker";

        public override string ElementName => UxmlName;
    }
}

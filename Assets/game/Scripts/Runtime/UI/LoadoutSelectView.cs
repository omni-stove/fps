using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 出現前に持っていく武器を選ぶビュー。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// 今は要素を受け取るだけで何も出さない (選んだロードアウトを送る処理は後で足す)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadoutSelectView : HudElementView
    {
        /// <summary>Hud.uxml のロードアウト選択の要素。</summary>
        public const string UxmlName = "loadout-select";

        public override string ElementName => UxmlName;
    }
}

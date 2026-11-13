using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 参加者ごとのキル・デスを一覧にするビュー。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// 今は要素を受け取るだけで何も出さない (Scoreboard の入力で開く処理は後で足す)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreboardView : HudElementView
    {
        /// <summary>Hud.uxml のスコアボードの要素。</summary>
        public const string UxmlName = "scoreboard";

        public override string ElementName => UxmlName;
    }
}

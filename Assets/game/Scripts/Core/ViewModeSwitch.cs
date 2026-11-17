using System;

namespace Fps.Core
{
    /// <summary>
    /// 実行中に一人称と三人称を入れ替えるときの決まり。入れ替え先 (<see cref="Other"/>) と、入れ替えたか断ったかのログの文面を決める。
    /// </summary>
    public static class ViewModeSwitch
    {
        public const string LogTag = "[ViewSwitch]";

        /// <summary>入れ替えを断った理由。</summary>
        public enum Refusal
        {
            /// <summary>断っていない。</summary>
            None = 0,

            /// <summary>この端末のプレイヤーの視点が無い (まだ生成していないか、倒れて消えた)。</summary>
            NoLocalView,

            /// <summary>死亡カメラや乗り物のカメラが視点を借りている。</summary>
            ViewBorrowed,
        }

        /// <summary>mode と逆の視点。</summary>
        /// <exception cref="ArgumentOutOfRangeException">mode が定義済みの値でない。</exception>
        public static ViewMode Other(ViewMode mode)
        {
            switch (mode)
            {
                case ViewMode.FirstPerson: return ViewMode.ThirdPerson;
                case ViewMode.ThirdPerson: return ViewMode.FirstPerson;
                default: throw new ArgumentOutOfRangeException(nameof(mode), mode, "mode is not a defined ViewMode");
            }
        }

        /// <summary>from から to へ入れ替えたときのログ。</summary>
        public static string FormatSwitchLog(ViewMode from, ViewMode to)
        {
            return $"{LogTag} view: {from} -> {to}";
        }

        /// <summary>入れ替えを断ったときのログ。</summary>
        /// <exception cref="ArgumentOutOfRangeException">refusal が <see cref="Refusal.None"/> か定義済みの値でない。</exception>
        public static string FormatRefusalLog(Refusal refusal)
        {
            switch (refusal)
            {
                case Refusal.NoLocalView: return $"{LogTag} refused: no local player view in this scene";
                case Refusal.ViewBorrowed: return $"{LogTag} refused: the view is borrowed by the death or vehicle camera";
                default: throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "refusal must name a reason");
            }
        }
    }
}

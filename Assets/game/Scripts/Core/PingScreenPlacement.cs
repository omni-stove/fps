namespace Fps.Core
{
    /// <summary><see cref="PingScreen.Place"/> の結果。印を画面のどこに、どの向きで出すか。</summary>
    public readonly struct PingScreenPlacement
    {
        public PingScreenPlacement(bool isOnScreen, float x, float y, float angleDegrees, PingScreenEdge edge)
        {
            IsOnScreen = isOnScreen;
            X = x;
            Y = y;
            AngleDegrees = angleDegrees;
            Edge = edge;
        }

        /// <summary>画面の中 (端の余白の内側) に見えているか。false なら端に寄せて向きを出す。</summary>
        public bool IsOnScreen { get; }

        /// <summary>出す位置の横。画面の左端が 0、右端が 1。</summary>
        public float X { get; }

        /// <summary>出す位置の縦。画面の上端が 0、下端が 1 (UI の向き)。</summary>
        public float Y { get; }

        /// <summary>画面の中心から印の方への向き (度)。真上が 0 で時計回り (右が 90、真下が 180、左が -90)。画面の中なら 0。</summary>
        public float AngleDegrees { get; }

        /// <summary>寄せた端。画面の中なら <see cref="PingScreenEdge.None"/>。</summary>
        public PingScreenEdge Edge { get; }
    }
}

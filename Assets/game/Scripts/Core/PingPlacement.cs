namespace Fps.Core
{
    /// <summary><see cref="PingBoard.Place"/> の結果。</summary>
    public readonly struct PingPlacement
    {
        public PingPlacement(PingMarker marker, bool refreshed, PingMarker? replaced)
        {
            Marker = marker;
            Refreshed = refreshed;
            Replaced = replaced;
        }

        /// <summary>付けた印。</summary>
        public PingMarker Marker { get; }

        /// <summary>同じ敵に付いていた印を付け直したなら true (番号は前のまま)。</summary>
        public bool Refreshed { get; }

        /// <summary>同時に付けられる数を超えたので消した、最も古い印。消していなければ null。</summary>
        public PingMarker? Replaced { get; }
    }
}

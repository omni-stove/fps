namespace Fps.Core
{
    /// <summary>縮むエリアの HUD の 1 行。秒は <see cref="HudFormat.Time"/> と同じ書式で出す。</summary>
    public static class ZoneHudText
    {
        public static string Closed => GameText.Get("zone.closed");

        /// <example>待ち: <c>エリア縮小まで 00:12</c>、縮み: <c>エリア縮小中 00:05</c>、最後: <c>最終エリア</c></example>
        public static string Line(ZoneState state)
        {
            switch (state.Stage)
            {
                case ZoneStage.Waiting:
                    return GameText.Format("zone.waiting", HudFormat.Time(state.SecondsUntilShrink));
                case ZoneStage.Shrinking:
                    return GameText.Format("zone.shrinking", HudFormat.Time(state.SecondsUntilStageEnds));
                default:
                    return Closed;
            }
        }
    }
}

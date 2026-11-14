namespace Fps.Core
{
    /// <summary>縮むエリアの HUD の 1 行。秒は <see cref="HudFormat.Time"/> と同じ書式で出す。</summary>
    public static class ZoneHudText
    {
        public static LocalizedText Closed => new LocalizedText("hud.zone.closed");

        /// <example>待ち: <c>エリア縮小まで 00:12</c>、縮み: <c>エリア縮小中 00:05</c>、最後: <c>最終エリア</c></example>
        public static LocalizedText Line(ZoneState state)
        {
            switch (state.Stage)
            {
                case ZoneStage.Waiting:
                    return new LocalizedText("hud.zone.waiting", HudFormat.Time(state.SecondsUntilShrink));
                case ZoneStage.Shrinking:
                    return new LocalizedText("hud.zone.shrinking", HudFormat.Time(state.SecondsUntilStageEnds));
                default:
                    return Closed;
            }
        }
    }
}

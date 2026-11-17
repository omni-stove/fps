using Shooter.Core;

namespace Shooter.Runtime
{
    public static class ZoneHudFormatter
    {
        public static LocalizedText Closed => new LocalizedText("hud.zone.closed");

        public static LocalizedText Line(ZoneState state)
        {
            switch (state.Stage)
            {
                case ZoneStage.Waiting:
                    return new LocalizedText("hud.zone.waiting", HudNumberFormat.RemainingTime(state.SecondsUntilShrink));
                case ZoneStage.Shrinking:
                    return new LocalizedText("hud.zone.shrinking", HudNumberFormat.RemainingTime(state.SecondsUntilStageEnds));
                default:
                    return Closed;
            }
        }
    }
}

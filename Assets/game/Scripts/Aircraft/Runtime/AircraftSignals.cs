using Shooter.Core;

namespace Shooter.Runtime
{
    public static class AircraftSignals
    {
        // GameSignals のプロパティにせず Create で作るのは、飛行機を外すゲームで本体の一覧に残さず、それでも ResetAll で空に戻すため。
        public static GameSignal<AircraftStatus?> Status { get; } = GameSignals.Create<AircraftStatus?>("Aircraft");
    }
}

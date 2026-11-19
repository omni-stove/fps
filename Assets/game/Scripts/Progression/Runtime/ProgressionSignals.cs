using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class ProgressionSignals
    {
        // GameSignals のプロパティにせず Create で作るのは、進行を外すゲームで本体の一覧に残さず、それでも ResetAll で空に戻すため。
        public static GameSignal<IReadOnlyList<AchievementProgress>> Achievements { get; } =
            GameSignals.Create<IReadOnlyList<AchievementProgress>>("Achievements");
    }
}

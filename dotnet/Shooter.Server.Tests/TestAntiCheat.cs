using Shooter.Core;

namespace Shooter.Server.Tests
{
    // 既定ではアンチチートが 4 つとも効くので、隠す・切る挙動を見ないテストは、ルールのモジュール無しで組めるようこれを渡す
    internal static class TestAntiCheat
    {
        public static readonly AntiCheatTuning Off = new AntiCheatTuning(
            new AntiCheatTuning.ConcealSettings(
                false,
                AntiCheatTuning.ConcealSettings.DefaultMaxDistance,
                AntiCheatTuning.ConcealSettings.DefaultMarginDistance,
                AntiCheatTuning.ConcealSettings.DefaultMarginSeconds),
            new AntiCheatTuning.ViolationSettings(
                false,
                AntiCheatTuning.ViolationSettings.DefaultWindowSeconds,
                AntiCheatTuning.ViolationSettings.DefaultShotRejectionLimit,
                AntiCheatTuning.ViolationSettings.DefaultOverBudgetLimit),
            new AntiCheatTuning.StatsSettings(
                false,
                AntiCheatTuning.StatsSettings.DefaultWindowSeconds,
                AntiCheatTuning.StatsSettings.DefaultHeadshotRatio,
                AntiCheatTuning.StatsSettings.DefaultMinReactionMs,
                AntiCheatTuning.StatsSettings.DefaultSnapDegreesPerTick,
                AntiCheatTuning.StatsSettings.DefaultWallTrackSeconds,
                AntiCheatTuning.StatsSettings.DefaultMinSamples),
            new AntiCheatTuning.EvidenceSettings(
                false,
                AntiCheatTuning.EvidenceSettings.DefaultSeconds,
                AntiCheatTuning.EvidenceSettings.DefaultMaxFilesPerMinute));
    }
}

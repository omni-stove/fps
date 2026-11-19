using System;

namespace Shooter.Core
{
    // Battle Royale の試合の調整値。分隊・時間・Bot の探し方と、エリア (ZoneSpec)・物資投下 (SupplyDropSpec)・復帰 (RedeploySpec) をまとめて持つ。
    // 降ろす高さと最初の円はマップが持つので、ここには持たない
    public sealed class BattleRoyaleSpec
    {
        public const int DefaultSquadCount = BattleRoyaleSession.DefaultSquadCount;
        public const int DefaultSquadSize = BattleRoyaleSession.DefaultSquadSize;

        // 分隊の番号はスナップショット (PlayerSnapshot.SquadId) に 1 バイトで載せるので、それを超える数は作れない
        public const int MaxSquadCount = byte.MaxValue + 1;

        // 既定は制限なしにし、縮むエリアで決着させる (オフラインの BattleRoyaleRunner と同じ)
        public const float DefaultTimeLimitSeconds = 0f;

        public const float DefaultBotSearchRadius = BattleRoyaleBotPlanner.DefaultSearchRadius;

        // オフラインの BattleRoyaleRunner.DefaultBotPlanIntervalSeconds と同じ
        public const float DefaultBotPlanIntervalSeconds = 0.5f;

        public static BattleRoyaleSpec Default { get; } = new BattleRoyaleSpec();

        public BattleRoyaleSpec(
            int squadCount = DefaultSquadCount,
            int squadSize = DefaultSquadSize,
            float timeLimitSeconds = DefaultTimeLimitSeconds,
            float botSearchRadius = DefaultBotSearchRadius,
            float botPlanIntervalSeconds = DefaultBotPlanIntervalSeconds,
            ZoneSpec zone = null,
            SupplyDropSpec supplyDrops = null,
            RedeploySpec redeploy = null)
        {
            if (squadCount < 2 || squadCount > MaxSquadCount)
            {
                throw new ArgumentOutOfRangeException(nameof(squadCount), squadCount, "squadCount must be between 2 and " + MaxSquadCount);
            }

            SquadCount = squadCount;
            SquadSize = SpecGuard.AtLeast(squadSize, 1, nameof(squadSize));
            TimeLimitSeconds = SpecGuard.NonNegativeFinite(timeLimitSeconds, nameof(timeLimitSeconds));
            BotSearchRadius = SpecGuard.NonNegativeFinite(botSearchRadius, nameof(botSearchRadius));
            BotPlanIntervalSeconds = SpecGuard.PositiveFinite(botPlanIntervalSeconds, nameof(botPlanIntervalSeconds));
            Zone = zone ?? ZoneSpec.Default;
            SupplyDrops = supplyDrops ?? SupplyDropSpec.Default;
            Redeploy = redeploy ?? RedeploySpec.Default;
        }

        public int SquadCount { get; }

        public int SquadSize { get; }

        // 0 なら制限なし
        public float TimeLimitSeconds { get; }

        public bool HasTimeLimit => TimeLimitSeconds > 0f;

        public float BotSearchRadius { get; }

        public float BotPlanIntervalSeconds { get; }

        public ZoneSpec Zone { get; }

        public SupplyDropSpec SupplyDrops { get; }

        public RedeploySpec Redeploy { get; }
    }
}

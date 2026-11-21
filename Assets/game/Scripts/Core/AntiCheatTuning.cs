using System;

namespace Shooter.Core
{
    // 4 つの仕組みの値を 1 つのファイル (anticheat.json) にまとめるのは、運営がサーバー側の対策を 1 か所で見て切り替えられるようにするため。
    public sealed class AntiCheatTuning
    {
        // static readonly のフィールドにすると、各 Settings の ctor が RequirePositive を呼んだときにこの型の静的初期化が走り、
        // まだ作り終えていない Settings.Default (null) を読んで落ちるので、別の型に置いて初めて使うときに作る。
        public static AntiCheatTuning Default => Defaults.Value;

        private static class Defaults
        {
            public static readonly AntiCheatTuning Value = new AntiCheatTuning(
                ConcealSettings.Default, ViolationSettings.Default, StatsSettings.Default, EvidenceSettings.Default);
        }

        public AntiCheatTuning(ConcealSettings conceal, ViolationSettings violations, StatsSettings stats, EvidenceSettings evidence)
        {
            Conceal = conceal ?? throw new ArgumentNullException(nameof(conceal));
            Violations = violations ?? throw new ArgumentNullException(nameof(violations));
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        }

        public ConcealSettings Conceal { get; }

        public ViolationSettings Violations { get; }

        public StatsSettings Stats { get; }

        public EvidenceSettings Evidence { get; }

        public sealed class ConcealSettings
        {
            public const bool DefaultEnabled = true;
            public const float DefaultMaxDistance = 150f;
            public const float DefaultMarginDistance = 1.5f;
            public const float DefaultMarginSeconds = 0.5f;

            public static readonly ConcealSettings Default = new ConcealSettings(
                DefaultEnabled, DefaultMaxDistance, DefaultMarginDistance, DefaultMarginSeconds);

            public ConcealSettings(bool enabled, float maxDistance, float marginDistance, float marginSeconds)
            {
                RequirePositive(maxDistance, nameof(maxDistance));
                RequireNonNegative(marginDistance, nameof(marginDistance));
                RequireNonNegative(marginSeconds, nameof(marginSeconds));
                Enabled = enabled;
                MaxDistance = maxDistance;
                MarginDistance = marginDistance;
                MarginSeconds = marginSeconds;
            }

            public bool Enabled { get; }

            public float MaxDistance { get; }

            // 体の中心だけを見ると、角から出る瞬間まで送り始められず相手が飛んで出るので、この距離だけ広げて見る。
            public float MarginDistance { get; }

            // 見えた tick だけを送ると、遮るものの縁で消えたり出たりを繰り返すので、最後に見えてからこの秒数は見えていると数える。
            public float MarginSeconds { get; }
        }

        public sealed class ViolationSettings
        {
            public const bool DefaultEnabled = true;
            public const float DefaultWindowSeconds = 30f;
            public const int DefaultShotRejectionLimit = 60;
            public const int DefaultOverBudgetLimit = 120;

            public static readonly ViolationSettings Default = new ViolationSettings(
                DefaultEnabled, DefaultWindowSeconds, DefaultShotRejectionLimit, DefaultOverBudgetLimit);

            public ViolationSettings(bool enabled, float windowSeconds, int shotRejectionLimit, int overBudgetLimit)
            {
                RequirePositive(windowSeconds, nameof(windowSeconds));
                RequireAtLeastOne(shotRejectionLimit, nameof(shotRejectionLimit));
                RequireAtLeastOne(overBudgetLimit, nameof(overBudgetLimit));
                Enabled = enabled;
                WindowSeconds = windowSeconds;
                ShotRejectionLimit = shotRejectionLimit;
                OverBudgetLimit = overBudgetLimit;
            }

            public bool Enabled { get; }

            public float WindowSeconds { get; }

            public int ShotRejectionLimit { get; }

            public int OverBudgetLimit { get; }
        }

        public sealed class StatsSettings
        {
            public const bool DefaultEnabled = true;
            public const float DefaultWindowSeconds = 120f;
            public const float DefaultHeadshotRatio = 0.6f;
            public const float DefaultMinReactionMs = 120f;
            public const float DefaultSnapDegreesPerTick = 45f;
            public const float DefaultWallTrackSeconds = 1.5f;
            public const int DefaultMinSamples = 10;

            // 1 tick の向きの差は短い回り方で測るので 180 度を超えず、それより大きい閾値は決して当たらない。
            public const float MaxSnapDegreesPerTick = 180f;

            public static readonly StatsSettings Default = new StatsSettings(
                DefaultEnabled,
                DefaultWindowSeconds,
                DefaultHeadshotRatio,
                DefaultMinReactionMs,
                DefaultSnapDegreesPerTick,
                DefaultWallTrackSeconds,
                DefaultMinSamples);

            public StatsSettings(
                bool enabled,
                float windowSeconds,
                float headshotRatio,
                float minReactionMs,
                float snapDegreesPerTick,
                float wallTrackSeconds,
                int minSamples)
            {
                RequirePositive(windowSeconds, nameof(windowSeconds));
                if (!(headshotRatio > 0f && headshotRatio <= 1f))
                {
                    throw new ArgumentOutOfRangeException(nameof(headshotRatio), headshotRatio, "headshotRatio must be greater than 0 and at most 1");
                }

                RequireNonNegative(minReactionMs, nameof(minReactionMs));
                if (!(snapDegreesPerTick > 0f && snapDegreesPerTick <= MaxSnapDegreesPerTick))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(snapDegreesPerTick), snapDegreesPerTick, "snapDegreesPerTick must be greater than 0 and at most 180");
                }

                RequirePositive(wallTrackSeconds, nameof(wallTrackSeconds));
                RequireAtLeastOne(minSamples, nameof(minSamples));
                Enabled = enabled;
                WindowSeconds = windowSeconds;
                HeadshotRatio = headshotRatio;
                MinReactionMs = minReactionMs;
                SnapDegreesPerTick = snapDegreesPerTick;
                WallTrackSeconds = wallTrackSeconds;
                MinSamples = minSamples;
            }

            public bool Enabled { get; }

            public float WindowSeconds { get; }

            public float HeadshotRatio { get; }

            // 秒でなくミリ秒で持つのは、人の反応の速さを論じるときの単位に合わせ、JSON で 0.12 と書き違えにくくするため。
            public float MinReactionMs { get; }

            public float SnapDegreesPerTick { get; }

            public float WallTrackSeconds { get; }

            // ヘッドショット率と反応時間の平均は、時間窓の中の命中がこの数に届くまで比べない。
            // 最初の 1 発が頭に当たっただけで率が 1 になり、閾値を超えてしまうため。
            public int MinSamples { get; }
        }

        public sealed class EvidenceSettings
        {
            public const bool DefaultEnabled = true;
            public const float DefaultSeconds = 30f;
            public const int DefaultMaxFilesPerMinute = 10;

            public static readonly EvidenceSettings Default = new EvidenceSettings(DefaultEnabled, DefaultSeconds, DefaultMaxFilesPerMinute);

            public EvidenceSettings(bool enabled, float seconds, int maxFilesPerMinute = DefaultMaxFilesPerMinute)
            {
                RequirePositive(seconds, nameof(seconds));
                RequireAtLeastOne(maxFilesPerMinute, nameof(maxFilesPerMinute));
                Enabled = enabled;
                Seconds = seconds;
                MaxFilesPerMinute = maxFilesPerMinute;
            }

            public bool Enabled { get; }

            public float Seconds { get; }

            // 通報の間引きは (通報した人, 相手) の組ごとで、抜けて入り直すと消える。
            // 1 人が全員を通報し直すだけで証拠のファイルと整形が際限なく増えないよう、サーバー全体の 1 分あたりの数を絞る
            public int MaxFilesPerMinute { get; }
        }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }

        private static void RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a non-negative finite number");
            }
        }

        private static void RequireAtLeastOne(int value, string name)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be at least 1");
            }
        }
    }
}

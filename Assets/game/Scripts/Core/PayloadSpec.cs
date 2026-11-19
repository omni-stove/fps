using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    // 道筋と中継点の位置はマップで決まるので持たない。速さの倍率を MinPushers 人目からの人数ごとの並び (超えた人数は最後の値) で持つのは、
    // 1 人あたりの足し算では「2 人目で大きく速くなり 3 人目からは伸びない」のような段の付け方を書けないため。
    public sealed class PayloadSpec
    {
        public const float DefaultCartSpeed = 1.5f;
        public const int DefaultMinPushers = 1;
        public const float DefaultRollbackDelaySeconds = 10f;
        public const float DefaultRollbackSpeed = 0.5f;
        public const float DefaultCheckpointBonusSeconds = 60f;
        public const float DefaultTimeLimitSeconds = 300f;

        private static readonly float[] _defaultPusherSpeedMultipliers = { 1f, 1.5f, 2f };

        public static PayloadSpec Default { get; } = new PayloadSpec();

        public float CartSpeed { get; }

        public int MinPushers { get; }

        public IReadOnlyList<float> PusherSpeedMultipliers { get; }

        public float RollbackDelaySeconds { get; }

        public float RollbackSpeed { get; }

        public float CheckpointBonusSeconds { get; }

        public float TimeLimitSeconds { get; }

        public PayloadSpec(
            float cartSpeed = DefaultCartSpeed,
            int minPushers = DefaultMinPushers,
            IReadOnlyList<float> pusherSpeedMultipliers = null,
            float rollbackDelaySeconds = DefaultRollbackDelaySeconds,
            float rollbackSpeed = DefaultRollbackSpeed,
            float checkpointBonusSeconds = DefaultCheckpointBonusSeconds,
            float timeLimitSeconds = DefaultTimeLimitSeconds)
        {
            if (!(cartSpeed > 0f) || float.IsInfinity(cartSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(cartSpeed), cartSpeed, "cartSpeed must be positive and finite");
            }

            if (minPushers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(minPushers), minPushers, "minPushers must be at least 1");
            }

            if (!(rollbackDelaySeconds >= 0f) || float.IsInfinity(rollbackDelaySeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rollbackDelaySeconds), rollbackDelaySeconds, "rollbackDelaySeconds must not be negative and must be finite");
            }

            if (!(rollbackSpeed >= 0f) || float.IsInfinity(rollbackSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(rollbackSpeed), rollbackSpeed, "rollbackSpeed must not be negative and must be finite");
            }

            if (!(checkpointBonusSeconds >= 0f) || float.IsInfinity(checkpointBonusSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(checkpointBonusSeconds), checkpointBonusSeconds, "checkpointBonusSeconds must not be negative and must be finite");
            }

            if (!(timeLimitSeconds > 0f) || float.IsInfinity(timeLimitSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds), timeLimitSeconds, "timeLimitSeconds must be positive and finite");
            }

            CartSpeed = cartSpeed;
            MinPushers = minPushers;
            PusherSpeedMultipliers = CopyMultipliers(pusherSpeedMultipliers, nameof(pusherSpeedMultipliers));
            RollbackDelaySeconds = rollbackDelaySeconds;
            RollbackSpeed = rollbackSpeed;
            CheckpointBonusSeconds = checkpointBonusSeconds;
            TimeLimitSeconds = timeLimitSeconds;
        }

        // 押す側と止める側が同時に乗っているかは知らないので、押す人数だけで決める。止めるかは呼ぶ側が決める
        public float SpeedFor(int pushers)
        {
            if (pushers < MinPushers)
            {
                return 0f;
            }

            var index = Math.Min(pushers - MinPushers, PusherSpeedMultipliers.Count - 1);
            return CartSpeed * PusherSpeedMultipliers[index];
        }

        private static System.Collections.ObjectModel.ReadOnlyCollection<float> CopyMultipliers(IReadOnlyList<float> multipliers, string name)
        {
            if (multipliers == null)
            {
                return Array.AsReadOnly((float[])_defaultPusherSpeedMultipliers.Clone());
            }

            if (multipliers.Count == 0)
            {
                throw new ArgumentException(name + " must list at least one multiplier", name);
            }

            var copy = new float[multipliers.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                var value = multipliers[i];
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(
                        name, value, name + "[" + i.ToString(CultureInfo.InvariantCulture) + "] must be positive and finite");
                }

                copy[i] = value;
            }

            return Array.AsReadOnly(copy);
        }
    }
}

using System;

namespace Fps.Core
{
    public sealed class AwarenessSpec
    {
        public const float DefaultFieldOfViewDegrees = 120f;
        public const float DefaultGainPerSecond = 1f;
        public const float DefaultFullGainDistance = 3f;
        public const float DefaultFarGainFactor = 0.15f;
        public const float DefaultPeripheralGainFactor = 0.4f;
        public const float DefaultCrouchGainFactor = 0.5f;
        public const float DefaultProneGainFactor = 0.25f;
        public const float DefaultStillGainFactor = 0.5f;
        public const float DefaultFullMotionSpeed = 6f;
        // 警戒に入った直後に見失っても、最後に見た場所へ着くまで警戒が続くだけの待ち。
        public const float DefaultDecayDelaySeconds = 3f;
        public const float DefaultDecayPerSecond = 0.1f;
        public const float DefaultNoticeThreshold = 0.2f;
        public const float DefaultAlertThreshold = 0.6f;
        public const float DefaultEngagedHoldThreshold = 0.8f;
        public const float DefaultAlarmRadius = 15f;

        // 銃声 1 発で、すぐそばなら気づきの手前から警戒まで上がる量。連射すると遠くても警戒に届く。
        public const float DefaultGunshotGain = 0.4f;

        // 歩いて 1 歩ごとに足す量。真後ろを歩き続けると数歩で振り向く。
        public const float DefaultFootstepGain = 0.1f;

        // 警戒の境目より上で、減り始めてから警戒が続く間に、物音の所まで歩いて着けるだけの量。
        public const float DefaultInvestigateLevel = 0.75f;

        public static AwarenessSpec Default { get; } = new AwarenessSpec();

        public AwarenessSpec(
            float fieldOfViewDegrees = DefaultFieldOfViewDegrees,
            float gainPerSecond = DefaultGainPerSecond,
            float fullGainDistance = DefaultFullGainDistance,
            float farGainFactor = DefaultFarGainFactor,
            float peripheralGainFactor = DefaultPeripheralGainFactor,
            float crouchGainFactor = DefaultCrouchGainFactor,
            float proneGainFactor = DefaultProneGainFactor,
            float stillGainFactor = DefaultStillGainFactor,
            float fullMotionSpeed = DefaultFullMotionSpeed,
            float decayDelaySeconds = DefaultDecayDelaySeconds,
            float decayPerSecond = DefaultDecayPerSecond,
            float noticeThreshold = DefaultNoticeThreshold,
            float alertThreshold = DefaultAlertThreshold,
            float alarmRadius = DefaultAlarmRadius,
            float engagedHoldThreshold = DefaultEngagedHoldThreshold,
            float gunshotGain = DefaultGunshotGain,
            float footstepGain = DefaultFootstepGain,
            float investigateLevel = DefaultInvestigateLevel)
        {
            if (!(fieldOfViewDegrees > 0f) || fieldOfViewDegrees > 360f)
            {
                throw new ArgumentOutOfRangeException(nameof(fieldOfViewDegrees), fieldOfViewDegrees, "fieldOfViewDegrees must be greater than 0 and at most 360");
            }

            RequirePositive(gainPerSecond, nameof(gainPerSecond));
            RequireNonNegative(fullGainDistance, nameof(fullGainDistance));
            RequireFraction(farGainFactor, nameof(farGainFactor));
            RequireFraction(peripheralGainFactor, nameof(peripheralGainFactor));
            RequireFraction(crouchGainFactor, nameof(crouchGainFactor));
            RequireFraction(proneGainFactor, nameof(proneGainFactor));
            RequireFraction(stillGainFactor, nameof(stillGainFactor));
            RequirePositive(fullMotionSpeed, nameof(fullMotionSpeed));
            RequireNonNegative(decayDelaySeconds, nameof(decayDelaySeconds));
            RequireNonNegative(decayPerSecond, nameof(decayPerSecond));
            RequireNonNegative(alarmRadius, nameof(alarmRadius));

            if (!(noticeThreshold > 0f) || !(noticeThreshold <= alertThreshold))
            {
                throw new ArgumentOutOfRangeException(nameof(noticeThreshold), noticeThreshold, "noticeThreshold must be greater than 0 and at most alertThreshold");
            }

            // 交戦は量が満ちたときなので、警戒の境目が満ちた量以上だと警戒を通らずに交戦へ飛ぶ。
            if (!(alertThreshold < AwarenessMeter.Full))
            {
                throw new ArgumentOutOfRangeException(nameof(alertThreshold), alertThreshold, "alertThreshold must be less than 1");
            }

            // 交戦から下がるときに警戒を通るよう、交戦を保つ境目は警戒の境目より上に置く。
            if (!(engagedHoldThreshold > alertThreshold) || engagedHoldThreshold > AwarenessMeter.Full)
            {
                throw new ArgumentOutOfRangeException(nameof(engagedHoldThreshold), engagedHoldThreshold, "engagedHoldThreshold must be greater than alertThreshold and at most 1");
            }

            RequireFraction(gunshotGain, nameof(gunshotGain));
            RequireFraction(footstepGain, nameof(footstepGain));

            // 音だけで交戦にしないので満ちた量より下に置き、聞いた敵が調べに行くよう警戒の境目以上にする。
            if (!(investigateLevel >= alertThreshold) || !(investigateLevel < AwarenessMeter.Full))
            {
                throw new ArgumentOutOfRangeException(nameof(investigateLevel), investigateLevel, "investigateLevel must be at least alertThreshold and less than 1");
            }

            FieldOfViewDegrees = fieldOfViewDegrees;
            GainPerSecond = gainPerSecond;
            FullGainDistance = fullGainDistance;
            FarGainFactor = farGainFactor;
            PeripheralGainFactor = peripheralGainFactor;
            CrouchGainFactor = crouchGainFactor;
            ProneGainFactor = proneGainFactor;
            StillGainFactor = stillGainFactor;
            FullMotionSpeed = fullMotionSpeed;
            DecayDelaySeconds = decayDelaySeconds;
            DecayPerSecond = decayPerSecond;
            NoticeThreshold = noticeThreshold;
            AlertThreshold = alertThreshold;
            AlarmRadius = alarmRadius;
            EngagedHoldThreshold = engagedHoldThreshold;
            GunshotGain = gunshotGain;
            FootstepGain = footstepGain;
            InvestigateLevel = investigateLevel;
        }

        public float FieldOfViewDegrees { get; }
        public float GainPerSecond { get; }
        public float FullGainDistance { get; }
        public float FarGainFactor { get; }
        public float PeripheralGainFactor { get; }
        public float CrouchGainFactor { get; }
        public float ProneGainFactor { get; }
        public float StillGainFactor { get; }
        public float FullMotionSpeed { get; }
        public float DecayDelaySeconds { get; }
        public float DecayPerSecond { get; }
        public float NoticeThreshold { get; }
        public float AlertThreshold { get; }
        public float AlarmRadius { get; }
        public float EngagedHoldThreshold { get; }
        public float GunshotGain { get; }
        public float FootstepGain { get; }
        public float InvestigateLevel { get; }

        // 爆発やリロードの音で上げないのは、爆発は当たれば撃たれたのと同じく交戦になり、リロードは銃声の後にしか鳴らないため。
        public float HearingGain(SoundCueKind kind)
        {
            switch (kind)
            {
                case SoundCueKind.Gunshot:
                    return GunshotGain;
                case SoundCueKind.Footstep:
                    return FootstepGain;
                default:
                    return 0f;
            }
        }

        public float PostureFactor(AwarenessPosture posture)
        {
            switch (posture)
            {
                case AwarenessPosture.Crouching:
                    return CrouchGainFactor;
                case AwarenessPosture.Prone:
                    return ProneGainFactor;
                default:
                    return 1f;
            }
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
                throw new ArgumentOutOfRangeException(name, value, name + " must not be negative and must be finite");
            }
        }

        private static void RequireFraction(float value, string name)
        {
            if (!(value >= 0f) || value > 1f)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be between 0 and 1");
            }
        }
    }
}

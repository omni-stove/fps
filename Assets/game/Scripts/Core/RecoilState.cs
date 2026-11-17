using System;

namespace Shooter.Core
{
    public sealed class RecoilState
    {
        // 戻らない武器で撃ち続けても視線が裏返らないよう、たまったずれに上限を設ける。
        public const float MaxOffsetDegrees = 90f;

        private float _recoveryPerSecond;
        private float _secondsSinceKick;

        public float RecoveryDelaySeconds { get; private set; }

        public float PitchUpDegrees { get; private set; }

        public float YawDegrees { get; private set; }

        public bool IsAtRest => PitchUpDegrees == 0f && YawDegrees == 0f;

        public void Kick(WeaponSpec spec, int playerId, int shotIndex)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var yaw = YawKick(spec, playerId, shotIndex);
            PitchUpDegrees = Math.Min(MaxOffsetDegrees, PitchUpDegrees + spec.RecoilPitchDegrees);
            YawDegrees = Math.Max(-MaxOffsetDegrees, Math.Min(MaxOffsetDegrees, YawDegrees + yaw));
            _recoveryPerSecond = spec.RecoilRecoveryPerSecond;
            RecoveryDelaySeconds = spec.RecoilRecoveryDelaySeconds;
            _secondsSinceKick = 0f;
        }

        public static float YawKick(WeaponSpec spec, int playerId, int shotIndex)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (spec.RecoilYawDegrees == 0f)
            {
                return 0f;
            }

            var unit = SpreadPattern.Sample(playerId, shotIndex, SpreadPattern.RecoilYawStream);
            return (float)(((unit * 2.0) - 1.0) * spec.RecoilYawDegrees);
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            var waiting = Math.Max(0f, RecoveryDelaySeconds - _secondsSinceKick);
            var recovering = Math.Max(0f, deltaSeconds - waiting);
            _secondsSinceKick = Math.Min(RecoveryDelaySeconds, _secondsSinceKick + deltaSeconds);
            if (recovering <= 0f || IsAtRest || _recoveryPerSecond <= 0f)
            {
                return;
            }

            var length = Math.Sqrt((PitchUpDegrees * (double)PitchUpDegrees) + (YawDegrees * (double)YawDegrees));
            var step = recovering * (double)_recoveryPerSecond;
            if (step >= length)
            {
                Reset();
                return;
            }

            var scale = (length - step) / length;
            PitchUpDegrees = (float)(PitchUpDegrees * scale);
            YawDegrees = (float)(YawDegrees * scale);
        }

        public void Reset()
        {
            PitchUpDegrees = 0f;
            YawDegrees = 0f;
            _secondsSinceKick = RecoveryDelaySeconds;
        }
    }
}

using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class AircraftAimSettings
    {
        [SerializeField, Min(0f), Tooltip("視線が上下にずれたときに、ピッチを倒す強さ")]
        private float _pitchGain = AircraftAimSpec.DefaultPitchGain;

        [SerializeField, Min(0f), Tooltip("視線が左右にずれたときに、ヨーを倒す強さ")]
        private float _yawGain = AircraftAimSpec.DefaultYawGain;

        [SerializeField, Min(0f), Tooltip("視線が左右にずれたときに、ロールを倒す強さ")]
        private float _rollGain = AircraftAimSpec.DefaultRollGain;

        [SerializeField, Min(0f), Tooltip("傾いた機体を水平へ戻す強さ")]
        private float _levelGain = AircraftAimSpec.DefaultLevelGain;

        [SerializeField, Min(0f), Tooltip("移動の前後を押し切ったときに、スロットルの開き具合が変わる速さ (1/秒)")]
        private float _throttleRate = AircraftAimSpec.DefaultThrottleRate;

        public AircraftAimSpec ToSpec()
        {
            return new AircraftAimSpec(_pitchGain, _yawGain, _rollGain, _levelGain, _throttleRate);
        }

        public void CopyFrom(AircraftAimSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _pitchGain = spec.PitchGain;
            _yawGain = spec.YawGain;
            _rollGain = spec.RollGain;
            _levelGain = spec.LevelGain;
            _throttleRate = spec.ThrottleRate;
        }
    }
}

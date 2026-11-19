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

        [SerializeField, Min(0.01f), Tooltip("機首を上げ下げして掛けてよい G (荷重倍数) の上限")]
        private float _maxLoadFactor = AircraftAimSpec.DefaultMaxLoadFactor;

        [SerializeField, Min(0f), Tooltip("G の上限のどれだけ手前からピッチを絞り始めるか (G)。上限以下")]
        private float _loadFactorMargin = AircraftAimSpec.DefaultLoadFactorMargin;

        [SerializeField, Range(0.01f, 180f), Tooltip("失速する迎え角 (度)。ここに届く前にピッチを絞り切る")]
        private float _criticalAngleOfAttack = AircraftAimSpec.DefaultCriticalAngleOfAttack;

        [SerializeField, Min(0f), Tooltip("失速する迎え角のどれだけ手前からピッチを絞り始めるか (度)。失速する迎え角以下")]
        private float _angleOfAttackMargin = AircraftAimSpec.DefaultAngleOfAttackMargin;

        [SerializeField, Min(0f), Tooltip("降りていて、真下の地面まで何秒を切ったら機首を上げるか。0 で引き起こさない")]
        private float _groundWarningSeconds = AircraftAimSpec.DefaultGroundWarningSeconds;

        [SerializeField, Min(0f), Tooltip("これより速く降りているときだけ引き起こす (m/s)。着陸のためにゆっくり降りるときは引き起こさない")]
        private float _minSinkRate = AircraftAimSpec.DefaultMinSinkRate;

        public AircraftAimSpec ToSpec()
        {
            return new AircraftAimSpec(
                _pitchGain,
                _yawGain,
                _rollGain,
                _levelGain,
                _throttleRate,
                _maxLoadFactor,
                _loadFactorMargin,
                _criticalAngleOfAttack,
                _angleOfAttackMargin,
                _groundWarningSeconds,
                _minSinkRate);
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
            _maxLoadFactor = spec.MaxLoadFactor;
            _loadFactorMargin = spec.LoadFactorMargin;
            _criticalAngleOfAttack = spec.CriticalAngleOfAttack;
            _angleOfAttackMargin = spec.AngleOfAttackMargin;
            _groundWarningSeconds = spec.GroundWarningSeconds;
            _minSinkRate = spec.MinSinkRate;
        }
    }
}

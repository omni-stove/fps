using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 機体のアセット (AircraftDefinition) に入れず別のアセットにするのは、同じ機体を人と Bot が乗り換えて使い、Bot の飛ばし方だけを Bot ごとに変えられるようにするため。
    [CreateAssetMenu(fileName = "BotAircraftPilot", menuName = "Shooter/Bot Aircraft Pilot Settings")]
    public sealed class BotAircraftPilotSettings : ScriptableObject
    {
        [Header("速さ")]
        [SerializeField, Min(0.01f), Tooltip("保とうとする速さ (m/s)。機体の浮く速さ (Aero の Lift Speed) より十分に速くする")]
        private float _cruiseSpeed = AircraftAutopilotSpec.DefaultCruiseSpeed;

        [SerializeField, Range(0f, 1f), Tooltip("巡航の速さちょうどのときのスロットルの開き具合")]
        private float _cruiseThrottle = AircraftAutopilotSpec.DefaultCruiseThrottle;

        [SerializeField, Min(0f), Tooltip("速さが 1 m/s ずれたときに、スロットルを開け閉めする量")]
        private float _speedGain = AircraftAutopilotSpec.DefaultSpeedGain;

        [Header("曲がり方")]
        [SerializeField, Range(0.01f, 89f), Tooltip("曲がるときに傾けてよい角 (度)。大きいほど小さく回れる")]
        private float _maxBankAngle = AircraftAutopilotSpec.DefaultMaxBankAngle;

        [SerializeField, Min(0f), Tooltip("向かう点への向きが 1 度ずれたときに傾ける角 (度)")]
        private float _headingGain = AircraftAutopilotSpec.DefaultHeadingGain;

        [SerializeField, Min(0f), Tooltip("傾きが 1 度ずれたときに、ロールを倒す量")]
        private float _rollGain = AircraftAutopilotSpec.DefaultRollGain;

        [SerializeField, Min(0f), Tooltip("向かう点への向きが 1 度ずれたときに、ヨーを倒す量")]
        private float _yawGain = AircraftAutopilotSpec.DefaultYawGain;

        [Header("高さ")]
        [SerializeField, Range(0.01f, 89f), Tooltip("高さを合わせるときに上り下りしてよい角 (度)。地面が近いときはこの角で上る")]
        private float _maxClimbAngle = AircraftAutopilotSpec.DefaultMaxClimbAngle;

        [SerializeField, Min(0f), Tooltip("高さが 1 m ずれたときに上り下りする角 (度)")]
        private float _altitudeGain = AircraftAutopilotSpec.DefaultAltitudeGain;

        [SerializeField, Min(0f), Tooltip("上り下りの角が 1 度ずれたときに、ピッチを倒す量")]
        private float _pitchGain = AircraftAutopilotSpec.DefaultPitchGain;

        [Header("失速と地面")]
        [SerializeField, Min(0.01f), Tooltip("機首を上げ下げして掛けてよい G (荷重倍数) の上限")]
        private float _maxLoadFactor = AircraftAutopilotSpec.DefaultMaxLoadFactor;

        [SerializeField, Min(0f), Tooltip("G の上限のどれだけ手前からピッチを絞り始めるか (G)。上限以下")]
        private float _loadFactorMargin = AircraftAutopilotSpec.DefaultLoadFactorMargin;

        [SerializeField, Range(0.01f, 180f), Tooltip("失速する迎え角 (度)。ここに届く前に引き起こしを止める。機体の Aero の Critical Angle 以下にする")]
        private float _criticalAngleOfAttack = AircraftAutopilotSpec.DefaultCriticalAngleOfAttack;

        [SerializeField, Min(0f), Tooltip("失速する迎え角のどれだけ手前から引き起こしを絞り始めるか (度)。失速する迎え角以下")]
        private float _angleOfAttackMargin = AircraftAutopilotSpec.DefaultAngleOfAttackMargin;

        [SerializeField, Min(0f), Tooltip("真下の地面までがこれ (m) を切りそうなら、翼を水平に戻して上るのを優先する")]
        private float _minGroundClearance = AircraftAutopilotSpec.DefaultMinGroundClearance;

        [SerializeField, Min(0f), Tooltip("今の降りる速さのまま何秒先の高さで、地面までの近さを測るか")]
        private float _groundLookAheadSeconds = AircraftAutopilotSpec.DefaultGroundLookAheadSeconds;

        [Header("点")]
        [SerializeField, Min(0.01f), Tooltip("点からの水平の距離がこれ (m) 以下になったら、次の点へ向かう")]
        private float _arrivalRadius = AircraftAutopilotSpec.DefaultArrivalRadius;

        public AircraftAutopilotSpec ToSpec()
        {
            try
            {
                return new AircraftAutopilotSpec(
                    _cruiseSpeed,
                    _cruiseThrottle,
                    _speedGain,
                    _maxBankAngle,
                    _headingGain,
                    _rollGain,
                    _yawGain,
                    _maxClimbAngle,
                    _altitudeGain,
                    _pitchGain,
                    _maxLoadFactor,
                    _loadFactorMargin,
                    _criticalAngleOfAttack,
                    _angleOfAttackMargin,
                    _minGroundClearance,
                    _groundLookAheadSeconds,
                    _arrivalRadius);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException(BotAircraftPilotLog.InvalidSettings(name, e.Message), e);
            }
        }

        public void CopyFrom(AircraftAutopilotSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _cruiseSpeed = spec.CruiseSpeed;
            _cruiseThrottle = spec.CruiseThrottle;
            _speedGain = spec.SpeedGain;
            _maxBankAngle = spec.MaxBankAngle;
            _headingGain = spec.HeadingGain;
            _rollGain = spec.RollGain;
            _yawGain = spec.YawGain;
            _maxClimbAngle = spec.MaxClimbAngle;
            _altitudeGain = spec.AltitudeGain;
            _pitchGain = spec.PitchGain;
            _maxLoadFactor = spec.MaxLoadFactor;
            _loadFactorMargin = spec.LoadFactorMargin;
            _criticalAngleOfAttack = spec.CriticalAngleOfAttack;
            _angleOfAttackMargin = spec.AngleOfAttackMargin;
            _minGroundClearance = spec.MinGroundClearance;
            _groundLookAheadSeconds = spec.GroundLookAheadSeconds;
            _arrivalRadius = spec.ArrivalRadius;
        }
    }
}

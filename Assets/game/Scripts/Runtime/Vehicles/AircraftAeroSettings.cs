using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class AircraftAeroSettings
    {
        [SerializeField, Min(0.01f), Tooltip("水平に飛んでスロットルを開き切ったときに落ち着く速さ (m/s)")]
        private float _maxSpeed = AircraftAeroSpec.DefaultMaxSpeed;

        [SerializeField, Min(0.01f), Tooltip("迎え角を臨界角まで上げたときに、翼が重さと同じだけ持ち上げる速さ (m/s)。これより遅いと、どう引き起こしても沈む")]
        private float _liftSpeed = AircraftAeroSpec.DefaultLiftSpeed;

        [SerializeField, Min(0.01f), Tooltip("この速さ (m/s) で、舵が効き切る")]
        private float _controlSpeed = AircraftAeroSpec.DefaultControlSpeed;

        [SerializeField, Range(0f, 1f), Tooltip("止まっていても残る舵の効き (0 から 1)")]
        private float _minControl = AircraftAeroSpec.DefaultMinControl;

        [SerializeField, Min(0f), Tooltip("ピッチを倒し切ったときの、機首を上下に回す速さ (度/秒)")]
        private float _pitchRate = AircraftAeroSpec.DefaultPitchRate;

        [SerializeField, Min(0f), Tooltip("ロールを倒し切ったときの、機体を傾ける速さ (度/秒)")]
        private float _rollRate = AircraftAeroSpec.DefaultRollRate;

        [SerializeField, Min(0f), Tooltip("ヨーを倒し切ったときの、機首を左右に回す速さ (度/秒)")]
        private float _yawRate = AircraftAeroSpec.DefaultYawRate;

        [SerializeField, Min(0f), Tooltip("回る速さを舵の求める速さへ寄せる強さ (1/秒)。大きいほど機敏")]
        private float _responsiveness = AircraftAeroSpec.DefaultResponsiveness;

        [SerializeField, Min(0f), Tooltip("横へ流れる速さを、機首の向きへ寄せる強さ (1/秒)。大きいほど滑らずに曲がる。上下は揚力が受け持つ")]
        private float _alignment = AircraftAeroSpec.DefaultAlignment;

        [SerializeField, Min(0f), Tooltip("エアブレーキを掛け切ったときに、空気の抵抗へ足す倍率")]
        private float _airBrakeDrag = AircraftAeroSpec.DefaultAirBrakeDrag;

        [SerializeField, Min(0.0001f), Tooltip("迎え角 1 度あたりに増える揚力の係数")]
        private float _liftSlope = AircraftAeroSpec.DefaultLiftSlope;

        [SerializeField, Range(0.1f, 89.9f), Tooltip("揚力の係数が頭を打つ迎え角 (度)。これを越えると失速して揚力が落ちる")]
        private float _criticalAngle = AircraftAeroSpec.DefaultCriticalAngle;

        [SerializeField, Min(0f), Tooltip("失速したあとの揚力の係数の大きさ。迎え角 45 度でこの値になる。臨界角での係数より小さいほど、失速で大きく落ちる")]
        private float _postStallLift = AircraftAeroSpec.DefaultPostStallLift;

        [SerializeField, Min(0f), Tooltip("揚力の係数の 2 乗に掛けて抗力の係数へ足す値。大きいほど、引き起こして曲がるときに速さを失う")]
        private float _inducedDrag = AircraftAeroSpec.DefaultInducedDrag;

        [SerializeField, Min(0f), Tooltip("横滑りしたときに機首を風上へ回す強さ (1/秒²、LiftSpeed で横滑り 1 ラジアンあたり)")]
        private float _weathervane = AircraftAeroSpec.DefaultWeathervane;

        [SerializeField, Min(0f), Tooltip("横滑りしたときに傾きを戻す強さ (上反角の効き。1/秒²、LiftSpeed で横滑り 1 ラジアンあたり)")]
        private float _dihedral = AircraftAeroSpec.DefaultDihedral;

        [SerializeField, Min(0f), Tooltip("左右の翼の揚力の差 (m/s²) を、傾ける回転の加速 (ラジアン/秒²) に変える倍率。翼が長いほど大きい")]
        private float _wingRoll = AircraftAeroSpec.DefaultWingRoll;

        public AircraftAeroSpec ToSpec()
        {
            return new AircraftAeroSpec(
                _maxSpeed,
                _liftSpeed,
                _controlSpeed,
                _minControl,
                _pitchRate,
                _rollRate,
                _yawRate,
                _responsiveness,
                _alignment,
                _airBrakeDrag,
                _liftSlope,
                _criticalAngle,
                _postStallLift,
                _inducedDrag,
                _weathervane,
                _dihedral,
                _wingRoll);
        }

        public void CopyFrom(AircraftAeroSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _maxSpeed = spec.MaxSpeed;
            _liftSpeed = spec.LiftSpeed;
            _controlSpeed = spec.ControlSpeed;
            _minControl = spec.MinControl;
            _pitchRate = spec.PitchRate;
            _rollRate = spec.RollRate;
            _yawRate = spec.YawRate;
            _responsiveness = spec.Responsiveness;
            _alignment = spec.Alignment;
            _airBrakeDrag = spec.AirBrakeDrag;
            _liftSlope = spec.LiftSlope;
            _criticalAngle = spec.CriticalAngle;
            _postStallLift = spec.PostStallLift;
            _inducedDrag = spec.InducedDrag;
            _weathervane = spec.Weathervane;
            _dihedral = spec.Dihedral;
            _wingRoll = spec.WingRoll;
        }
    }
}

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

        [SerializeField, Min(0.01f), Tooltip("この速さ (m/s) で、翼が重さと同じだけ持ち上げる。これより遅いと沈む")]
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

        [SerializeField, Min(0f), Tooltip("横と上下へ流れる速さを、機首の向きへ寄せる強さ (1/秒)。大きいほど滑らずに曲がる")]
        private float _alignment = AircraftAeroSpec.DefaultAlignment;

        [SerializeField, Min(0f), Tooltip("エアブレーキを掛け切ったときに、空気の抵抗へ足す倍率")]
        private float _airBrakeDrag = AircraftAeroSpec.DefaultAirBrakeDrag;

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
                _airBrakeDrag);
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
        }
    }
}

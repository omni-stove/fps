using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class AircraftGroundSettings
    {
        [SerializeField, Range(0f, AircraftGroundSpec.MaxSteerAngleLimit), Tooltip("止まっているときに、ヨーを倒し切って前輪を向ける角度 (度)")]
        private float _maxSteerAngle = AircraftGroundSpec.DefaultMaxSteerAngle;

        [SerializeField, Range(0f, AircraftGroundSpec.MaxSteerAngleLimit), Tooltip("SteerFadeSpeed より速いときに、前輪を向けられる角度 (度)。MaxSteerAngle 以下にする")]
        private float _highSpeedSteerAngle = AircraftGroundSpec.DefaultHighSpeedSteerAngle;

        [SerializeField, Min(0.01f), Tooltip("この速さ (m/s) まで、前輪を向けられる角度を HighSpeedSteerAngle へ絞っていく")]
        private float _steerFadeSpeed = AircraftGroundSpec.DefaultSteerFadeSpeed;

        [SerializeField, Min(0f), Tooltip("エアブレーキを掛け切ったときの、車輪 1 つあたりのブレーキ (N·m)")]
        private float _brakeTorque = AircraftGroundSpec.DefaultBrakeTorque;

        [SerializeField, Min(0f), Tooltip("誰も操縦していないときに掛けておく、車輪 1 つあたりのブレーキ (N·m)")]
        private float _idleBrakeTorque = AircraftGroundSpec.DefaultIdleBrakeTorque;

        [SerializeField, Min(0.01f), Tooltip("浮いているときに、地面からの高さを測る下向きの距離 (m)。これより高いと高さは分からないとする")]
        private float _heightProbeDistance = AircraftGroundSpec.DefaultHeightProbeDistance;

        public AircraftGroundSpec ToSpec()
        {
            return new AircraftGroundSpec(
                _maxSteerAngle,
                _highSpeedSteerAngle,
                _steerFadeSpeed,
                _brakeTorque,
                _idleBrakeTorque,
                _heightProbeDistance);
        }

        public void CopyFrom(AircraftGroundSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _maxSteerAngle = spec.MaxSteerAngle;
            _highSpeedSteerAngle = spec.HighSpeedSteerAngle;
            _steerFadeSpeed = spec.SteerFadeSpeed;
            _brakeTorque = spec.BrakeTorque;
            _idleBrakeTorque = spec.IdleBrakeTorque;
            _heightProbeDistance = spec.HeightProbeDistance;
        }
    }
}

using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class AircraftEffectsSettings
    {
        [SerializeField, Min(0f), Tooltip("ピッチを倒し切ったときに昇降舵を回す角度 (度)")]
        private float _elevatorAngle = AircraftEffectsSpec.DefaultElevatorAngle;

        [SerializeField, Min(0f), Tooltip("ロールを倒し切ったときに補助翼を回す角度 (度)")]
        private float _aileronAngle = AircraftEffectsSpec.DefaultAileronAngle;

        [SerializeField, Min(0f), Tooltip("ヨーを倒し切ったときに方向舵を回す角度 (度)")]
        private float _rudderAngle = AircraftEffectsSpec.DefaultRudderAngle;

        [SerializeField, Min(0.01f), Tooltip("スロットルを閉じて止まっているときのエンジン音の高さ (1 で録った高さ)")]
        private float _minEnginePitch = AircraftEffectsSpec.DefaultMinEnginePitch;

        [SerializeField, Min(0.01f), Tooltip("スロットルを開き切って最高速で飛んでいるときのエンジン音の高さ")]
        private float _maxEnginePitch = AircraftEffectsSpec.DefaultMaxEnginePitch;

        [SerializeField, Range(0f, 1f), Tooltip("スロットルを閉じて止まっているときのエンジン音の大きさ")]
        private float _minEngineVolume = AircraftEffectsSpec.DefaultMinEngineVolume;

        [SerializeField, Range(0f, 1f), Tooltip("スロットルを開き切って最高速で飛んでいるときのエンジン音の大きさ")]
        private float _maxEngineVolume = AircraftEffectsSpec.DefaultMaxEngineVolume;

        [SerializeField, Range(0f, 1f), Tooltip("エンジン音の高さと大きさを決める割合のうち、速さが占める分 (残りはスロットル)")]
        private float _engineSpeedShare = AircraftEffectsSpec.DefaultEngineSpeedShare;

        [SerializeField, Min(0f), Tooltip("この G を超えている間だけ翼端からベイパーを出す (水平飛行が 1)")]
        private float _vaporLoadFactor = AircraftEffectsSpec.DefaultVaporLoadFactor;

        public AircraftEffectsSpec ToSpec()
        {
            return new AircraftEffectsSpec(
                _elevatorAngle,
                _aileronAngle,
                _rudderAngle,
                _minEnginePitch,
                _maxEnginePitch,
                _minEngineVolume,
                _maxEngineVolume,
                _engineSpeedShare,
                _vaporLoadFactor);
        }

        public void CopyFrom(AircraftEffectsSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _elevatorAngle = spec.ElevatorAngle;
            _aileronAngle = spec.AileronAngle;
            _rudderAngle = spec.RudderAngle;
            _minEnginePitch = spec.MinEnginePitch;
            _maxEnginePitch = spec.MaxEnginePitch;
            _minEngineVolume = spec.MinEngineVolume;
            _maxEngineVolume = spec.MaxEngineVolume;
            _engineSpeedShare = spec.EngineSpeedShare;
            _vaporLoadFactor = spec.VaporLoadFactor;
        }
    }
}

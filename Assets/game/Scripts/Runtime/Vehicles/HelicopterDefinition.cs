using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Helicopter", menuName = "Shooter/Helicopter Definition")]
    public sealed class HelicopterDefinition : ScriptableObject
    {
        [SerializeField, Min(0.01f), Tooltip("機体の質量 (kg)。Rigidbody に入れる。飛び方は変わらず、ぶつかったときの重さだけが変わる")]
        private float _mass = HelicopterSpec.DefaultMass;

        [SerializeField, Min(0.01f), Tooltip("前後左右へ傾け切ったときに出す水平の速さ (m/s)。これで頭打ちになる")]
        private float _maxSpeed = HelicopterSpec.DefaultMaxSpeed;

        [SerializeField, Min(0f), Tooltip("狙う水平の速さとの差を、毎秒どれだけ詰めようとするか (1/s)。大きいほど機敏に止まる・進む")]
        private float _horizontalResponse = HelicopterSpec.DefaultHorizontalResponse;

        [SerializeField, Min(0f), Tooltip("水平に掛ける加速の上限 (m/s²)")]
        private float _maxHorizontalAcceleration = HelicopterSpec.DefaultMaxHorizontalAcceleration;

        [SerializeField, Min(0f), Tooltip("コレクティブを上げ切ったときに昇る速さ (m/s)")]
        private float _maxClimbRate = HelicopterSpec.DefaultMaxClimbRate;

        [SerializeField, Min(0f), Tooltip("コレクティブを下げ切ったときに降りる速さ (m/s)")]
        private float _maxDescentRate = HelicopterSpec.DefaultMaxDescentRate;

        [SerializeField, Min(0f), Tooltip("狙う上下の速さとの差を、毎秒どれだけ詰めようとするか (1/s)")]
        private float _verticalResponse = HelicopterSpec.DefaultVerticalResponse;

        [SerializeField, Min(0f), Tooltip("ホバーの釣り合いに足し引きする上下の加速の上限 (m/s²)")]
        private float _maxVerticalAcceleration = HelicopterSpec.DefaultMaxVerticalAcceleration;

        [SerializeField, Min(0f), Tooltip("機体の原点から真下の地面までがこの高さ (m) より近いと地面効果が効く。0 なら効かない")]
        private float _groundEffectHeight = HelicopterSpec.DefaultGroundEffectHeight;

        [SerializeField, Min(0f), Tooltip("地面に触れているときに、上へ押す加速を何割増しにするか。高さに比例して 0 まで減る")]
        private float _groundEffectBoost = HelicopterSpec.DefaultGroundEffectBoost;

        [SerializeField, Range(0f, HelicopterSpec.MaxTiltAngleLimit), Tooltip("前後左右へ倒し切ったときに機体を傾ける角度 (度)")]
        private float _maxTiltAngle = HelicopterSpec.DefaultMaxTiltAngle;

        [SerializeField, Min(0f), Tooltip("狙う傾きへ戻す強さ (1/s²)")]
        private float _tiltStiffness = HelicopterSpec.DefaultTiltStiffness;

        [SerializeField, Min(0f), Tooltip("傾く回転を止める強さ (1/s)。2√TiltStiffness 前後で揺れずに収まる")]
        private float _tiltDamping = HelicopterSpec.DefaultTiltDamping;

        [SerializeField, Min(0f), Tooltip("回し切ったときの旋回の速さ (度/秒)")]
        private float _maxYawRate = HelicopterSpec.DefaultMaxYawRate;

        [SerializeField, Min(0f), Tooltip("狙う旋回の速さとの差を、毎秒どれだけ詰めようとするか (1/s)")]
        private float _yawResponse = HelicopterSpec.DefaultYawResponse;

        [SerializeField, Range(0.01f, HelicopterSpec.YawAimAngleLimit), Tooltip("視線が機首からこの角度 (度) 以上ずれたら旋回を回し切る")]
        private float _yawAimAngle = HelicopterSpec.DefaultYawAimAngle;

        [SerializeField, Min(0f), Tooltip("ローターが止まった所から回り切るまでの秒数。降りると同じ秒数で止まっていく")]
        private float _spoolSeconds = HelicopterSpec.DefaultSpoolSeconds;

        public float Mass
        {
            get => _mass;
            set => _mass = value;
        }

        public float MaxSpeed
        {
            get => _maxSpeed;
            set => _maxSpeed = value;
        }

        public HelicopterSpec ToSpec()
        {
            try
            {
                return new HelicopterSpec(
                    _mass,
                    _maxSpeed,
                    _horizontalResponse,
                    _maxHorizontalAcceleration,
                    _maxClimbRate,
                    _maxDescentRate,
                    _verticalResponse,
                    _maxVerticalAcceleration,
                    _groundEffectHeight,
                    _groundEffectBoost,
                    _maxTiltAngle,
                    _tiltStiffness,
                    _tiltDamping,
                    _maxYawRate,
                    _yawResponse,
                    _yawAimAngle,
                    _spoolSeconds);
            }
            catch (ArgumentException e)
            {
                throw Invalid(e);
            }
        }

        public void CopyFrom(HelicopterSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _mass = spec.Mass;
            _maxSpeed = spec.MaxSpeed;
            _horizontalResponse = spec.HorizontalResponse;
            _maxHorizontalAcceleration = spec.MaxHorizontalAcceleration;
            _maxClimbRate = spec.MaxClimbRate;
            _maxDescentRate = spec.MaxDescentRate;
            _verticalResponse = spec.VerticalResponse;
            _maxVerticalAcceleration = spec.MaxVerticalAcceleration;
            _groundEffectHeight = spec.GroundEffectHeight;
            _groundEffectBoost = spec.GroundEffectBoost;
            _maxTiltAngle = spec.MaxTiltAngle;
            _tiltStiffness = spec.TiltStiffness;
            _tiltDamping = spec.TiltDamping;
            _maxYawRate = spec.MaxYawRate;
            _yawResponse = spec.YawResponse;
            _yawAimAngle = spec.YawAimAngle;
            _spoolSeconds = spec.SpoolSeconds;
        }

        // 値の誤りにアセットの名前を添えるのは、どの機体のアセットを直せばよいかを例外だけで分かるようにするため。
        private ArgumentException Invalid(ArgumentException inner)
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed helicopter)" : name;
            return new ArgumentException($"helicopter '{source}' is invalid: {inner.Message}", inner);
        }
    }
}

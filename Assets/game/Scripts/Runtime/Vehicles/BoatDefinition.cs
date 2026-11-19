using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Boat", menuName = "Shooter/Boat Definition")]
    public sealed class BoatDefinition : ScriptableObject
    {
        [SerializeField, Min(0.01f), Tooltip("船の質量 (kg)。Rigidbody に入れる")]
        private float _mass = BoatSpec.DefaultMass;

        [SerializeField, Min(0.01f), Tooltip("浮力を測る船体の点を置く前後の長さ (m)。スクリューは船尾の真ん中に置く")]
        private float _length = BoatSpec.DefaultLength;

        [SerializeField, Min(0.01f), Tooltip("浮力を測る船体の点を置く左右の幅 (m)")]
        private float _width = BoatSpec.DefaultWidth;

        [SerializeField, Tooltip("船体の底の高さ (m)。船の原点から見た値で、浮力を測る点とスクリューをこの高さに置く")]
        private float _hullBottom = BoatSpec.DefaultHullBottom;

        [SerializeField, Min(0.01f), Tooltip("船体の高さ (m)。点がこれより深く沈んでも浮力は増えない")]
        private float _hullHeight = BoatSpec.DefaultHullHeight;

        [SerializeField, Min(0.01f), Tooltip("止まって浮いているときに底が沈む深さ (m)。船体の高さ以下にする")]
        private float _draft = BoatSpec.DefaultDraft;

        [SerializeField, Min(0f), Tooltip("上下の揺れを水が止める強さ (1/s)。大きいほど早く揺れが収まる")]
        private float _heaveDamping = BoatSpec.DefaultHeaveDamping;

        [SerializeField, Min(0f), Tooltip("前後に進むときの水の抵抗 (1/s)")]
        private float _forwardDrag = BoatSpec.DefaultForwardDrag;

        [SerializeField, Min(0f), Tooltip("横へ滑るときの水の抵抗 (1/s)。前後より大きくすると曲がるときに横へ流れにくい")]
        private float _sideDrag = BoatSpec.DefaultSideDrag;

        [SerializeField, Min(0f), Tooltip("スロットルを開き切ったときの、スクリューが押す加速 (m/s²)")]
        private float _thrust = BoatSpec.DefaultThrust;

        [SerializeField, Min(0f), Tooltip("前へ進むときに推力を掛ける上限の速さ (m/s)")]
        private float _maxForwardSpeed = BoatSpec.DefaultMaxForwardSpeed;

        [SerializeField, Min(0f), Tooltip("後ろへ進むときに推力を掛ける上限の速さ (m/s)")]
        private float _maxReverseSpeed = BoatSpec.DefaultMaxReverseSpeed;

        [SerializeField, Min(0f), Tooltip("舵を切り切ったときの曲がる速さ (度/秒)")]
        private float _turnRate = BoatSpec.DefaultTurnRate;

        [SerializeField, Min(0.01f), Tooltip("この速さ (m/s) で舵が効き切る。遅いときもスロットルを開けば効く")]
        private float _turnSpeed = BoatSpec.DefaultTurnSpeed;

        [SerializeField, Min(0f), Tooltip("曲がる速さを舵の目標へ寄せる速さ (1/s)")]
        private float _turnResponsiveness = BoatSpec.DefaultTurnResponsiveness;

        public float Mass
        {
            get => _mass;
            set => _mass = value;
        }

        public float Length
        {
            get => _length;
            set => _length = value;
        }

        public float Width
        {
            get => _width;
            set => _width = value;
        }

        public float HullBottom
        {
            get => _hullBottom;
            set => _hullBottom = value;
        }

        public float HullHeight
        {
            get => _hullHeight;
            set => _hullHeight = value;
        }

        public float Draft
        {
            get => _draft;
            set => _draft = value;
        }

        public float HeaveDamping
        {
            get => _heaveDamping;
            set => _heaveDamping = value;
        }

        public float ForwardDrag
        {
            get => _forwardDrag;
            set => _forwardDrag = value;
        }

        public float SideDrag
        {
            get => _sideDrag;
            set => _sideDrag = value;
        }

        public float Thrust
        {
            get => _thrust;
            set => _thrust = value;
        }

        public float MaxForwardSpeed
        {
            get => _maxForwardSpeed;
            set => _maxForwardSpeed = value;
        }

        public float MaxReverseSpeed
        {
            get => _maxReverseSpeed;
            set => _maxReverseSpeed = value;
        }

        public float TurnRate
        {
            get => _turnRate;
            set => _turnRate = value;
        }

        public float TurnSpeed
        {
            get => _turnSpeed;
            set => _turnSpeed = value;
        }

        public float TurnResponsiveness
        {
            get => _turnResponsiveness;
            set => _turnResponsiveness = value;
        }

        public BoatSpec ToSpec()
        {
            try
            {
                return new BoatSpec(
                    _mass,
                    _length,
                    _width,
                    _hullBottom,
                    _hullHeight,
                    _draft,
                    _heaveDamping,
                    _forwardDrag,
                    _sideDrag,
                    _thrust,
                    _maxForwardSpeed,
                    _maxReverseSpeed,
                    _turnRate,
                    _turnSpeed,
                    _turnResponsiveness);
            }
            catch (ArgumentException e)
            {
                // 値の誤りにアセットの名前を添えるのは、どの船のアセットを直せばよいかを例外だけで分かるようにするため。
                var source = string.IsNullOrEmpty(name) ? "(unnamed boat)" : name;
                throw new ArgumentException($"boat '{source}' is invalid: {e.Message}", e);
            }
        }

        public void CopyFrom(BoatSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _mass = spec.Mass;
            _length = spec.Length;
            _width = spec.Width;
            _hullBottom = spec.HullBottom;
            _hullHeight = spec.HullHeight;
            _draft = spec.Draft;
            _heaveDamping = spec.HeaveDamping;
            _forwardDrag = spec.ForwardDrag;
            _sideDrag = spec.SideDrag;
            _thrust = spec.Thrust;
            _maxForwardSpeed = spec.MaxForwardSpeed;
            _maxReverseSpeed = spec.MaxReverseSpeed;
            _turnRate = spec.TurnRate;
            _turnSpeed = spec.TurnSpeed;
            _turnResponsiveness = spec.TurnResponsiveness;
        }
    }
}

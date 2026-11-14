using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 敵を引く・物と物をつなぐはキットでは作らない。ゲームが別の AbilityDefinition として足す。
    [CreateAssetMenu(fileName = "GrappleAbility", menuName = "Fps/Abilities/Grapple Ability")]
    public sealed class GrappleAbilityDefinition : AbilityDefinition
    {
        [SerializeField, Min(0f), Tooltip("撃てる最短の距離 (m)。目から刺さる点までがこれより近ければ刺さらない")]
        private float _minRange = GrappleSpec.DefaultMinRange;

        [SerializeField, Min(0f), Tooltip("縄が届く距離 (m)。目から刺さる点までで測る")]
        private float _maxRange = GrappleSpec.DefaultMaxRange;

        [SerializeField, Min(0f), Tooltip("引き寄せる最高の速さ (m/s)")]
        private float _pullSpeed = GrappleSpec.DefaultPullSpeed;

        [SerializeField, Min(0f), Tooltip("最高の速さまで速める加速度 (m/s²)")]
        private float _pullAcceleration = GrappleSpec.DefaultPullAcceleration;

        [SerializeField, Min(0f), Tooltip("止まる位置までこの距離 (m) 以内で着いたとする")]
        private float _arriveDistance = GrappleSpec.DefaultArriveDistance;

        [SerializeField, Min(0f), Tooltip("引き寄せ続ける秒数の上限。過ぎたら離す")]
        private float _maxPullSeconds = GrappleSpec.DefaultMaxPullSeconds;

        [SerializeField, Min(0f), Tooltip("壁に着いたときの、体の中心から刺さった点 (手) までの高さ (m)")]
        private float _hangHeight = GrappleSpec.DefaultHangHeight;

        [SerializeField, Min(0f), Tooltip("壁の上の縁までこの距離 (m) 以内に刺さったら縁へ寄せる。0 なら寄せない")]
        private float _edgeSnapDistance = GrappleSpec.DefaultEdgeSnapDistance;

        [SerializeField, Min(0f), Tooltip("着いて崖のぼりをしないとき、縁で止まっている秒数")]
        private float _hangSeconds = GrappleSpec.DefaultHangSeconds;

        [SerializeField, Tooltip("掴める面のレイヤー。照準の先で最初に当たった面がこのどれかでなければ刺さらない")]
        private LayerMask _grabbableLayers = Physics.DefaultRaycastLayers;

        [SerializeField, Tooltip("掴める面のタグ。空ならタグは問わない。1 つ以上あれば、レイヤーに加えてこのどれかのタグも要る")]
        private string[] _grabbableTags = Array.Empty<string>();

        public float MinRange
        {
            get => _minRange;
            set => _minRange = value;
        }

        public float MaxRange
        {
            get => _maxRange;
            set => _maxRange = value;
        }

        public float PullSpeed
        {
            get => _pullSpeed;
            set => _pullSpeed = value;
        }

        public float PullAcceleration
        {
            get => _pullAcceleration;
            set => _pullAcceleration = value;
        }

        public float ArriveDistance
        {
            get => _arriveDistance;
            set => _arriveDistance = value;
        }

        public float MaxPullSeconds
        {
            get => _maxPullSeconds;
            set => _maxPullSeconds = value;
        }

        public float HangHeight
        {
            get => _hangHeight;
            set => _hangHeight = value;
        }

        public float EdgeSnapDistance
        {
            get => _edgeSnapDistance;
            set => _edgeSnapDistance = value;
        }

        public float HangSeconds
        {
            get => _hangSeconds;
            set => _hangSeconds = value;
        }

        public LayerMask GrabbableLayers
        {
            get => _grabbableLayers;
            set => _grabbableLayers = value;
        }

        // 渡した配列を後から書き換えられても能力の判定が変わらないよう、写しを持つ。
        public string[] GrabbableTags
        {
            get => (string[])(_grabbableTags ?? Array.Empty<string>()).Clone();
            set => _grabbableTags = value != null ? (string[])value.Clone() : Array.Empty<string>();
        }

        public GrappleSpec ToGrappleSpec()
        {
            try
            {
                return new GrappleSpec(
                    _minRange, _maxRange, _pullSpeed, _pullAcceleration, _arriveDistance, _maxPullSeconds, _hangHeight, _edgeSnapDistance, _hangSeconds);
            }
            catch (ArgumentException e)
            {
                throw new AbilityDefinitionException($"grapple ability '{Id}' is invalid: {e.Message}", e);
            }
        }

        public override IAbility CreateAbility()
        {
            var spec = ToGrappleSpec();
            GrappleSurfaceFilter surfaces;
            try
            {
                surfaces = new GrappleSurfaceFilter(_grabbableLayers, GrabbableTags);
            }
            catch (ArgumentException e)
            {
                throw new AbilityDefinitionException($"grapple ability '{Id}' has invalid grabbable tags: {e.Message}", e);
            }

            return new GrappleAbility(spec, surfaces);
        }
    }
}

using System;
using UnityEngine;

namespace Fps.Runtime
{
    // NavMesh の無い場所 (射撃場) でもボスの段階による速さの変化を見せられるよう、経路を引かずに左右へ往復するだけの動き。
    // 速さは同じ GameObject の BossController の段階の倍率を毎フレーム読み、段階の切り替えをこちらで覚えない。
    [DisallowMultipleComponent]
    public sealed class BossStrafeMotion : MonoBehaviour
    {
        public const float DefaultSpeed = 1.5f;
        public const float DefaultHalfWidth = 3f;

        [SerializeField, Min(0f), Tooltip("倍率 1 のときの速さ (m/s)")]
        private float _speed = DefaultSpeed;

        [SerializeField, Min(0f), Tooltip("置いた位置から左右へ動く幅の半分 (m)")]
        private float _halfWidth = DefaultHalfWidth;

        private BossController _boss;
        private Damageable _self;
        private Vector3 _center;
        private Vector3 _axis;

        // 往復 1 回分 (幅の 4 倍) の中で進んだ道のり。端での折り返しを、1 フレームで大きく進んでも同じ式で求められるように持つ。
        private float _travelled;
        private bool _initialized;

        public float Speed => _speed;

        public float HalfWidth => _halfWidth;

        public float Offset
        {
            get
            {
                var span = _halfWidth * 2f;
                var along = Mathf.Repeat(_travelled + _halfWidth, span * 2f);
                return (along <= span ? along : (span * 2f) - along) - _halfWidth;
            }
        }

        public void Configure(float speed, float halfWidth)
        {
            if (!(speed >= 0f) || float.IsInfinity(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be non-negative and finite");
            }

            if (!(halfWidth >= 0f) || float.IsInfinity(halfWidth))
            {
                throw new ArgumentOutOfRangeException(nameof(halfWidth), halfWidth, "halfWidth must be non-negative and finite");
            }

            _speed = speed;
            _halfWidth = halfWidth;
        }

        // Edit Mode のテストで Update を待たずに進められるよう public にする。
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            EnsureInitialized();
            if (_halfWidth <= 0f || (_self != null && _self.IsDead))
            {
                return;
            }

            var multiplier = _boss != null ? _boss.MoveSpeedMultiplier : 1f;
            _travelled = Mathf.Repeat(_travelled + (_speed * multiplier * deltaSeconds), _halfWidth * 4f);
            transform.position = _center + (_axis * Offset);
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _boss = GetComponent<BossController>();
            _self = GetComponent<Damageable>();
            _center = transform.position;
            _axis = transform.right;
            _initialized = true;
        }
    }
}

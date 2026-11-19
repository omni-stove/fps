using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 蝶番の軸と向きを角度と別にここで持つのは、それが模型の作りで決まる組み付けであり、機体ごとに調整する値 (AircraftEffectsSettings) ではないため。
    [Serializable]
    public sealed class AircraftControlSurface
    {
        [SerializeField, Tooltip("回す舵面。空なら何もしない")]
        private Transform _transform;

        [SerializeField, Tooltip("どの操作で回す舵面か")]
        private AircraftControlSurfaceRole _role;

        [SerializeField, Tooltip("舵面の親から見た蝶番の軸")]
        private Vector3 _hingeAxis = Vector3.right;

        [SerializeField, Tooltip("逆向きに回す。左右の補助翼の片方に付ける")]
        private bool _reversed;

        public AircraftControlSurface()
        {
        }

        public AircraftControlSurface(Transform transform, AircraftControlSurfaceRole role, Vector3 hingeAxis, bool reversed = false)
        {
            if (!Enum.IsDefined(typeof(AircraftControlSurfaceRole), role))
            {
                throw new ArgumentOutOfRangeException(nameof(role), role, "role is not a defined AircraftControlSurfaceRole");
            }

            if (!(hingeAxis.sqrMagnitude > 0f) || float.IsInfinity(hingeAxis.sqrMagnitude))
            {
                throw new ArgumentOutOfRangeException(nameof(hingeAxis), hingeAxis, "hingeAxis must be a non-zero finite vector");
            }

            _transform = transform;
            _role = role;
            _hingeAxis = hingeAxis;
            _reversed = reversed;
        }

        public Transform Transform => _transform;

        public AircraftControlSurfaceRole Role => _role;

        // 長さ 0 の軸を右に読み替えるのは、インスペクターで消してしまった欄でも舵面が NaN の回転で消えないようにするため。
        public Vector3 HingeAxis => _hingeAxis.sqrMagnitude > 0f ? _hingeAxis.normalized : Vector3.right;

        public bool Reversed => _reversed;
    }
}

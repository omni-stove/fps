using System;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class CarWheel
    {
        [SerializeField]
        private WheelCollider _collider;

        [SerializeField, Tooltip("車輪の回りと向きを写す見た目。空なら写さない")]
        private Transform _visual;

        [SerializeField, Tooltip("ハンドルで向きを変えるか")]
        private bool _steers;

        [SerializeField, Tooltip("エンジンの力で回すか")]
        private bool _drives;

        public CarWheel(WheelCollider collider, Transform visual, bool steers, bool drives)
        {
            _collider = collider != null ? collider : throw new ArgumentNullException(nameof(collider));
            _visual = visual;
            _steers = steers;
            _drives = drives;
        }

        public WheelCollider Collider => _collider;

        public Transform Visual => _visual;

        public bool Steers => _steers;

        public bool Drives => _drives;
    }
}

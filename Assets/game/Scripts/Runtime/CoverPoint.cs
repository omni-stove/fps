using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 動かない地形に置く前提なので、Play 中に動かしても選ぶ位置は変わらない。
    [DisallowMultipleComponent]
    public sealed class CoverPoint : MonoBehaviour
    {
        [SerializeField, Tooltip("壁の高さの区分。Low ならしゃがんで隠れ立って覗く、High なら立って隠れ PeekOffset へ出て覗く")]
        private CoverHeight _height = CoverHeight.Low;

        [SerializeField, Tooltip("覗くときに立つ位置。この GameObject のローカル座標でのずれ。0 ならその場で立ち上がる")]
        private Vector3 _peekOffset = Vector3.zero;

        public CoverHeight Height
        {
            get => _height;
            set => _height = value;
        }

        public Vector3 PeekOffset
        {
            get => _peekOffset;
            set => _peekOffset = value;
        }

        public Vector3 Position => transform.position;

        public Vector3 Facing => transform.forward;

        public Vector3 PeekPosition => transform.position + (transform.rotation * _peekOffset);

        public void SetPeekOffsetWorld(Vector3 worldOffset)
        {
            _peekOffset = Quaternion.Inverse(transform.rotation) * worldOffset;
        }

        public bool TryToCore(Vector3 position, out Shooter.Core.CoverPoint point, out string error)
        {
            try
            {
                point = new Shooter.Core.CoverPoint(position.ToNumerics(), Facing.ToNumerics(), _height);
                error = null;
                return true;
            }
            catch (ArgumentOutOfRangeException e)
            {
                point = default;
                error = e.Message;
                return false;
            }
        }

        private void OnDrawGizmos()
        {
            var position = transform.position;
            Gizmos.color = _height == CoverHeight.Low ? Color.cyan : Color.blue;
            Gizmos.DrawWireSphere(position + (Vector3.up * 0.1f), 0.25f);
            Gizmos.DrawLine(position + (Vector3.up * 0.1f), position + (Vector3.up * 0.1f) + Facing);

            var peek = PeekPosition;
            if ((peek - position).sqrMagnitude > 1e-6f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(position + (Vector3.up * 0.1f), peek + (Vector3.up * 0.1f));
                Gizmos.DrawWireSphere(peek + (Vector3.up * 0.1f), 0.15f);
            }
        }
    }
}

using UnityEngine;

namespace Shooter.Runtime
{
    // 貼り先の子にしないのは、貼り先が消えると痕もプールから消えて使い回せなくなり、貼り先の伸縮で痕の形が歪むため。
    // 代わりに貼り先から見た位置と向きを覚え、貼り先が動いたら痕を置き直す。
    public sealed class DecalAttachment
    {
        private Collider _collider;
        private Transform _anchor;
        private bool _anchored;
        private Vector3 _localPoint;
        private Quaternion _localRotation = Quaternion.identity;
        private Vector3 _offset;
        private Matrix4x4 _lastAnchorMatrix;

        public Collider Collider => _collider;

        // 消された Collider も == null になるので、初めから貼り先が無かった痕 (剥がさない) と分けるために _anchored を持つ。
        public bool IsDetached => _anchored && (_collider == null || !_collider.enabled || !_collider.gameObject.activeInHierarchy);

        // point は面の上の点、offset は痕の向きから見た面からの浮かせ量。浮かせ量は貼り先の伸縮に合わせず、常に同じ距離にする。
        public void Attach(Collider collider, Vector3 point, Quaternion rotation, Vector3 offset)
        {
            _collider = collider;
            _anchored = !ReferenceEquals(collider, null);
            _anchor = collider != null ? collider.transform : null;
            _offset = offset;
            if (_anchor == null)
            {
                _localPoint = point;
                _localRotation = rotation;
                return;
            }

            _lastAnchorMatrix = _anchor.localToWorldMatrix;
            _localPoint = _anchor.InverseTransformPoint(point);
            _localRotation = Quaternion.Inverse(_anchor.rotation) * rotation;
        }

        public void Clear()
        {
            _collider = null;
            _anchor = null;
            _anchored = false;
        }

        public Pose WorldPose()
        {
            if (_anchor == null)
            {
                return new Pose(_localPoint + (_localRotation * _offset), _localRotation);
            }

            var rotation = _anchor.rotation * _localRotation;
            return new Pose(_anchor.TransformPoint(_localPoint) + (rotation * _offset), rotation);
        }

        // 止まっている貼り先の痕まで毎フレーム置き直すと描画の境界を無駄に計算し直すので、貼り先が動いたときだけ置き直す。
        public bool Follow(Transform decal)
        {
            if (_anchor == null || decal == null)
            {
                return false;
            }

            var matrix = _anchor.localToWorldMatrix;
            if (matrix == _lastAnchorMatrix)
            {
                return false;
            }

            _lastAnchorMatrix = matrix;
            var pose = WorldPose();
            decal.SetPositionAndRotation(pose.position, pose.rotation);
            return true;
        }
    }
}

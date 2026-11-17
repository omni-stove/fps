using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class ThrownGrenade
    {
        private Transform _carrier;
        private Vector3 _carrierLocalPosition;
        private Quaternion _carrierLocalRotation;
        private bool _stuck;

        public bool IsStuck => _stuck;

        public Transform StuckTo => _stuck ? _carrier : null;

        partial void Stick(Collision collision)
        {
            if (_stuck)
            {
                return;
            }

            var other = collision.collider;
            Vector3 point;
            Vector3 normal;
            if (collision.contactCount > 0)
            {
                var contact = collision.GetContact(0);
                point = contact.point;
                normal = contact.normal;
            }
            else
            {
                point = transform.position;
                normal = Vector3.up;
            }

            StickTo(other, point, normal);
        }

        internal bool StickTo(Collider other, Vector3 point, Vector3 normal)
        {
            if (_stuck || !_armed || _exploded || other == null || !Accepts(other))
            {
                return false;
            }

            // 衝突の通知は跳ね返した後に来るので、跳ねた先ではなく触れた点の外側へ戻して止める
            transform.position = point + (normal.normalized * BodyRadius);
            var carrier = other.transform;
            _carrier = carrier;
            _carrierLocalPosition = carrier.InverseTransformPoint(transform.position);
            _carrierLocalRotation = Quaternion.Inverse(carrier.rotation) * transform.rotation;
            _stuck = true;

            var body = GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = true;

            // 体に付いた球が、くっついた相手の CharacterController や他の物を押し返さないよう当たりを外す
            SetOwnCollidersEnabled(false);
            return true;
        }

        private bool Accepts(Collider other)
        {
            switch (_stickiness)
            {
                case GrenadeStickiness.Surfaces:
                    return !IsCharacter(other);
                case GrenadeStickiness.SurfacesAndCharacters:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsCharacter(Collider other)
        {
            return Hitbox.FindFor(other) != null || other.GetComponentInParent<Damageable>(true) != null;
        }

        private void LateUpdate()
        {
            FollowCarrier();
        }

        internal void FollowCarrier()
        {
            if (!_stuck || _exploded)
            {
                return;
            }

            if (_carrier == null || !_carrier.gameObject.activeInHierarchy)
            {
                Unstick();
                return;
            }

            transform.SetPositionAndRotation(
                _carrier.TransformPoint(_carrierLocalPosition), _carrier.rotation * _carrierLocalRotation);
        }

        private void Unstick()
        {
            _stuck = false;
            _carrier = null;
            SetOwnCollidersEnabled(true);
            var body = GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void SetOwnCollidersEnabled(bool enabled)
        {
            GetComponentsInChildren(true, _ownColliders);
            foreach (var own in _ownColliders)
            {
                own.enabled = enabled;
            }
        }
    }
}

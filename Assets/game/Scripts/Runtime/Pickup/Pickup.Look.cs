using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class Pickup
    {
        private const int LookHitCapacity = 16;

        private static readonly RaycastHit[] _lookHits = new RaycastHit[LookHitCapacity];
        private static int _lookFrame = -1;
        private static Damageable _lookViewer;
        private static Pickup _lookResult;

        // 照準の線が見た目に当たらないときの候補。誰かが範囲にいる物だけを持ち、Edit Mode でも HandleEnter から入る。
        private static readonly List<Pickup> _occupied = new List<Pickup>();

        public bool IsLookedAtBy(Damageable viewer)
        {
            return viewer != null && !_collected && _collectMode == PickupCollectMode.Look && LookedAt(viewer) == this;
        }

        public static Pickup LookedAt(Damageable viewer)
        {
            if (viewer == null)
            {
                throw new ArgumentNullException(nameof(viewer));
            }

            // 範囲にいる物がそれぞれ同じ線を引くので、Play 中は 1 フレームに 1 本だけ引いて使い回す。
            if (Application.isPlaying && _lookFrame == Time.frameCount && _lookViewer == viewer)
            {
                return _lookResult;
            }

            var view = ViewOf(viewer);
            Pickup result = null;
            if (view != null)
            {
                var hit = CastLook(view.position, view.forward, viewer);
                result = hit != null && hit.IsInside(viewer) ? hit : NearestInView(view.position, view.forward, viewer);
            }

            _lookFrame = Time.frameCount;
            _lookViewer = viewer;
            _lookResult = result;
            return result;
        }

        public static Transform ViewOf(Damageable viewer)
        {
            if (viewer == null)
            {
                throw new ArgumentNullException(nameof(viewer));
            }

            var weapons = viewer.GetComponentInChildren<WeaponController>(true);
            if (weapons != null)
            {
                return weapons.transform;
            }

            var main = Camera.main;
            return main != null ? main.transform : null;
        }

        private static Pickup CastLook(Vector3 origin, Vector3 direction, Damageable viewer)
        {
            var count = Physics.RaycastNonAlloc(
                origin, direction, _lookHits, LookRayLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Array.Sort(_lookHits, 0, count, HitDistanceComparer.Instance);
            for (var i = 0; i < count; i++)
            {
                var collider = _lookHits[i].collider;
                if (collider == null || collider.GetComponentInParent<Damageable>() == viewer)
                {
                    continue;
                }

                var pickup = collider.GetComponentInParent<Pickup>();
                if (pickup != null && pickup._lookTarget == collider)
                {
                    return pickup.IsAvailable && pickup._collectMode == PickupCollectMode.Look ? pickup : null;
                }

                if (!collider.isTrigger)
                {
                    return null;
                }
            }

            return null;
        }

        private static Pickup NearestInView(Vector3 origin, Vector3 forward, Damageable viewer)
        {
            Pickup best = null;
            var bestAngle = float.PositiveInfinity;
            for (var i = _occupied.Count - 1; i >= 0; i--)
            {
                var pickup = _occupied[i];
                if (pickup == null)
                {
                    _occupied.RemoveAt(i);
                    continue;
                }

                if (!pickup.IsAvailable || pickup._collectMode != PickupCollectMode.Look || !pickup.IsInside(viewer))
                {
                    continue;
                }

                var point = pickup.LookPoint;
                var angle = Vector3.Angle(forward, point - origin);
                if (angle > pickup._lookAngleDegrees || angle >= bestAngle || !CanSee(origin, point, viewer))
                {
                    continue;
                }

                best = pickup;
                bestAngle = angle;
            }

            return best;
        }

        private Vector3 LookPoint
        {
            get
            {
                if (_lookTarget != null)
                {
                    return _lookTarget.transform.TransformPoint(_lookTarget.center);
                }

                return _visual != null ? _visual.position : transform.position;
            }
        }

        private static bool CanSee(Vector3 origin, Vector3 point, Damageable viewer)
        {
            var toPoint = point - origin;
            var distance = toPoint.magnitude;
            if (distance < 1e-4f)
            {
                return true;
            }

            var count = Physics.RaycastNonAlloc(
                origin, toPoint / distance, _lookHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var collider = _lookHits[i].collider;
                if (collider != null && collider.GetComponentInParent<Damageable>() != viewer)
                {
                    return false;
                }
            }

            return true;
        }

        private static float ClampLookAngle(float degrees)
        {
            return float.IsNaN(degrees) ? 0f : Mathf.Clamp(degrees, 0f, MaxLookAngleDegrees);
        }

        private sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();

            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }
    }
}

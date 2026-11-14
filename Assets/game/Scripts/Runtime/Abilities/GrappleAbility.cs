using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed class GrappleAbility : IAbility
    {
        private const int MaxRopeHits = 16;

        // 縁を探す Ray を面のわずかに奥から下ろす。面ちょうどから下ろすと、面の上を滑って当たらないことがある。
        private const float EdgeProbeInset = 0.05f;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxRopeHits];
        private readonly GrappleSpec _spec;
        private readonly GrappleSurfaceFilter _surfaces;
        private readonly GrappleMovement _movement;
        private GrappleAim? _pendingAim;
        private Collider _surface;
        private Vector3 _localAnchor;
        private Vector3 _localNormal;
        private bool _wall;
        private bool _arrivalHandled;
        private float _hangRemaining;

        public GrappleAbility(GrappleSpec spec, GrappleSurfaceFilter surfaces)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
            _movement = new GrappleMovement(spec);
        }

        public GrappleSpec Spec => _spec;

        public GrappleSurfaceFilter Surfaces => _surfaces;

        public GrappleMovement Movement => _movement;

        public GrappleMiss LastMiss { get; private set; }

        public Collider Surface => _surface;

        public bool IsAttached => _surface != null && (_movement.IsPulling || _movement.IsHanging);

        public Vector3 Anchor => _surface != null ? _surface.transform.TransformPoint(_localAnchor) : Vector3.zero;

        public bool CanUse(AbilityContext context)
        {
            _pendingAim = null;
            var mover = context.Mover;
            if (mover == null || !mover.IsInitialized)
            {
                return Refuse(GrappleMiss.NoMover, null, null, context.Owner);
            }

            ResolveAim(context.Owner, out var origin, out var direction);
            var miss = TryAim(context.Owner.transform, origin, direction, out var aim);
            if (miss != GrappleMiss.None)
            {
                return Refuse(miss, aim.Surface != null ? aim.Surface.name : null, aim.Surface != null ? aim.Distance : (float?)null, context.Owner);
            }

            LastMiss = GrappleMiss.None;
            _pendingAim = aim;
            return true;
        }

        public void Begin(AbilityContext context)
        {
            // CanUse と Begin は同じ TryUse の中で続けて呼ばれるので、狙った先はそのときのものを使う。
            var aim = _pendingAim ?? throw new InvalidOperationException("Begin was called without a successful CanUse");
            _pendingAim = null;

            _surface = aim.Surface;
            var surface = _surface.transform;
            _localAnchor = surface.InverseTransformPoint(aim.Point);
            _localNormal = surface.InverseTransformDirection(aim.Normal);
            _wall = GrappleSpec.IsWall(aim.Normal.ToNumerics());
            _arrivalHandled = false;
            _hangRemaining = _spec.HangSeconds;
            Debug.Log(GrappleLog.Attached(_surface.name, aim.Distance, aim.SnappedToEdge), context.Owner);

            _movement.Arm(HangTarget(context.Mover), _wall ? aim.Normal : Vector3.zero);
            if (!context.IsEffective)
            {
                // 効果が出るまでは引かない。横取りの登録だけ先に済ませ、IsActive は効果が出た Tick から立てる。
                _movement.Finish();
            }

            context.OverrideMovement(_movement);
        }

        public bool Tick(AbilityContext context, float deltaSeconds)
        {
            if (!context.IsEffective)
            {
                return true;
            }

            var mover = context.Mover;
            if (context.EffectStartedThisFrame && !_movement.IsPulling && !_movement.HasArrived && _movement.Ended == GrappleEnd.None)
            {
                _movement.Arm(HangTarget(mover), _wall ? WorldNormal() : Vector3.zero);
            }

            if ((_movement.IsPulling || _movement.IsHanging) && !IsSurfaceUsable())
            {
                ReleaseAndLog(GrappleEnd.Lost);
                return false;
            }

            if (_movement.IsPulling)
            {
                _movement.Target = HangTarget(mover);
                return true;
            }

            if (_movement.HasArrived && !_arrivalHandled)
            {
                _arrivalHandled = true;
                Debug.Log(GrappleLog.Arrived(_movement.Pull.ElapsedSeconds, _wall), context.Owner);
                if (_wall && _movement.IsHanging && TryClimb(context))
                {
                    return false;
                }

                // 床や段の上に刺さったなら、着いた所にそのまま立つ。止まって待つのは壁と天井だけ。
                if (!_wall && _movement.IsHanging && WorldNormal().y >= GrappleSpec.FloorNormalY)
                {
                    _movement.Finish();
                    return false;
                }
            }

            if (_movement.IsHanging)
            {
                _hangRemaining -= deltaSeconds;
                if (_hangRemaining > 0f)
                {
                    return true;
                }

                ReleaseAndLog(GrappleEnd.Dropped);
                return false;
            }

            if (_movement.Ended != GrappleEnd.None)
            {
                Debug.Log(GrappleLog.Released(_movement.Ended, _movement.ReleaseVelocity.magnitude), context.Owner);
            }

            return false;
        }

        public void End(AbilityContext context, bool interrupted)
        {
            if (_movement.IsPulling || _movement.IsHanging)
            {
                ReleaseAndLog(GrappleEnd.Interrupted);
            }

            _movement.Finish();
            context.StopMovementOverride();
            _surface = null;
            _pendingAim = null;
        }

        // 照準の先 (origin から direction へ MaxRange まで) で、owner の体を除いて最初に当たった面に刺さるか。
        // 掴めない面が手前にあれば、その奥に掴める面があっても刺さらない (縄は面を抜けない)。
        public GrappleMiss TryAim(Transform owner, Vector3 origin, Vector3 direction, out GrappleAim aim)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            aim = default;
            if (direction.sqrMagnitude < 1e-8f)
            {
                return GrappleMiss.OutOfRange;
            }

            direction.Normalize();
            if (!TryNearestHit(owner, origin, direction, _spec.MaxRange, out var hit))
            {
                return GrappleMiss.OutOfRange;
            }

            aim = new GrappleAim(hit.collider, hit.point, hit.normal, hit.distance, false);
            if (hit.distance < _spec.MinRange)
            {
                return GrappleMiss.TooClose;
            }

            if (!_surfaces.Allows(hit.collider.gameObject))
            {
                return GrappleMiss.NotGrabbable;
            }

            if (GrappleSpec.IsWall(hit.normal.ToNumerics()) && TryFindEdge(hit, out var edgeY))
            {
                aim = new GrappleAim(hit.collider, new Vector3(hit.point.x, edgeY, hit.point.z), hit.normal, hit.distance, true);
            }

            return GrappleMiss.None;
        }

        // 一人称・三人称とも、武器と同じく目から画面中央が指す点へ撃つ。ViewRig が無ければ本体の正面へ。
        private void ResolveAim(GameObject owner, out Vector3 origin, out Vector3 direction)
        {
            var rig = owner.GetComponent<ViewRig>();
            if (rig != null && rig.IsInitialized)
            {
                rig.ResolveAim(_spec.MaxRange, out origin, out direction);
                return;
            }

            origin = owner.transform.position;
            direction = owner.transform.forward;
        }

        private bool TryNearestHit(Transform owner, Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            var count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var found = false;
            nearest = default;
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(owner))
                {
                    continue;
                }

                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }

            Array.Clear(_hits, 0, count);
            return found;
        }

        // 当たった面の少し奥の上から、同じコライダーにだけ Ray を下ろして上の縁を探す。始点がコライダーの中 (縁がもっと上) なら当たらない。
        private bool TryFindEdge(RaycastHit hit, out float edgeY)
        {
            edgeY = 0f;
            var reach = _spec.EdgeSnapDistance;
            if (reach <= 0f)
            {
                return false;
            }

            var into = -Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
            var start = hit.point + (into * EdgeProbeInset) + (Vector3.up * reach);
            if (!hit.collider.Raycast(new Ray(start, Vector3.down), out var top, reach))
            {
                return false;
            }

            if (top.normal.y < GrappleSpec.FloorNormalY)
            {
                return false;
            }

            edgeY = top.point.y;
            return true;
        }

        private bool TryClimb(AbilityContext context)
        {
            var mover = context.Mover;
            if (mover == null || mover.MantleRule != Mantle.On)
            {
                return false;
            }

            // 登れないなら縁で止まったままにするので、横取りを外す前に調べる。外してから断られると、止まらずに落ちる。
            if (!mover.ProbeMantle().CanMantle)
            {
                Debug.Log(GrappleLog.Climb(false), context.Owner);
                return false;
            }

            _movement.Finish();
            context.StopMovementOverride();
            var started = mover.TryStartMantleWhileHanging();
            Debug.Log(GrappleLog.Climb(started), context.Owner);
            return true;
        }

        private void ReleaseAndLog(GrappleEnd reason)
        {
            if (!_movement.IsPulling && !_movement.IsHanging)
            {
                return;
            }

            _movement.Release(reason);
            Debug.Log(GrappleLog.Released(reason, _movement.ReleaseVelocity.magnitude));
        }

        private bool IsSurfaceUsable()
        {
            return _surface != null && _surface.enabled && _surface.gameObject.activeInHierarchy;
        }

        private Vector3 WorldNormal()
        {
            return _surface != null ? _surface.transform.TransformDirection(_localNormal).normalized : Vector3.up;
        }

        private Vector3 HangTarget(PlayerController mover)
        {
            var controller = mover != null ? mover.Controller : null;
            var scale = controller != null ? controller.transform.lossyScale : Vector3.one;
            var radius = controller != null ? controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : 0.5f;
            var halfHeight = controller != null ? controller.height * 0.5f * Mathf.Abs(scale.y) : 1f;
            return _spec.HangPoint(Anchor.ToNumerics(), WorldNormal().ToNumerics(), radius, halfHeight).ToUnity();
        }

        private bool Refuse(GrappleMiss miss, string surface, float? distance, GameObject owner)
        {
            LastMiss = miss;
            Debug.Log(GrappleLog.Miss(miss, surface, distance), owner);
            return false;
        }
    }
}

using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 置けるかの決まりは Core (DeployPlacement) が持ち、ここは物理で調べた結果を渡すだけにする。決まりを Unity 抜きで確かめられるように。
    public static class DeploySurveyor
    {
        public const float GroundProbeHeight = 2.5f;

        public const float CornerProbeMargin = 0.05f;

        // 接している壁や足場まで塞いでいると数えないよう、置く物の大きさから縮めて調べる。
        public const float ClearanceSkin = 0.02f;

        private const int MaxHits = 32;

        private static readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private static readonly Collider[] _overlaps = new Collider[MaxHits];

        // 置けなくても見本を出せるよう、pose には置こうとした所を必ず入れる。
        public static DeployVerdict Survey(Transform owner, Vector3 aimOrigin, Vector3 aimDirection, DeploySpec spec, out Pose pose)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var feet = FeetOf(owner);
            var forward = HorizontalForward(aimDirection, owner);

            if (!TryFindGround(owner, feet, forward, aimOrigin, aimDirection, spec, out var point, out var normal))
            {
                pose = new Pose(feet + (forward * spec.MaxRange), Quaternion.LookRotation(forward, Vector3.up));
                return DeployPlacement.Judge(spec, DeploySurvey.NoGround);
            }

            var groundNormal = normal.ToNumerics();
            var steep = DeployPlacement.SlopeDegrees(groundNormal) > spec.MaxSlopeDegrees;

            // 置ける坂では足場に沿わせる。真上に立てると、坂の下側に弾の抜ける隙間ができる。
            var up = steep ? Vector3.up : normal.normalized;
            var facing = Vector3.ProjectOnPlane(forward, up);
            var rotation = Quaternion.LookRotation(facing.sqrMagnitude > 1e-8f ? facing.normalized : forward, up);
            pose = new Pose(point, rotation);
            if (steep)
            {
                return DeployPlacement.Judge(spec, new DeploySurvey(groundNormal, 0f, 0f, true, false));
            }

            MeasureCorners(owner, point, rotation, spec, out var lowest, out var highest, out var grounded);
            var blocked = IsBlocked(point, rotation, spec);
            return DeployPlacement.Judge(spec, new DeploySurvey(groundNormal, lowest, highest, grounded, blocked));
        }

        // 三人称ではカメラの中央が狙いなので、体の向きでなくカメラから狙う。視点が無い (テストや AI) ときだけ体の正面を使う。
        public static void ResolveAim(GameObject owner, out Vector3 origin, out Vector3 direction)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var rig = owner.GetComponent<ViewRig>();
            if (rig != null && rig.IsInitialized)
            {
                var camera = rig.Camera.transform;
                origin = camera.position;
                direction = camera.forward;
                return;
            }

            origin = owner.transform.position;
            direction = owner.transform.forward;
        }

        public static Vector3 FeetOf(Transform owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var controller = owner.GetComponent<CharacterController>();
            if (controller != null && controller.enabled)
            {
                var position = owner.position;
                return new Vector3(position.x, controller.bounds.min.y, position.z);
            }

            return owner.position;
        }

        private static bool TryFindGround(
            Transform owner,
            Vector3 feet,
            Vector3 forward,
            Vector3 aimOrigin,
            Vector3 aimDirection,
            DeploySpec spec,
            out Vector3 point,
            out Vector3 normal)
        {
            if (aimDirection.sqrMagnitude > 1e-8f)
            {
                var reach = Vector3.Distance(aimOrigin, feet) + spec.MaxRange + GroundProbeHeight;
                if (TryNearest(aimOrigin, aimDirection.normalized, reach, owner, out var hit))
                {
                    var offset = Vector3.ProjectOnPlane(hit.point - feet, Vector3.up);

                    // 三人称のカメラは体の後ろにあるので、体の後ろの足場に当たった線は使わない。
                    if (offset.magnitude <= spec.MaxRange && Vector3.Dot(offset, forward) > 0f)
                    {
                        point = hit.point;
                        normal = hit.normal;
                        return true;
                    }
                }
            }

            // 遠くや空を狙っても置けるよう、届く所の端の真下の足場に置く。
            var above = feet + (forward * spec.MaxRange) + (Vector3.up * GroundProbeHeight);
            if (TryNearest(above, Vector3.down, GroundProbeHeight * 2f, owner, out var ground))
            {
                point = ground.point;
                normal = ground.normal;
                return true;
            }

            point = default;
            normal = Vector3.up;
            return false;
        }

        private static void MeasureCorners(Transform owner, Vector3 point, Quaternion rotation, DeploySpec spec, out float lowest, out float highest, out bool grounded)
        {
            lowest = 0f;
            highest = 0f;
            grounded = true;
            var reach = spec.MaxStepHeight + CornerProbeMargin;
            var halfWidth = spec.Width / 2f;
            var halfDepth = spec.Depth / 2f;
            for (var i = 0; i < 4; i++)
            {
                var local = new Vector3((i & 1) == 0 ? -halfWidth : halfWidth, 0f, (i & 2) == 0 ? -halfDepth : halfDepth);
                var corner = point + (rotation * local);
                if (!TryNearest(corner + (Vector3.up * reach), Vector3.down, reach * 2f, owner, out var hit))
                {
                    grounded = false;
                    continue;
                }

                var deviation = hit.point.y - corner.y;
                lowest = Mathf.Min(lowest, deviation);
                highest = Mathf.Max(highest, deviation);
            }
        }

        // 持ち主の体も数える。自分の立っている所に置かせないため。
        private static bool IsBlocked(Vector3 point, Quaternion rotation, DeploySpec spec)
        {
            var lift = spec.MaxStepHeight + ClearanceSkin;
            var boxHeight = Mathf.Max(ClearanceSkin, spec.Height - lift);
            var halfExtents = new Vector3(
                Mathf.Max(ClearanceSkin, (spec.Width / 2f) - ClearanceSkin),
                boxHeight / 2f,
                Mathf.Max(ClearanceSkin, (spec.Depth / 2f) - ClearanceSkin));
            var center = point + (rotation * new Vector3(0f, lift + (boxHeight / 2f), 0f));
            var count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlaps, rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Clear(_overlaps, 0, count);
            return count > 0;
        }

        private static bool TryNearest(Vector3 origin, Vector3 direction, float distance, Transform owner, out RaycastHit nearest)
        {
            var count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            nearest = default;
            var found = false;
            for (var i = 0; i < count; i++)
            {
                var candidate = _hits[i];
                if (candidate.collider.transform.IsChildOf(owner))
                {
                    continue;
                }

                if (!found || candidate.distance < nearest.distance)
                {
                    nearest = candidate;
                    found = true;
                }
            }

            Array.Clear(_hits, 0, count);
            return found;
        }

        private static Vector3 HorizontalForward(Vector3 aimDirection, Transform owner)
        {
            var forward = Vector3.ProjectOnPlane(aimDirection, Vector3.up);
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = Vector3.ProjectOnPlane(owner.forward, Vector3.up);
            }

            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }
    }
}

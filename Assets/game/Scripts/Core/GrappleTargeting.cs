using System;
using System.Numerics;

namespace Shooter.Core
{
    // オフラインの GrappleAbility.TryAim と同じ規則で、差し込み口の当たり (IWorldCollision) から縄の掛かる先を決める。
    // 掴めない面が手前にあれば、その奥に掴める面があっても掛からない (縄は面を抜けない)。
    public static class GrappleTargeting
    {
        // 縁を探す Ray を面のわずかに奥から下ろす。面ちょうどから下ろすと、面の上を滑って当たらないことがある (GrappleAbility と同じ値)。
        public const float EdgeProbeInset = 0.05f;

        // yaw と pitch はラジアン。yaw は CharacterMotorSimulation.YawForward と同じく +Z が 0、pitch は上を向くと正。
        public static Vector3 Direction(float yaw, float pitch)
        {
            if (!float.IsFinite(yaw) || !float.IsFinite(pitch))
            {
                throw new ArgumentOutOfRangeException(nameof(yaw), "yaw and pitch must be finite");
            }

            var flat = MathF.Cos(pitch);
            return new Vector3(MathF.Sin(yaw) * flat, MathF.Sin(pitch), MathF.Cos(yaw) * flat);
        }

        public static GrappleMiss Aim(
            IWorldCollision world,
            GrappleType grapple,
            int blockingLayers,
            Vector3 origin,
            Vector3 direction,
            out GrappleTarget target)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (grapple == null)
            {
                throw new ArgumentNullException(nameof(grapple));
            }

            if (!IsFinite(origin))
            {
                throw new ArgumentOutOfRangeException(nameof(origin), origin, "origin must be finite");
            }

            target = default;
            if (!IsFinite(direction) || direction.LengthSquared() < 1e-8f)
            {
                return GrappleMiss.OutOfRange;
            }

            direction = Vector3.Normalize(direction);
            var spec = grapple.Spec;
            if (!world.Raycast(origin, direction, spec.MaxRange, blockingLayers, out var hit))
            {
                return GrappleMiss.OutOfRange;
            }

            target = new GrappleTarget(hit.ColliderId, hit.Layer, hit.Point, hit.Normal, hit.Distance, false);
            if (hit.Distance < spec.MinRange)
            {
                return GrappleMiss.TooClose;
            }

            if (!grapple.Grabs(hit.Layer))
            {
                return GrappleMiss.NotGrabbable;
            }

            if (GrappleSpec.IsWall(hit.Normal) && TryFindEdge(world, spec, hit, out var edgeY))
            {
                target = new GrappleTarget(
                    hit.ColliderId, hit.Layer, new Vector3(hit.Point.X, edgeY, hit.Point.Z), hit.Normal, hit.Distance, true);
            }

            return GrappleMiss.None;
        }

        // 当たった面の少し奥の上から、同じ当たりにだけ Ray を下ろして上の縁を探す。始点が当たりの中 (縁がもっと上) なら当たらない。
        private static bool TryFindEdge(IWorldCollision world, GrappleSpec spec, WorldHit hit, out float edgeY)
        {
            edgeY = 0f;
            var reach = spec.EdgeSnapDistance;
            if (reach <= 0f)
            {
                return false;
            }

            var flat = new Vector3(hit.Normal.X, 0f, hit.Normal.Z);
            if (flat.LengthSquared() < 1e-8f)
            {
                return false;
            }

            var into = -Vector3.Normalize(flat);
            var start = hit.Point + (into * EdgeProbeInset) + (Vector3.UnitY * reach);
            if (!world.RaycastCollider(hit.ColliderId, start, -Vector3.UnitY, reach, out var top))
            {
                return false;
            }

            if (top.Normal.LengthSquared() < 1e-8f || Vector3.Normalize(top.Normal).Y < GrappleSpec.FloorNormalY)
            {
                return false;
            }

            edgeY = top.Point.Y;
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
        }
    }
}

using System;
using System.Numerics;

namespace Shooter.Core
{
    // 一周しない範囲では、近いほうへ回ると範囲の外を通ることがあるので、範囲の中をまっすぐ回る。
    public sealed class VehicleWeaponAim
    {
        // 角度に直すと float の丸めで端ちょうどの向きがわずかに外へ出るので、この差までは届くとみなす。
        private const float _reachToleranceDegrees = 1e-3f;

        public VehicleWeaponAim(VehicleWeaponSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Reset();
        }

        public VehicleWeaponSpec Spec { get; }

        public float YawDegrees { get; private set; }

        public float PitchDegrees { get; private set; }

        public bool IsFullCircle => Spec.YawMaxDegrees - Spec.YawMinDegrees >= 360f;

        public Vector3 LocalDirection => ToDirection(YawDegrees, PitchDegrees);

        public void Reset()
        {
            YawDegrees = ClampYaw(0f);
            PitchDegrees = ClampPitch(0f);
        }

        public float ClampYaw(float yawDegrees)
        {
            RequireFinite(yawDegrees, nameof(yawDegrees));
            var yaw = Wrap(yawDegrees);
            if (IsFullCircle || (yaw >= Spec.YawMinDegrees && yaw <= Spec.YawMaxDegrees))
            {
                return yaw;
            }

            var toMin = Math.Abs(TurretTargeting.DeltaDegrees(yaw, Spec.YawMinDegrees));
            var toMax = Math.Abs(TurretTargeting.DeltaDegrees(yaw, Spec.YawMaxDegrees));
            return toMin <= toMax ? Spec.YawMinDegrees : Spec.YawMaxDegrees;
        }

        public float ClampPitch(float pitchDegrees)
        {
            RequireFinite(pitchDegrees, nameof(pitchDegrees));
            return Math.Clamp(pitchDegrees, Spec.PitchMinDegrees, Spec.PitchMaxDegrees);
        }

        public bool Step(Vector3 localDirection, float deltaSeconds)
        {
            if (!TryToAngles(localDirection, out var yaw, out var pitch))
            {
                RequireDelta(deltaSeconds);
                return false;
            }

            return Step(yaw, pitch, deltaSeconds);
        }

        public bool Step(float yawDegrees, float pitchDegrees, float deltaSeconds)
        {
            RequireDelta(deltaSeconds);
            var targetYaw = ClampYaw(yawDegrees);
            var targetPitch = ClampPitch(pitchDegrees);
            var maxStep = Spec.TurnSpeedDegreesPerSecond * deltaSeconds;

            var yawDelta = IsFullCircle ? TurretTargeting.DeltaDegrees(YawDegrees, targetYaw) : targetYaw - YawDegrees;
            var pitchDelta = targetPitch - PitchDegrees;
            YawDegrees = IsFullCircle
                ? Wrap(YawDegrees + Math.Clamp(yawDelta, -maxStep, maxStep))
                : YawDegrees + Math.Clamp(yawDelta, -maxStep, maxStep);
            PitchDegrees += Math.Clamp(pitchDelta, -maxStep, maxStep);
            return Math.Abs(yawDelta) <= maxStep && Math.Abs(pitchDelta) <= maxStep;
        }

        // 範囲の端で止まった銃は向けた先へ届かないまま撃ててしまうので、撃つ側 (敵の銃手など) が届くかを先に確かめられるようにする。
        public bool CanReach(Vector3 localDirection)
        {
            if (!TryToAngles(localDirection, out var yaw, out var pitch))
            {
                return false;
            }

            return Math.Abs(TurretTargeting.DeltaDegrees(ClampYaw(yaw), yaw)) <= _reachToleranceDegrees
                && Math.Abs(ClampPitch(pitch) - pitch) <= _reachToleranceDegrees;
        }

        public float OffsetDegrees(Vector3 localDirection)
        {
            if (!IsFinite(localDirection) || localDirection.LengthSquared() < 1e-12f)
            {
                throw new ArgumentException($"localDirection must be finite and non-zero: {localDirection}", nameof(localDirection));
            }

            var cos = Vector3.Dot(LocalDirection, Vector3.Normalize(localDirection));
            return MathF.Acos(Math.Clamp(cos, -1f, 1f)) * (180f / MathF.PI);
        }

        public static bool TryToAngles(Vector3 localDirection, out float yawDegrees, out float pitchDegrees)
        {
            yawDegrees = 0f;
            pitchDegrees = 0f;
            if (!IsFinite(localDirection) || localDirection.LengthSquared() < 1e-12f)
            {
                return false;
            }

            var flat = MathF.Sqrt((localDirection.X * localDirection.X) + (localDirection.Z * localDirection.Z));
            yawDegrees = flat > 1e-6f ? MathF.Atan2(localDirection.X, localDirection.Z) * (180f / MathF.PI) : 0f;
            pitchDegrees = MathF.Atan2(localDirection.Y, flat) * (180f / MathF.PI);
            return true;
        }

        public static Vector3 ToDirection(float yawDegrees, float pitchDegrees)
        {
            var yaw = yawDegrees * (MathF.PI / 180f);
            var pitch = pitchDegrees * (MathF.PI / 180f);
            var flat = MathF.Cos(pitch);
            return new Vector3(MathF.Sin(yaw) * flat, MathF.Sin(pitch), MathF.Cos(yaw) * flat);
        }

        // 範囲は -180 以上 180 以下で持つので、同じ形 (-180 より大きく 180 以下) に揃えてから比べる。
        private static float Wrap(float degrees)
        {
            return TurretTargeting.DeltaDegrees(0f, degrees);
        }

        private static void RequireDelta(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }
        }

        private static void RequireFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite");
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }
    }
}

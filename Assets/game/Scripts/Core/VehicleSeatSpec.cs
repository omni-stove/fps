using System;

namespace Shooter.Core
{
    // 乗る位置・降りる位置・座る位置はシーンの置き方なので、ここではなく座席の部品が持つ。
    public sealed class VehicleSeatSpec
    {
        public const VehicleSeatPose DefaultPose = VehicleSeatPose.Seated;

        public const VehicleCameraView DefaultCameraView = VehicleCameraView.Chase;

        public const float DefaultOwnWeaponArcDegrees = 0f;

        public const float MaxOwnWeaponArcDegrees = 360f;

        public const float DefaultOwnWeaponSpreadMultiplier = 1.5f;

        public VehicleSeatSpec(
            VehicleSeatRole role,
            VehicleSeatPose pose = DefaultPose,
            VehicleCameraView cameraView = DefaultCameraView,
            float ownWeaponArcDegrees = DefaultOwnWeaponArcDegrees,
            float ownWeaponSpreadMultiplier = DefaultOwnWeaponSpreadMultiplier)
        {
            if (!Enum.IsDefined(typeof(VehicleSeatRole), role))
            {
                throw new ArgumentOutOfRangeException(nameof(role), role, "role is not a defined VehicleSeatRole");
            }

            if (!Enum.IsDefined(typeof(VehicleSeatPose), pose))
            {
                throw new ArgumentOutOfRangeException(nameof(pose), pose, "pose is not a defined VehicleSeatPose");
            }

            if (!Enum.IsDefined(typeof(VehicleCameraView), cameraView))
            {
                throw new ArgumentOutOfRangeException(nameof(cameraView), cameraView, "cameraView is not a defined VehicleCameraView");
            }

            if (!(ownWeaponArcDegrees >= 0f && ownWeaponArcDegrees <= MaxOwnWeaponArcDegrees))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ownWeaponArcDegrees), ownWeaponArcDegrees, "ownWeaponArcDegrees must be between 0 and 360");
            }

            if (!(ownWeaponSpreadMultiplier >= 1f) || float.IsInfinity(ownWeaponSpreadMultiplier))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ownWeaponSpreadMultiplier), ownWeaponSpreadMultiplier, "ownWeaponSpreadMultiplier must be at least 1 and finite");
            }

            Role = role;
            Pose = pose;
            CameraView = cameraView;
            OwnWeaponArcDegrees = ownWeaponArcDegrees;
            OwnWeaponSpreadMultiplier = ownWeaponSpreadMultiplier;
        }

        public VehicleSeatRole Role { get; }

        public VehicleSeatPose Pose { get; }

        public VehicleCameraView CameraView { get; }

        public float OwnWeaponArcDegrees { get; }

        public float OwnWeaponSpreadMultiplier { get; }

        // 運転席は撃つ手が空かないので、範囲を持っていても自分の武器では撃たない。乗り物の武器なら VehicleWeaponSpec.SeatIndex で運転席に付けられる。
        public bool CanFireOwnWeapon => Role != VehicleSeatRole.Driver && OwnWeaponArcDegrees > 0f;

        public bool AllowsOwnWeaponAt(float yawOffsetDegrees)
        {
            if (float.IsNaN(yawOffsetDegrees) || float.IsInfinity(yawOffsetDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(yawOffsetDegrees), yawOffsetDegrees, "yawOffsetDegrees must be finite");
            }

            if (!CanFireOwnWeapon)
            {
                return false;
            }

            return OwnWeaponArcDegrees >= MaxOwnWeaponArcDegrees
                || Math.Abs(TurretTargeting.DeltaDegrees(0f, yawOffsetDegrees)) <= OwnWeaponArcDegrees * 0.5f;
        }

        public bool CrouchesRider => Pose != VehicleSeatPose.Standing;
    }
}

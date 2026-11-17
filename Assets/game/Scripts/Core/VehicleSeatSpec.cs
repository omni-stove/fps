using System;

namespace Shooter.Core
{
    /// <summary>
    /// 乗り物の 1 つの座席の設定 (<see cref="VehicleSpec.Seats"/> の 1 つ)。生成時に値を検査し、以後は変わらない。
    /// 乗る位置・降りる位置・座る位置はシーンの置き方なので、ここではなく座席の部品が持つ。
    /// </summary>
    public sealed class VehicleSeatSpec
    {
        public const VehicleSeatPose DefaultPose = VehicleSeatPose.Seated;

        public const VehicleCameraView DefaultCameraView = VehicleCameraView.Chase;

        /// <summary>自分の武器で撃てる向きの範囲 (度) の既定値。0 は撃てない。</summary>
        public const float DefaultOwnWeaponArcDegrees = 0f;

        public const float MaxOwnWeaponArcDegrees = 360f;

        public const float DefaultOwnWeaponSpreadMultiplier = 1.5f;

        /// <param name="role">座席の種類。定義済みの値。</param>
        /// <param name="pose">座っている間の姿勢。定義済みの値。</param>
        /// <param name="cameraView">座っている間のカメラ。定義済みの値。</param>
        /// <param name="ownWeaponArcDegrees">
        /// 座ったまま自分の武器で撃てる、座席の正面を中心にした向きの範囲 (度)。0 以上 <see cref="MaxOwnWeaponArcDegrees"/> 以下。0 は撃てない。
        /// </param>
        /// <param name="ownWeaponSpreadMultiplier">自分の武器で撃つときの散らばりに掛ける倍率。1 以上の有限値。</param>
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

        /// <summary>座ったまま自分の武器で撃てる向きの範囲 (度)。0 は撃てない。</summary>
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

        /// <summary>しゃがみの高さにするか (<see cref="VehicleSeatPose.Standing"/> 以外)。</summary>
        public bool CrouchesRider => Pose != VehicleSeatPose.Standing;
    }
}

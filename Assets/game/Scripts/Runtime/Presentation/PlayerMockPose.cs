using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public readonly struct MockPartPose : IEquatable<MockPartPose>
    {
        public MockPartPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public bool Equals(MockPartPose other)
        {
            return Position == other.Position && Rotation == other.Rotation;
        }

        public override bool Equals(object obj)
        {
            return obj is MockPartPose other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Position, Rotation);
        }

        public override string ToString()
        {
            return $"pos={Position} rot={Rotation.eulerAngles}";
        }
    }

    public static class PlayerMockPose
    {
        public const float TorsoWidth = 0.6f;
        public const float TorsoDepth = 0.4f;
        public const float TorsoHeight = 1.4f;

        public const float TorsoBottom = 0.05f;

        public const float HeadDiameter = 0.36f;

        // PlayerController.DefaultCrouchHeight と GameBootstrap.PlayerHeight の比に揃え、当たり判定の縮み方と見た目を合わせる。
        public const float CrouchScale = PlayerController.DefaultCrouchHeight / GameBootstrap.PlayerHeight;

        // CrouchScale と同じく、当たり判定の縮み方と見た目を合わせる。
        public const float ProneScale = ProneSpec.DefaultHeight / GameBootstrap.PlayerHeight;

        public const float EyeHeight = 1.6f;

        public static readonly Vector3 GunSize = new Vector3(0.08f, 0.12f, 0.55f);

        public const float DodgeLeanDegrees = 15f;
        public const float VaultLeanDegrees = 25f;
        public const float CoverLeanDegrees = 5f;

        // 真横 (90) だと体が床にめり込むので少し手前で止める。
        public const float DownedRollDegrees = 80f;

        // 真横 (90) まで倒すと、足元を軸に回した体が水面より下へ沈むので少し起こす。
        public const float SwimPitchDegrees = 70f;

        public const float WallRunTiltDegrees = 20f;

        public const float CoverShift = 0.2f;

        public const float BodyKickDistance = 0.08f;
        public const float BodyKickDegrees = 6f;

        public const float ViewKickDistance = 0.06f;
        public const float ViewKickDegrees = 8f;

        public const float KickRecoverSeconds = 0.12f;

        public const float BlendRate = 14f;

        public static readonly Vector3 BodyHipGunPosition = new Vector3(0.25f, 1.15f, 0.3f);

        public static readonly Vector3 BodyAimGunPosition = new Vector3(0.12f, EyeHeight - 0.08f, 0.35f);

        public static readonly Vector3 BodyReloadGunPosition = new Vector3(0.25f, 0.95f, 0.2f);
        public static readonly Vector3 BodyReloadGunEuler = new Vector3(35f, 0f, -25f);

        public static readonly Vector3 ViewHipPosition = new Vector3(0.22f, -0.2f, 0.45f);

        public static readonly Vector3 ViewAimPosition = new Vector3(0f, -0.1f, 0.32f);

        public static readonly Vector3 ViewReloadPosition = new Vector3(0.22f, -0.36f, 0.4f);
        public static readonly Vector3 ViewReloadEuler = new Vector3(30f, 0f, -20f);

        public static float StanceScale(PlayerActionState state)
        {
            return StanceScale(state, null);
        }

        public static float StanceScale(PlayerActionState state, StanceGeometry stance)
        {
            if (state.IsDowned)
            {
                return 1f;
            }

            if (state.IsProne)
            {
                return stance != null ? stance.Prone.Height / stance.Standing.Height : ProneScale;
            }

            if (state.IsCrouching)
            {
                return stance != null ? stance.Crouched.Height / stance.Standing.Height : CrouchScale;
            }

            return 1f;
        }

        public static void Torso(float stanceScale, out Vector3 position, out Vector3 scale)
        {
            RequirePositiveFinite(stanceScale, nameof(stanceScale));
            var height = TorsoHeight * stanceScale;
            position = new Vector3(0f, (TorsoBottom * stanceScale) + (height / 2f), 0f);
            scale = new Vector3(TorsoWidth, height / 2f, TorsoDepth);
        }

        public static Vector3 Head(float stanceScale)
        {
            RequirePositiveFinite(stanceScale, nameof(stanceScale));
            return new Vector3(0f, ((TorsoBottom + TorsoHeight) * stanceScale) + (HeadDiameter / 2f), 0f);
        }

        public static Quaternion BodyLean(PlayerActionState state, Vector3 localMove, Vector3 localCoverNormal)
        {
            if (state.IsDowned)
            {
                return Quaternion.Euler(0f, 0f, DownedRollDegrees);
            }

            if (state.IsVaulting)
            {
                return Quaternion.Euler(VaultLeanDegrees, 0f, 0f);
            }

            if (state.IsDodging)
            {
                var direction = Horizontal(localMove);
                return LeanToward(direction == Vector3.zero ? Vector3.forward : direction, DodgeLeanDegrees);
            }

            if (state.IsInCover)
            {
                var wall = -Horizontal(localCoverNormal);
                return wall == Vector3.zero ? Quaternion.identity : LeanToward(wall, CoverLeanDegrees);
            }

            return Quaternion.identity;
        }

        public static Quaternion BodyLean(
            PlayerActionState state, Vector3 localMove, Vector3 localCoverNormal, CharacterMotorState motor, float leanAngleDegrees)
        {
            if (state.IsDowned || state.IsVaulting || state.IsDodging || state.IsInCover)
            {
                return BodyLean(state, localMove, localCoverNormal);
            }

            return TraversalLean(motor, leanAngleDegrees);
        }

        public static Quaternion TraversalLean(CharacterMotorState motor, float leanAngleDegrees)
        {
            return TraversalLean(motor.Lean.Amount, motor.IsSwimming, motor.WallRun.Side, leanAngleDegrees);
        }

        public static Quaternion TraversalLean(CharacterPoseParameters parameters, float leanAngleDegrees)
        {
            return TraversalLean(parameters.Lean, parameters.Swimming, (WallRunSide)parameters.WallRun, leanAngleDegrees);
        }

        public static Quaternion TraversalLean(float lean, bool swimming, WallRunSide wallRun, float leanAngleDegrees)
        {
            if (!(lean >= -1f && lean <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(lean), lean, "lean must be between -1 and 1");
            }

            if (!(leanAngleDegrees >= 0f && leanAngleDegrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(nameof(leanAngleDegrees), leanAngleDegrees, "leanAngleDegrees must be between 0 and 90");
            }

            switch (wallRun)
            {
                case WallRunSide.Left:
                    return LeanToward(Vector3.right, WallRunTiltDegrees);
                case WallRunSide.Right:
                    return LeanToward(Vector3.left, WallRunTiltDegrees);
                case WallRunSide.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(wallRun), wallRun, "wallRun is not a defined WallRunSide");
            }

            if (swimming)
            {
                return Quaternion.Euler(SwimPitchDegrees, 0f, 0f);
            }

            return LeanToward(Vector3.right, leanAngleDegrees * lean);
        }

        public static Vector3 BodyShift(PlayerActionState state, Vector3 localCoverNormal)
        {
            if (!state.IsInCover || state.IsDowned)
            {
                return Vector3.zero;
            }

            return -Horizontal(localCoverNormal) * CoverShift;
        }

        public static MockPartPose BodyGun(PlayerActionState state, Quaternion aimPitch)
        {
            return BodyGun(state, aimPitch, null);
        }

        public static MockPartPose BodyGun(PlayerActionState state, Quaternion aimPitch, StanceGeometry stance)
        {
            var scale = StanceScale(state, stance);
            if (state.IsReloading || state.IsDowned)
            {
                return new MockPartPose(ScaleHeight(BodyReloadGunPosition, scale), Quaternion.Euler(BodyReloadGunEuler));
            }

            var position = state.IsAiming ? BodyAimGunPosition : BodyHipGunPosition;
            return new MockPartPose(ScaleHeight(position, scale), aimPitch);
        }

        public static MockPartPose ViewModel(PlayerActionState state)
        {
            if (state.IsReloading || state.IsDowned)
            {
                return new MockPartPose(ViewReloadPosition, Quaternion.Euler(ViewReloadEuler));
            }

            return new MockPartPose(state.IsAiming ? ViewAimPosition : ViewHipPosition, Quaternion.identity);
        }

        public static MockPartPose WithKick(MockPartPose pose, float kick, float distance, float degrees)
        {
            if (!(kick >= 0f && kick <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(kick), kick, "kick must be between 0 and 1");
            }

            if (kick == 0f)
            {
                return pose;
            }

            var back = pose.Rotation * Vector3.back * (distance * kick);
            return new MockPartPose(pose.Position + back, pose.Rotation * Quaternion.Euler(-degrees * kick, 0f, 0f));
        }

        public static float RecoverKick(float kick, float deltaSeconds)
        {
            RequireDelta(deltaSeconds);
            return Mathf.Clamp01(kick - (deltaSeconds / KickRecoverSeconds));
        }

        // フレームの長さが変わっても同じ速さで寄るよう、割合は指数で決める。
        public static float BlendFactor(float deltaSeconds)
        {
            RequireDelta(deltaSeconds);
            return 1f - Mathf.Exp(-BlendRate * deltaSeconds);
        }

        public static Vector3 Horizontal(Vector3 vector)
        {
            var flat = new Vector3(vector.x, 0f, vector.z);
            var length = flat.magnitude;
            return length > 1e-4f && !float.IsInfinity(length) && !float.IsNaN(length) ? flat / length : Vector3.zero;
        }

        // 上を direction へ傾ける。軸を up × direction に取ると、正の角度で頭が direction 側へ倒れる。
        private static Quaternion LeanToward(Vector3 direction, float degrees)
        {
            return Quaternion.AngleAxis(degrees, Vector3.Cross(Vector3.up, direction));
        }

        private static Vector3 ScaleHeight(Vector3 position, float scale)
        {
            return new Vector3(position.x, position.y * scale, position.z);
        }

        private static void RequireDelta(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }
        }

        private static void RequirePositiveFinite(float value, string paramName)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, "must be positive and finite");
            }
        }
    }
}

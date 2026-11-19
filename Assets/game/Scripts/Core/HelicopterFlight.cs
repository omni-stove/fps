using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct HelicopterFlightCommand
    {
        public HelicopterFlightCommand(Vector3 acceleration, Vector3 angularAcceleration, float groundEffect, float upright)
        {
            Acceleration = acceleration;
            AngularAcceleration = angularAcceleration;
            GroundEffect = groundEffect;
            Upright = upright;
        }

        // 重力を含めないのは、Runtime が Rigidbody の重力に任せたまま AddForce に足せるようにするため。
        public Vector3 Acceleration { get; }

        public Vector3 AngularAcceleration { get; }

        public float GroundEffect { get; }

        public float Upright { get; }
    }

    // 軸と回る向きはワールド (x 東・y 上・z 北) で Unity の左手系に合わせる。Runtime が AddForce・AddTorque にそのまま渡せるようにするため。
    public static class HelicopterFlight
    {
        public const int CollectiveAxis = 0;

        public const int PitchAxis = 1;

        public const int RollAxis = 2;

        public const int YawAxis = 3;

        // ローターを回すかを軸で受けるのは、誰も乗っていないときの入力 (VehicleMotorInput.Idle) で宙に浮いたままにせず、落ちるようにするため。
        public const int EngineAxis = 4;

        private const float _minHeadingLength = 1e-3f;
        private const float _degreesToRadians = (float)(Math.PI / 180.0);
        private const float _radiansToDegrees = (float)(180.0 / Math.PI);

        public static HelicopterFlightCommand Compute(
            HelicopterSpec spec,
            VehicleMotorInput input,
            float rotor,
            Vector3 forward,
            Vector3 up,
            Vector3 velocity,
            Vector3 angularVelocity,
            float gravity,
            float groundHeight = float.PositiveInfinity)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!(rotor >= 0f && rotor <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(rotor), rotor, "rotor must be between 0 and 1");
            }

            RequireFinite(forward, nameof(forward));
            RequireFinite(up, nameof(up));
            RequireFinite(velocity, nameof(velocity));
            RequireFinite(angularVelocity, nameof(angularVelocity));
            if (!(gravity >= 0f) || float.IsInfinity(gravity))
            {
                throw new ArgumentOutOfRangeException(nameof(gravity), gravity, "gravity must be a non-negative finite number");
            }

            var groundEffect = GroundEffect(spec, groundHeight);
            var heading = Heading(forward, up);
            var right = Vector3.Cross(Vector3.UnitY, heading);
            var upright = Upright(spec, up);
            var pitch = input[PitchAxis];
            var roll = input[RollAxis];

            var wanted = (heading * pitch) + (right * roll);
            if (wanted.LengthSquared() > 1f)
            {
                wanted = Vector3.Normalize(wanted);
            }

            var horizontalVelocity = new Vector3(velocity.X, 0f, velocity.Z);
            var horizontal = ClampLength((wanted * spec.MaxSpeed - horizontalVelocity) * spec.HorizontalResponse, spec.MaxHorizontalAcceleration);

            var collective = input[CollectiveAxis];
            var climb = collective >= 0f ? collective * spec.MaxClimbRate : collective * spec.MaxDescentRate;
            var vertical = Clamp((climb - velocity.Y) * spec.VerticalResponse, -spec.MaxVerticalAcceleration, spec.MaxVerticalAcceleration);
            // 地面効果を上へ押すときだけ強めるのは、中立で地面から勝手に浮き上がらず、離陸と着地の手前の減速だけを助けるため。
            if (vertical > 0f)
            {
                vertical *= 1f + (spec.GroundEffectBoost * groundEffect);
            }

            var lift = Math.Max(0f, gravity + vertical);
            var acceleration = (horizontal + (Vector3.UnitY * lift)) * (rotor * upright);

            var tilt = spec.MaxTiltAngle * _degreesToRadians;
            var leanForward = (float)Math.Sin(pitch * tilt) - Vector3.Dot(up, heading);
            var leanRight = (float)Math.Sin(roll * tilt) - Vector3.Dot(up, right);
            var aboutRight = (leanForward * spec.TiltStiffness) - (Vector3.Dot(angularVelocity, right) * spec.TiltDamping);
            // 前の軸の正の向きは左へ傾ける回り方 (左手系) なので、右へ傾けるには負に回す。
            var aboutForward = (-leanRight * spec.TiltStiffness) - (Vector3.Dot(angularVelocity, heading) * spec.TiltDamping);
            var aboutUp = ((input[YawAxis] * spec.MaxYawRate * _degreesToRadians) - angularVelocity.Y) * spec.YawResponse;
            var angular = ((right * aboutRight) + (heading * aboutForward) + (Vector3.UnitY * aboutUp)) * rotor;

            return new HelicopterFlightCommand(acceleration, angular, groundEffect, upright);
        }

        public static float StepRotor(HelicopterSpec spec, float rotor, bool engineOn, float deltaSeconds)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!(rotor >= 0f && rotor <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(rotor), rotor, "rotor must be between 0 and 1");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            var target = engineOn ? 1f : 0f;
            if (spec.SpoolSeconds <= 0f)
            {
                return target;
            }

            var step = deltaSeconds / spec.SpoolSeconds;
            return engineOn ? Math.Min(target, rotor + step) : Math.Max(target, rotor - step);
        }

        // 負の高さを弾かず地面に触れているとみなすのは、測る原点が着地の衝撃で地面より下に沈むことがあるため。
        public static float GroundEffect(HelicopterSpec spec, float groundHeight)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (float.IsNaN(groundHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(groundHeight), groundHeight, "groundHeight must not be NaN");
            }

            if (!(spec.GroundEffectHeight > 0f) || groundHeight >= spec.GroundEffectHeight)
            {
                return 0f;
            }

            return 1f - (Math.Max(0f, groundHeight) / spec.GroundEffectHeight);
        }

        // 傾け切った角度までは浮かせる力を落とさないのは、前へ進むと沈むことの無いようにするため。それより倒れると 90 度で 0 まで落とし、横倒しの機体が浮かないようにする。
        public static float Upright(HelicopterSpec spec, Vector3 up)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            RequireFinite(up, nameof(up));
            var length = up.Length();
            if (!(length > 0f))
            {
                return 0f;
            }

            var cosine = up.Y / length;
            var envelope = (float)Math.Cos(spec.MaxTiltAngle * _degreesToRadians);
            return Clamp(cosine / envelope, 0f, 1f);
        }

        // 機首が真下・真上を向くと水平の向きが無いので、そのときは上の向きから機首の側を取る。
        public static Vector3 Heading(Vector3 forward, Vector3 up)
        {
            var flat = new Vector3(forward.X, 0f, forward.Z);
            if (flat.Length() > _minHeadingLength)
            {
                return Vector3.Normalize(flat);
            }

            var side = forward.Y < 0f ? 1f : -1f;
            var fromUp = new Vector3(up.X, 0f, up.Z) * side;
            return fromUp.Length() > _minHeadingLength ? Vector3.Normalize(fromUp) : Vector3.UnitZ;
        }

        // 真上・真下を狙ったときに回さないのは、水平の向きが定まらず左右に振れるため。
        public static float YawToward(HelicopterSpec spec, Vector3 forward, Vector3 up, Vector3 aim)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            RequireFinite(forward, nameof(forward));
            RequireFinite(up, nameof(up));
            RequireFinite(aim, nameof(aim));
            var flatAim = new Vector3(aim.X, 0f, aim.Z);
            if (!(flatAim.Length() > _minHeadingLength))
            {
                return 0f;
            }

            var heading = Heading(forward, up);
            var right = Vector3.Cross(Vector3.UnitY, heading);
            var degrees = (float)Math.Atan2(Vector3.Dot(flatAim, right), Vector3.Dot(flatAim, heading)) * _radiansToDegrees;
            return Clamp(degrees / spec.YawAimAngle, -1f, 1f);
        }

        private static Vector3 ClampLength(Vector3 value, float max)
        {
            var length = value.Length();
            return length > max ? value * (max / length) : value;
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z))
            {
                throw new ArgumentException($"{name} must be finite: {value}", name);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

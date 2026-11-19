using System;
using System.Numerics;

namespace Shooter.Core
{
    // 位置と速さはワールドの値で受ける。水の箱 (WaterBox) が軸にそろったワールドの箱で、点ごとに水面を引くため。
    public readonly struct BoatHullState
    {
        public BoatHullState(Vector3 position, Quaternion rotation, Vector3 centerOfMass, Vector3 linearVelocity, Vector3 angularVelocity)
        {
            RequireFinite(position, nameof(position));
            RequireFinite(centerOfMass, nameof(centerOfMass));
            RequireFinite(linearVelocity, nameof(linearVelocity));
            RequireFinite(angularVelocity, nameof(angularVelocity));
            if (!IsFinite(rotation.X) || !IsFinite(rotation.Y) || !IsFinite(rotation.Z) || !IsFinite(rotation.W) || rotation.LengthSquared() < 1e-6f)
            {
                throw new ArgumentException($"rotation {rotation} must be a finite, non-zero quaternion", nameof(rotation));
            }

            Position = position;
            Rotation = Quaternion.Normalize(rotation);
            CenterOfMass = centerOfMass;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
        }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public Vector3 CenterOfMass { get; }

        public Vector3 LinearVelocity { get; }

        public Vector3 AngularVelocity { get; }

        public Vector3 ToWorld(Vector3 local)
        {
            return Position + Vector3.Transform(local, Rotation);
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z))
            {
                throw new ArgumentException($"{name} {value} must be finite", name);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct BoatHullCommand
    {
        public BoatHullCommand(
            Vector3 force,
            Vector3 torque,
            Vector3 angularAcceleration,
            float wetFraction,
            float propellerImmersion,
            float thrustAcceleration)
        {
            Force = force;
            Torque = torque;
            AngularAcceleration = angularAcceleration;
            WetFraction = wetFraction;
            PropellerImmersion = propellerImmersion;
            ThrustAcceleration = thrustAcceleration;
        }

        // 重力を含めないのは、Rigidbody が掛ける重力と二重にしないため。
        public Vector3 Force { get; }

        public Vector3 Torque { get; }

        // 舵が出すワールドの角加速度 (rad/s²)。慣性の大きさによらず同じ曲がり方にするため、トルクでなく加速度で渡す。
        public Vector3 AngularAcceleration { get; }

        public float WetFraction { get; }

        public float PropellerImmersion { get; }

        public float ThrustAcceleration { get; }

        public bool IsAfloat => WetFraction > 0f;
    }

    public static class BoatHull
    {
        // 当て方は CarControlScheme をそのまま使うので、軸の割り当てを車にそろえる。
        public const int ThrottleAxis = CarDrive.ThrottleAxis;

        public const int SteerAxis = CarDrive.SteerAxis;

        // これより遅ければ、後ろへのスロットルで舵を後退向きに切り替える。止まり切るまで待つと、ほぼ止まった船が後ろへ曲がり出せないため。
        public const float DirectionChangeSpeed = CarDrive.DirectionChangeSpeed;

        private const float _degreesToRadians = (float)(Math.PI / 180.0);

        public static BoatHullCommand Compute(BoatSpec spec, VehicleMotorInput input, BoatHullState state, IWaterVolumes water, float gravity)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (water == null)
            {
                throw new ArgumentNullException(nameof(water));
            }

            if (!(gravity >= 0f) || float.IsInfinity(gravity))
            {
                throw new ArgumentOutOfRangeException(nameof(gravity), gravity, "gravity must be a non-negative finite number");
            }

            var pointMass = spec.Mass / BoatSpec.HullPointCount;
            // 喫水まで沈んだ点の浮力の合計がちょうど重さになるよう、ばねの強さを重さと喫水から決める。
            var stiffness = pointMass * gravity / spec.Draft;
            var force = Vector3.Zero;
            var torque = Vector3.Zero;
            var wetSum = 0f;
            for (var i = 0; i < BoatSpec.HullPointCount; i++)
            {
                var point = state.ToWorld(spec.HullPoint(i));
                var depth = Depth(spec, water, point);
                if (depth <= 0f)
                {
                    continue;
                }

                var wet = Math.Min(1f, depth / spec.Draft);
                wetSum += wet;
                var arm = point - state.CenterOfMass;
                var pointVelocity = state.LinearVelocity + Vector3.Cross(state.AngularVelocity, arm);
                var lift = (stiffness * depth) - (pointMass * spec.HeaveDamping * pointVelocity.Y * wet);
                var pointForce = new Vector3(0f, lift, 0f);
                force += pointForce;
                torque += Vector3.Cross(arm, pointForce);
            }

            var wetFraction = wetSum / BoatSpec.HullPointCount;
            var propellerImmersion = Math.Min(1f, Depth(spec, water, state.ToWorld(spec.Propeller)) / spec.Draft);
            var forward = Vector3.Transform(Vector3.UnitZ, state.Rotation);
            var heading = new Vector3(forward.X, 0f, forward.Z);
            // 船首が真上か真下を向くと水平な向きが無く、進む向きも横の向きも決まらないので、浮力だけを掛ける。
            if (heading.LengthSquared() < 1e-6f || wetFraction <= 0f)
            {
                return new BoatHullCommand(force, torque, Vector3.Zero, wetFraction, propellerImmersion, 0f);
            }

            heading = Vector3.Normalize(heading);
            var side = Vector3.Cross(Vector3.UnitY, heading);
            var forwardSpeed = Vector3.Dot(state.LinearVelocity, heading);
            var sideSpeed = Vector3.Dot(state.LinearVelocity, side);
            var throttle = input[ThrottleAxis];
            var thrust = ThrustAcceleration(spec, throttle, forwardSpeed) * propellerImmersion;
            var drag = (heading * (spec.ForwardDrag * forwardSpeed)) + (side * (spec.SideDrag * sideSpeed));
            force += spec.Mass * ((heading * thrust) - (drag * wetFraction));

            var control = Math.Min(1f, Math.Max(Math.Abs(forwardSpeed) / spec.TurnSpeed, Math.Abs(throttle))) * propellerImmersion;
            var reversing = forwardSpeed < -DirectionChangeSpeed || (forwardSpeed <= DirectionChangeSpeed && throttle < 0f);
            var targetYawRate = input[SteerAxis] * spec.TurnRate * _degreesToRadians * control * (reversing ? -1f : 1f);
            var yawAcceleration = (targetYawRate - state.AngularVelocity.Y) * spec.TurnResponsiveness * wetFraction;
            return new BoatHullCommand(force, torque, new Vector3(0f, yawAcceleration, 0f), wetFraction, propellerImmersion, thrust);
        }

        // 上限の速さに近づくほど推力を弱めるのは、上限で急に切ると速さが上限の前後で揺れるため。
        public static float ThrustAcceleration(BoatSpec spec, float throttle, float forwardSpeed)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (throttle > 0f)
            {
                return spec.MaxForwardSpeed > 0f ? throttle * spec.Thrust * Clamp01(1f - (forwardSpeed / spec.MaxForwardSpeed)) : 0f;
            }

            if (throttle < 0f)
            {
                return spec.MaxReverseSpeed > 0f ? throttle * spec.Thrust * Clamp01(1f + (forwardSpeed / spec.MaxReverseSpeed)) : 0f;
            }

            return 0f;
        }

        // 船体の高さで止めるのは、船体より深く沈めても押しのける水は増えず、浮力も増えないため。
        public static float Depth(BoatSpec spec, IWaterVolumes water, Vector3 point)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (water == null)
            {
                throw new ArgumentNullException(nameof(water));
            }

            var surface = water.SurfaceAt(point);
            if (!surface.HasValue)
            {
                return 0f;
            }

            return Math.Min(spec.HullHeight, Math.Max(0f, surface.Value - point.Y));
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }
}

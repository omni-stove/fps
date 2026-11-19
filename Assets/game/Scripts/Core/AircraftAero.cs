using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct AircraftAeroForces
    {
        public AircraftAeroForces(Vector3 localAcceleration, Vector3 localAngularAcceleration, float liftRatio, float control)
        {
            LocalAcceleration = localAcceleration;
            LocalAngularAcceleration = localAngularAcceleration;
            LiftRatio = liftRatio;
            Control = control;
        }

        public Vector3 LocalAcceleration { get; }

        public Vector3 LocalAngularAcceleration { get; }

        public float LiftRatio { get; }

        public float Control { get; }
    }

    // 推力と地上の力を AircraftFlight に残して空気から受ける分だけを出すのは、空力の作りを差し替えるときに推力や車輪の扱いまで書き直さずに済ませるため。
    public static class AircraftAero
    {
        private const float _degreesToRadians = (float)(Math.PI / 180.0);

        // 揚力を重さまでで止めるのは、迎え角を持たない作りで速いほど浮くと、操縦しなくても勝手に昇ってしまうため。
        public static float LiftRatio(AircraftAeroSpec aero, float forwardSpeed)
        {
            RequireAero(aero);
            var ratio = Math.Max(0f, forwardSpeed) / aero.LiftSpeed;
            return Math.Min(1f, ratio * ratio);
        }

        public static float Control(AircraftAeroSpec aero, float forwardSpeed)
        {
            RequireAero(aero);
            var ratio = Math.Min(1f, Math.Max(0f, forwardSpeed) / aero.ControlSpeed);
            return Math.Max(aero.MinControl, ratio);
        }

        // 降下では重さも前へ押すので、最高速度より速くなる。サーバーの速すぎ判定にはこちらを使う。
        public static float TopSpeed(AircraftSpec spec, float gravity)
        {
            RequireSpec(spec);
            RequireGravity(gravity);
            return (float)Math.Sqrt((spec.MaxThrust + gravity) / spec.DragCoefficient);
        }

        // 抗力の係数が推力から決まるので、空力の値だけでなく AircraftSpec 全体を受け取る。
        public static AircraftAeroForces Compute(
            AircraftSpec spec,
            VehicleMotorInput input,
            Vector3 localVelocity,
            Vector3 localAngularVelocity,
            float gravity,
            AircraftCondition condition = default)
        {
            RequireSpec(spec);
            RequireGravity(gravity);
            RequireFinite(localVelocity, nameof(localVelocity));
            RequireFinite(localAngularVelocity, nameof(localAngularVelocity));

            var aero = spec.Aero;
            var forwardSpeed = Math.Max(0f, localVelocity.Z);
            var lift = LiftRatio(aero, forwardSpeed);
            var control = Control(aero, forwardSpeed);

            var throttle = input[AircraftFlight.ThrottleAxis];
            var drag = spec.DragCoefficient * (1f + (Math.Max(0f, -throttle) * aero.AirBrakeDrag));
            var acceleration = new Vector3(0f, gravity * lift * condition.Lift, 0f) - (localVelocity * (localVelocity.Length() * drag));

            // 横と上下へ流れる速さを機首の向きへ寄せるのは、翼が空気を受ける代わりで、これが無いと機首を回しても進む向きが曲がらないため。
            acceleration -= new Vector3(localVelocity.X, localVelocity.Y, 0f) * (aero.Alignment * control);

            var target = new Vector3(
                -input[AircraftFlight.PitchAxis] * aero.PitchRate,
                input[AircraftFlight.YawAxis] * aero.YawRate,
                -input[AircraftFlight.RollAxis] * aero.RollRate) * (_degreesToRadians * control * condition.Control);
            var angular = (target - localAngularVelocity) * aero.Responsiveness;
            return new AircraftAeroForces(acceleration, angular, lift, control);
        }

        private static void RequireAero(AircraftAeroSpec aero)
        {
            if (aero == null)
            {
                throw new ArgumentNullException(nameof(aero));
            }
        }

        private static void RequireSpec(AircraftSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }
        }

        private static void RequireGravity(float gravity)
        {
            if (!(gravity >= 0f) || float.IsInfinity(gravity))
            {
                throw new ArgumentOutOfRangeException(nameof(gravity), gravity, "gravity must be a non-negative finite number");
            }
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite");
            }
        }
    }
}

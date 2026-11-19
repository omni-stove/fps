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
        private const float _radiansToDegrees = (float)(180.0 / Math.PI);

        // 臨界角を越えた先を平板の形 (迎え角の 2 倍の sin) にするのは、横から風を受ける 90 度や後ろ向きでも係数が続けて変わり、急に跳ねないようにするため。
        public static float LiftCoefficient(AircraftAeroSpec aero, float angleOfAttackRadians)
        {
            RequireAero(aero);
            if (!float.IsFinite(angleOfAttackRadians))
            {
                throw new ArgumentOutOfRangeException(nameof(angleOfAttackRadians), angleOfAttackRadians, "angleOfAttackRadians must be finite");
            }

            var degrees = angleOfAttackRadians * _radiansToDegrees;
            if (Math.Abs(degrees) <= aero.CriticalAngle)
            {
                return aero.LiftSlope * degrees;
            }

            return aero.PostStallLift * (float)Math.Sin(2.0 * angleOfAttackRadians);
        }

        // 重さで止めないのは、速く飛んで機首を引き起こしたときに重さより大きく持ち上げ、G が掛かって曲がれるようにするため。
        public static float LiftRatio(AircraftAeroSpec aero, Vector3 localVelocity)
        {
            RequireAero(aero);
            RequireFinite(localVelocity, nameof(localVelocity));
            var coefficient = LiftCoefficient(aero, AngleOfAttack(localVelocity));
            return coefficient / aero.MaxLiftCoefficient * (PlaneSpeedSquared(localVelocity) / (aero.LiftSpeed * aero.LiftSpeed));
        }

        public static float Control(AircraftAeroSpec aero, float forwardSpeed)
        {
            RequireAero(aero);
            var ratio = Math.Min(1f, Math.Max(0f, forwardSpeed) / aero.ControlSpeed);
            return Math.Max(aero.MinControl, ratio);
        }

        // 降下では重さも前へ押すので、最高速度より速くなる。サーバーの速すぎ判定にはこちらを使う。
        // 揚力で増える抗力は速さを落とす向きにしか働かないので、ここでは数えずに上の限りとして使う。
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
            var control = Control(aero, forwardSpeed);
            var angleOfAttack = AngleOfAttack(localVelocity);
            var coefficient = LiftCoefficient(aero, angleOfAttack);
            var lift = LiftRatio(aero, localVelocity);

            var throttle = input[AircraftFlight.ThrottleAxis];
            var drag = spec.DragCoefficient * (1f + (Math.Max(0f, -throttle) * aero.AirBrakeDrag));
            var acceleration = -(localVelocity * (localVelocity.Length() * drag));

            // 左右の翼に半分ずつ持たせるのは、片翼が壊れたときに持ち上げる力が減るだけでなく、残った側へ傾くようにするため。
            var wingLift = gravity * lift * 0.5f;
            var leftLift = wingLift * condition.LeftLift;
            var rightLift = wingLift * condition.RightLift;
            var planeSpeed = (float)Math.Sqrt(PlaneSpeedSquared(localVelocity));
            if (planeSpeed > 0f)
            {
                // 揚力を機体の上でなく風に直角な向きへ掛けるのは、迎え角が大きいほど揚力が後ろへ傾き、そのぶん速さを削るようにするため。
                var across = new Vector3(0f, localVelocity.Z, -localVelocity.Y) / planeSpeed;
                var along = new Vector3(0f, localVelocity.Y, localVelocity.Z) / planeSpeed;
                var totalLift = leftLift + rightLift;
                acceleration += across * totalLift;
                acceleration -= along * (aero.InducedDrag * coefficient * totalLift);
            }

            // 上下へ流れる速さを寄せないのは、それを揚力が受け持つようになり、寄せる力が残ると失速しても落ちなくなるため。
            acceleration -= new Vector3(localVelocity.X, 0f, 0f) * (aero.Alignment * control);

            var target = new Vector3(
                -input[AircraftFlight.PitchAxis] * aero.PitchRate,
                input[AircraftFlight.YawAxis] * aero.YawRate,
                -input[AircraftFlight.RollAxis] * aero.RollRate) * (_degreesToRadians * control * condition.Control);
            var angular = (target - localAngularVelocity) * aero.Responsiveness;

            var sideslip = Sideslip(localVelocity);
            var pressure = localVelocity.LengthSquared() / (aero.LiftSpeed * aero.LiftSpeed);
            angular += new Vector3(
                0f,
                aero.Weathervane * sideslip * pressure,
                (aero.Dihedral * sideslip * pressure) + ((rightLift - leftLift) * aero.WingRoll));
            return new AircraftAeroForces(acceleration, angular, lift, control);
        }

        // AircraftFlightState と同じ測り方にするのは、HUD に出る迎え角と揚力を決める迎え角が食い違わないようにするため。
        private static float AngleOfAttack(Vector3 localVelocity)
        {
            return (float)Math.Atan2(-localVelocity.Y, localVelocity.Z);
        }

        private static float Sideslip(Vector3 localVelocity)
        {
            var speed = localVelocity.Length();
            return speed > 0f ? (float)Math.Asin(Math.Clamp(localVelocity.X / speed, -1f, 1f)) : 0f;
        }

        // 横へ流れる分を除くのは、翼が受けて揚力になるのは機体の上下と前後を含む面を流れる風だけのため。
        private static float PlaneSpeedSquared(Vector3 localVelocity)
        {
            return (localVelocity.Y * localVelocity.Y) + (localVelocity.Z * localVelocity.Z);
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

using System;

namespace Shooter.Core
{
    public static class HorseStride
    {
        public const int ThrottleAxis = 0;

        public const int TurnAxis = 1;

        public const int GallopAxis = 2;

        public const int JumpAxis = 3;

        public const float WalkThrottle = 0.5f;

        // これより速く逆へ進んでいる間は、逆向きの入力を止まる指示として扱う。前へ駆けている馬がその場で後ずさりを始めないため。
        public const float DirectionChangeSpeed = 0.5f;

        public static float TargetSpeed(HorseSpec spec, VehicleMotorInput input, float forwardSpeed)
        {
            RequireSpec(spec);
            RequireFinite(forwardSpeed, nameof(forwardSpeed));

            var throttle = input[ThrottleAxis];
            if (throttle > 0f)
            {
                if (forwardSpeed < -DirectionChangeSpeed)
                {
                    return 0f;
                }

                var stride = throttle <= WalkThrottle
                    ? spec.WalkSpeed * (throttle / WalkThrottle)
                    : Lerp(spec.WalkSpeed, spec.TrotSpeed, (throttle - WalkThrottle) / (1f - WalkThrottle));
                // 負の Gallop を捨てるのは、軸が -1 から 1 まで取れても、Canter より遅くする向きは無いため。
                var gallop = Math.Max(0f, input[GallopAxis]);
                return Lerp(stride, spec.CanterSpeed, gallop);
            }

            if (throttle < 0f)
            {
                if (forwardSpeed > DirectionChangeSpeed)
                {
                    return 0f;
                }

                return throttle * spec.ReverseSpeed;
            }

            return 0f;
        }

        public static float Step(HorseSpec spec, float currentSpeed, float targetSpeed, float deltaTime)
        {
            RequireSpec(spec);
            RequireFinite(currentSpeed, nameof(currentSpeed));
            RequireFinite(targetSpeed, nameof(targetSpeed));
            if (!(deltaTime >= 0f) || float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "deltaTime must be a non-negative finite number");
            }

            // 0 をまたぐときは、まず 0 で止める。減速と加速の割合が違うので、またいだ分を同じ割合で進めると速さが合わないため。
            if ((currentSpeed > 0f && targetSpeed < 0f) || (currentSpeed < 0f && targetSpeed > 0f))
            {
                return MoveTowards(currentSpeed, 0f, spec.Deceleration * deltaTime);
            }

            var speedingUp = Math.Abs(targetSpeed) > Math.Abs(currentSpeed);
            var rate = speedingUp ? spec.Acceleration : spec.Deceleration;
            return MoveTowards(currentSpeed, targetSpeed, rate * deltaTime);
        }

        public static float TurnRate(HorseSpec spec, float forwardSpeed)
        {
            RequireSpec(spec);
            RequireFinite(forwardSpeed, nameof(forwardSpeed));

            var pace = Math.Min(1f, Math.Abs(forwardSpeed) / spec.CanterSpeed);
            return spec.TurnRate * Lerp(1f, spec.CanterTurnScale, pace);
        }

        public static HorseGait GaitOf(HorseSpec spec, float forwardSpeed)
        {
            RequireSpec(spec);
            RequireFinite(forwardSpeed, nameof(forwardSpeed));

            // 向きを捨てるのは、後ずさりも脚の運びは Walk と同じで、歩き方の段階は速さの大きさだけで決まるため。
            var speed = Math.Abs(forwardSpeed);
            if (speed >= spec.CanterThreshold)
            {
                return HorseGait.Canter;
            }

            if (speed >= spec.TrotThreshold)
            {
                return HorseGait.Trot;
            }

            return speed >= spec.WalkThreshold ? HorseGait.Walk : HorseGait.Idle;
        }

        private static float Lerp(float from, float to, float t)
        {
            return from + ((to - from) * t);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
            {
                return target;
            }

            return current + (Math.Sign(target - current) * maxDelta);
        }

        private static void RequireSpec(HorseSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }
        }

        private static void RequireFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite");
            }
        }
    }
}

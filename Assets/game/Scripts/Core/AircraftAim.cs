using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class AircraftAim
    {
        public static float StepThrottle(AircraftAimSpec spec, float level, float push, float deltaSeconds)
        {
            RequireSpec(spec);
            if (!(level >= 0f && level <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "level must be between 0 and 1");
            }

            if (!(Math.Abs(push) <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(push), push, "push must be between -1 and 1");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            return Clamp01(level + (push * spec.ThrottleRate * deltaSeconds));
        }

        // 開き具合が 0 のまま下げ続けたときだけエアブレーキ (負) にするのは、絞っている途中で減速まで掛かると、速さを残して絞れないため。
        public static float ThrottleAxis(float level, float push)
        {
            return level > 0f ? level : Math.Min(0f, Clamp(push));
        }

        public static VehicleMotorInput Compute(AircraftAimSpec spec, Vector3 localAim, float rightWingUp, float rollPush, float throttleAxis)
        {
            RequireSpec(spec);
            if (!float.IsFinite(localAim.X) || !float.IsFinite(localAim.Y) || !float.IsFinite(localAim.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(localAim), localAim, "localAim must be finite");
            }

            if (!(Math.Abs(rightWingUp) <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(rightWingUp), rightWingUp, "rightWingUp must be between -1 and 1");
            }

            var off = Off(localAim);

            // 傾きを戻す分を足すのは、曲がりたい向きの分と釣り合う傾きで止まり、視線を正面へ戻せば水平へ戻るようにするため。
            var roll = Clamp((off.X * spec.RollGain) + (rightWingUp * spec.LevelGain) + rollPush);
            var pitch = Clamp(off.Y * spec.PitchGain);
            var yaw = Clamp(off.X * spec.YawGain);
            return VehicleMotorInput.Idle
                .With(AircraftFlight.ThrottleAxis, Clamp(throttleAxis))
                .With(AircraftFlight.PitchAxis, pitch)
                .With(AircraftFlight.RollAxis, roll)
                .With(AircraftFlight.YawAxis, yaw);
        }

        // 後ろを見たときに横と上下の成分を伸ばすのは、真後ろ近くでは成分が小さく、振り向くのが遅くなるため。真後ろなら機首を上げて回る。
        private static Vector2 Off(Vector3 localAim)
        {
            var length = localAim.Length();
            if (!(length > 1e-6f))
            {
                return Vector2.Zero;
            }

            var aim = localAim / length;
            var off = new Vector2(aim.X, aim.Y);
            if (aim.Z >= 0f)
            {
                return off;
            }

            var offLength = off.Length();
            return offLength > 1e-4f ? off / offLength : Vector2.UnitY;
        }

        private static float Clamp(float value) => Math.Max(-1f, Math.Min(1f, value));

        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));

        private static void RequireSpec(AircraftAimSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }
        }
    }
}

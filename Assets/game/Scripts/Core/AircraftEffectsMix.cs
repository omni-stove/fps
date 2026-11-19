using System;

namespace Shooter.Core
{
    public enum AircraftControlSurfaceRole
    {
        Elevator = 0,
        Aileron = 1,
        Rudder = 2,
    }

    // 演出の値を Runtime でなくここで出すのは、Unity を起こさずに舵の角度・音の高さ・ベイパーの出る条件を確かめられるようにするため。
    public static class AircraftEffectsMix
    {
        public static float SurfaceAngle(AircraftEffectsSpec spec, VehicleMotorInput input, AircraftControlSurfaceRole role)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            switch (role)
            {
                case AircraftControlSurfaceRole.Elevator: return input[AircraftFlight.PitchAxis] * spec.ElevatorAngle;
                case AircraftControlSurfaceRole.Aileron: return input[AircraftFlight.RollAxis] * spec.AileronAngle;
                case AircraftControlSurfaceRole.Rudder: return input[AircraftFlight.YawAxis] * spec.RudderAngle;
                default: throw new ArgumentOutOfRangeException(nameof(role), role, "role is not a defined AircraftControlSurfaceRole");
            }
        }

        // 負のスロットルを 0 とみなすのは、AircraftFlight が負の軸で推力を出さず、音だけ上がると飛び方と食い違うため。
        public static float EngineLevel(AircraftEffectsSpec spec, VehicleMotorInput input, float airspeed, float maxSpeed)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!(maxSpeed > 0f) || float.IsInfinity(maxSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSpeed), maxSpeed, "maxSpeed must be positive and finite");
            }

            if (!float.IsFinite(airspeed))
            {
                throw new ArgumentOutOfRangeException(nameof(airspeed), airspeed, "airspeed must be finite");
            }

            var throttle = Math.Max(0f, input[AircraftFlight.ThrottleAxis]);
            var speed = Math.Clamp(Math.Abs(airspeed) / maxSpeed, 0f, 1f);
            return Math.Clamp((throttle * (1f - spec.EngineSpeedShare)) + (speed * spec.EngineSpeedShare), 0f, 1f);
        }

        public static float EnginePitch(AircraftEffectsSpec spec, float level)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return Lerp(spec.MinEnginePitch, spec.MaxEnginePitch, level);
        }

        public static float EngineVolume(AircraftEffectsSpec spec, float level)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return Lerp(spec.MinEngineVolume, spec.MaxEngineVolume, level);
        }

        // 負の G も数えるのは、押し下げる急な機動でも翼の上で空気が冷えて筋が出るため。地上では曲がる力が車輪から来るので出さない。
        public static bool VaporOn(AircraftEffectsSpec spec, AircraftFlightState state)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return !state.Grounded && Math.Abs(state.LoadFactor) > spec.VaporLoadFactor;
        }

        private static float Lerp(float from, float to, float t)
        {
            var clamped = float.IsNaN(t) ? 0f : Math.Clamp(t, 0f, 1f);
            return from + ((to - from) * clamped);
        }
    }
}

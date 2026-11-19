using System;
using System.Numerics;

namespace Shooter.Core
{
    // 向きをクォータニオンでなく前と上の向きで持つのは、Unity と System.Numerics で回る向きの約束が違い、変換の取り違えが起きないようにするため。
    public readonly struct AircraftAutopilotSituation
    {
        public AircraftAutopilotSituation(
            Vector3 position,
            Vector3 forward,
            Vector3 up,
            Vector3 velocity,
            AircraftFlightState state,
            float groundDistance,
            float gravity)
        {
            RequireFinite(position, nameof(position));
            RequireFinite(velocity, nameof(velocity));
            Forward = RequireDirection(forward, nameof(forward));
            Up = RequireDirection(up, nameof(up));
            if (!float.IsFinite(state.LoadFactor) || !float.IsFinite(state.AngleOfAttackRadians))
            {
                throw new ArgumentOutOfRangeException(nameof(state), state, "state must have a finite load factor and angle of attack");
            }

            // 無限大を許すのは、下に地面が見つからないときをそのまま渡せるようにするため。
            if (!(groundDistance >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(groundDistance), groundDistance, "groundDistance must be zero or more");
            }

            if (!(gravity >= 0f) || float.IsInfinity(gravity))
            {
                throw new ArgumentOutOfRangeException(nameof(gravity), gravity, "gravity must be a non-negative finite number");
            }

            Position = position;
            Velocity = velocity;
            State = state;
            GroundDistance = groundDistance;
            Gravity = gravity;
        }

        public Vector3 Position { get; }

        public Vector3 Forward { get; }

        public Vector3 Up { get; }

        // 右を別に受け取らず外積で出すのは、前と上に食い違う右を渡されて、傾きの向きを取り違えないようにするため。
        public Vector3 Right => Vector3.Cross(Up, Forward);

        public Vector3 Velocity { get; }

        public AircraftFlightState State { get; }

        public float GroundDistance { get; }

        public float Gravity { get; }

        public float ClimbRate => Velocity.Y;

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite");
            }
        }

        private static Vector3 RequireDirection(Vector3 value, string name)
        {
            RequireFinite(value, name);
            var length = value.Length();
            if (!(length > 1e-6f))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must not be zero");
            }

            return value / length;
        }
    }

    public static class AircraftAutopilot
    {
        private const float _degreesToRadians = (float)(Math.PI / 180.0);
        private const float _radiansToDegrees = (float)(180.0 / Math.PI);
        private const float _flatEpsilon = 1e-3f;

        // 高さを目標の点から取らず別に渡すのは、地面に置いた点を回りながら決めた高さを保つ使い方と、点ごとの高さをなぞる使い方の両方を、呼ぶ側が選べるようにするため。
        public static VehicleMotorInput Compute(
            AircraftAutopilotSpec spec, AircraftAutopilotSituation situation, Vector3 target, float targetAltitude)
        {
            RequireSpec(spec);
            if (!float.IsFinite(target.X) || !float.IsFinite(target.Y) || !float.IsFinite(target.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(target), target, "target must be finite");
            }

            if (!float.IsFinite(targetAltitude))
            {
                throw new ArgumentOutOfRangeException(nameof(targetAltitude), targetAltitude, "targetAltitude must be finite");
            }

            // 地面が近いときは翼を水平に戻して上れるだけ上るのは、傾けたまま機首を上げると、上がる分が横へ逃げて地面から離れられないため。
            var low = IsLow(spec, situation);
            var headingError = HeadingError(situation, target);
            var steer = !low && CanTurnTo(spec, situation, target, headingError);
            var bank = steer ? Clamp(headingError * spec.HeadingGain, spec.MaxBankAngle) : 0f;
            var roll = Clamp(WrapDegrees(bank - BankAngle(situation)) * spec.RollGain, 1f);
            var yaw = steer ? Clamp(headingError * spec.YawGain, 1f) : 0f;

            var climb = low
                ? spec.MaxClimbAngle
                : Clamp((targetAltitude - situation.Position.Y) * spec.AltitudeGain, spec.MaxClimbAngle);
            var pitch = Clamp((climb - FlightPathAngle(situation)) * spec.PitchGain, 1f);

            // 背面のときに引かないのは、機体から見た上が地面を向いており、機首を上げると地面へ向かうため。翼を戻す間だけ待つ。
            if (!(situation.Up.Y > 0f))
            {
                pitch = 0f;
            }

            pitch = LimitPitch(spec, pitch, situation.State);
            var throttle = low ? 1f : Clamp01(spec.CruiseThrottle + ((spec.CruiseSpeed - situation.Velocity.Length()) * spec.SpeedGain));
            return VehicleMotorInput.Idle
                .With(AircraftFlight.ThrottleAxis, throttle)
                .With(AircraftFlight.PitchAxis, pitch)
                .With(AircraftFlight.RollAxis, roll)
                .With(AircraftFlight.YawAxis, yaw);
        }

        // 接地中も低い扱いにするのは、滑走路に置いたまま渡されたときに、翼を水平に保って開き切り、浮けば上がっていくようにするため。
        public static bool IsLow(AircraftAutopilotSpec spec, AircraftAutopilotSituation situation)
        {
            RequireSpec(spec);
            if (situation.State.Grounded)
            {
                return true;
            }

            var sink = Math.Max(0f, -situation.ClimbRate);
            return situation.GroundDistance - (sink * spec.GroundLookAheadSeconds) < spec.MinGroundClearance;
        }

        public static bool HasArrived(AircraftAutopilotSpec spec, Vector3 position, Vector3 target)
        {
            RequireSpec(spec);
            var dx = target.X - position.X;
            var dz = target.Z - position.Z;
            return (dx * dx) + (dz * dz) <= spec.ArrivalRadius * spec.ArrivalRadius;
        }

        // AircraftAim.LimitPitch と同じ絞り方にするのは、人が操縦するときと Bot が操縦するときで、失速の手前で止まる感じをそろえるため。
        // 値を AircraftAimSpec から借りないのは、Bot だけ余裕を広く取って、人より慎重に飛ばせるようにするため。
        public static float LimitPitch(AircraftAutopilotSpec spec, float pitch, AircraftFlightState state)
        {
            RequireSpec(spec);
            if (!(Math.Abs(pitch) <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "pitch must be between -1 and 1");
            }

            if (state.Grounded || pitch == 0f)
            {
                return pitch;
            }

            var sign = pitch > 0f ? 1f : -1f;
            var loadRoom = Room(spec.MaxLoadFactor, spec.LoadFactorMargin, state.LoadFactor * sign);
            var angleRoom = Room(spec.CriticalAngleOfAttack, spec.AngleOfAttackMargin, state.AngleOfAttackRadians * sign * _radiansToDegrees);
            return pitch * Math.Min(loadRoom, angleRoom);
        }

        // 右へ傾く (右の翼が下がる) 向きを正にするのは、ロールの舵 (AircraftFlight.RollAxis) を正に倒したときに傾く向きと合わせるため。
        public static float BankAngle(AircraftAutopilotSituation situation)
        {
            return (float)Math.Atan2(-situation.Right.Y, situation.Up.Y) * _radiansToDegrees;
        }

        // 機首の向きでなく速さの向きで測るのは、迎え角や横滑りの分だけ機首が逸れていても、実際に上っているか下っているかで高さを合わせるため。
        public static float FlightPathAngle(AircraftAutopilotSituation situation)
        {
            var velocity = situation.Velocity;
            var speed = velocity.Length();
            var direction = speed > _flatEpsilon ? velocity / speed : situation.Forward;
            return (float)Math.Asin(Math.Clamp(direction.Y, -1f, 1f)) * _radiansToDegrees;
        }

        // 右を正にするのは、ロールとヨーの舵を正に倒すと右へ曲がるため。
        public static float HeadingError(AircraftAutopilotSituation situation, Vector3 target)
        {
            var toTarget = Flatten(target - situation.Position);
            var heading = Flatten(situation.Forward);
            if (heading.LengthSquared() < _flatEpsilon * _flatEpsilon)
            {
                // 真上や真下を向いているときは機首から向きが決まらないので、進んでいる向きで測る。
                heading = Flatten(situation.Velocity);
            }

            if (toTarget.LengthSquared() < _flatEpsilon * _flatEpsilon || heading.LengthSquared() < _flatEpsilon * _flatEpsilon)
            {
                return 0f;
            }

            var cross = (heading.Z * toTarget.X) - (heading.X * toTarget.Z);
            var dot = (heading.X * toTarget.X) + (heading.Z * toTarget.Z);
            return (float)Math.Atan2(cross, dot) * _radiansToDegrees;
        }

        // 点が今の速さで傾け切って回る輪の内側にあるときに曲がらないのは、いくら回っても届かず、点の周りを回り続けるため。まっすぐ離れてから回り直す。
        private static bool CanTurnTo(AircraftAutopilotSpec spec, AircraftAutopilotSituation situation, Vector3 target, float headingError)
        {
            var heading = Flatten(situation.Forward);
            var speedSquared = Flatten(situation.Velocity).LengthSquared();
            if (headingError == 0f || situation.Gravity <= 0f || heading.LengthSquared() < _flatEpsilon * _flatEpsilon)
            {
                return true;
            }

            heading = Vector3.Normalize(heading);
            var radius = speedSquared / (situation.Gravity * (float)Math.Tan(spec.MaxBankAngle * _degreesToRadians));
            var right = new Vector3(heading.Z, 0f, -heading.X);
            var center = Flatten(situation.Position) + (right * (Math.Sign(headingError) * radius));
            return Vector3.DistanceSquared(Flatten(target), center) >= radius * radius;
        }

        private static float Room(float limit, float margin, float value)
        {
            if (!(value < limit))
            {
                return 0f;
            }

            var start = limit - margin;
            return value <= start ? 1f : (limit - value) / margin;
        }

        private static Vector3 Flatten(Vector3 value) => new Vector3(value.X, 0f, value.Z);

        private static float WrapDegrees(float degrees)
        {
            var wrapped = degrees % 360f;
            if (wrapped > 180f)
            {
                return wrapped - 360f;
            }

            return wrapped < -180f ? wrapped + 360f : wrapped;
        }

        private static float Clamp(float value, float limit) => Math.Max(-limit, Math.Min(limit, value));

        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));

        private static void RequireSpec(AircraftAutopilotSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }
        }
    }
}

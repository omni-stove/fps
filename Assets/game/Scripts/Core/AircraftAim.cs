using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class AircraftAim
    {
        private const float _radiansToDegrees = (float)(180.0 / Math.PI);

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

        public static VehicleMotorInput Compute(
            AircraftAimSpec spec,
            Vector3 localAim,
            float rightWingUp,
            float rollPush,
            float throttleAxis,
            AircraftAimSituation situation = default)
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

            // 引き起こすときに視線の代わりに真上を狙うのは、背面で突っ込んでいても、機体から見た真上へ向かう当て方なら回って起き上がれるため。
            // ロールのキーを捨てるのは、引き起こしの最中に傾けると、機首の上がる向きが地面から逸れるため。
            var pullUp = ShouldPullUp(spec, situation);
            var off = Off(pullUp ? situation.LocalUp : localAim);
            var push = pullUp ? 0f : rollPush;

            // 傾きを戻す分を足すのは、曲がりたい向きの分と釣り合う傾きで止まり、視線を正面へ戻せば水平へ戻るようにするため。
            var roll = Clamp((off.X * spec.RollGain) + (rightWingUp * spec.LevelGain) + push);
            var pitch = LimitPitch(spec, Clamp(off.Y * spec.PitchGain), situation.State);
            var yaw = Clamp(off.X * spec.YawGain);
            return VehicleMotorInput.Idle
                .With(AircraftFlight.ThrottleAxis, Clamp(throttleAxis))
                .With(AircraftFlight.PitchAxis, pitch)
                .With(AircraftFlight.RollAxis, roll)
                .With(AircraftFlight.YawAxis, yaw);
        }

        // 接地中は引き起こさないのは、滑走や着陸の接地で地面までの距離が 0 になり、毎回機首を上げてしまうため。
        public static bool ShouldPullUp(AircraftAimSpec spec, AircraftAimSituation situation)
        {
            RequireSpec(spec);
            return !situation.State.Grounded
                && situation.SinkRate > spec.MinSinkRate
                && situation.SecondsToGround < spec.GroundWarningSeconds;
        }

        // 絞る量を、上限の手前の余裕の中で 1 から 0 へ下げるのは、上限で急に舵を抜くと、機首が跳ねて G と迎え角が上限を行き来するため。
        // 機首を上げるときは正の G と迎え角を、下げるときは負の G と迎え角を見るのは、限界から離れる向きの操作まで止めると、失速から抜けられないため。
        // 接地中に絞らないのは、車輪が受ける力が G に入り、着地の弾みで離陸の引き起こしまで止まるため。
        public static float LimitPitch(AircraftAimSpec spec, float pitch, AircraftFlightState state)
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

        // 数でない値を上限に達した扱いにするのは、壊れた状態のまま舵を通すと、機体が何をするか分からないため。
        private static float Room(float limit, float margin, float value)
        {
            if (!(value < limit))
            {
                return 0f;
            }

            var start = limit - margin;
            return value <= start ? 1f : (limit - value) / margin;
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

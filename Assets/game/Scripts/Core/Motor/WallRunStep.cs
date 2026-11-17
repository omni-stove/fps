using System;
using System.Numerics;

namespace Shooter.Core
{
    public enum WallRunMotion : byte
    {
        None = 0,
        Running = 1,
        JumpedOff = 2,
    }

    public readonly struct WallRunResult
    {
        public WallRunResult(CharacterWallRunState state, WallRunMotion motion, Vector3 velocity, Vector3 displacement)
        {
            State = state;
            Motion = motion;

            // 動かさない結果に速度を残すと、ふつうの移動に任せた tick で読む側が古い値を使いかねない
            Velocity = motion == WallRunMotion.None ? Vector3.Zero : velocity;
            Displacement = motion == WallRunMotion.None ? Vector3.Zero : displacement;
        }

        public CharacterWallRunState State { get; }

        public WallRunMotion Motion { get; }

        public Vector3 Velocity { get; }

        public Vector3 Displacement { get; }

        public static WallRunResult Resting(CharacterWallRunState state)
        {
            return new WallRunResult(state, WallRunMotion.None, Vector3.Zero, Vector3.Zero);
        }
    }

    // 壁を呼び手が CharacterWallSensor で調べて渡すのは、世界を引かない計算にして、壁の有無ごとの決まりを世界を組まずに確かめるため。
    // 同じ壁に付き直さないのを秒数でなく着地までにしたのは、待ちの秒数を調整値に足すと、サーバーへ送る設定とその書き出しまで増えるため。
    public static class WallRunStep
    {
        public const float SameWallDot = 0.95f;

        public static WallRunResult Step(
            CharacterWallRunState wallRun,
            WallContact wall,
            Vector3 velocity,
            Vector2 move,
            bool jump,
            bool grounded,
            MotorSpec spec,
            WallRunTuning tuning,
            float tickSeconds)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (!(tickSeconds > 0f) || float.IsInfinity(tickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "tickSeconds must be positive and finite");
            }

            if (grounded || !tuning.Enabled)
            {
                return WallRunResult.Resting(Stop(wallRun, grounded));
            }

            return wallRun.IsRunning
                ? Continue(wallRun, wall, velocity, move, jump, spec, tuning, tickSeconds)
                : TryStart(wallRun, wall, velocity, move, jump, spec, tuning, tickSeconds);
        }

        public static CharacterWallRunState Stop(CharacterWallRunState wallRun, bool grounded)
        {
            if (grounded)
            {
                return CharacterWallRunState.None;
            }

            return wallRun.IsRunning ? CharacterWallRunState.LeftWall(wallRun.Normal) : wallRun;
        }

        public static bool IsSameWall(Vector3 lastWall, Vector3 normal)
        {
            return lastWall != Vector3.Zero && Vector3.Dot(lastWall, normal) >= SameWallDot;
        }

        public static Vector3 AlongWall(Vector3 velocity, Vector3 normal)
        {
            var horizontal = new Vector3(velocity.X, 0f, velocity.Z);
            return horizontal - (normal * Vector3.Dot(horizontal, normal));
        }

        private static WallRunResult TryStart(
            CharacterWallRunState wallRun, WallContact wall, Vector3 velocity, Vector2 move, bool jump, MotorSpec spec, WallRunTuning tuning, float tickSeconds)
        {
            // ジャンプは空中で跳ぶのに使い、同じ tick に壁へ付かせない
            if (jump || !wall.IsFound || !(move.Y > 0f) || IsSameWall(wallRun.LastWall, wall.Normal))
            {
                return WallRunResult.Resting(wallRun);
            }

            var along = AlongWall(velocity, wall.Normal);
            if (along.Length() < tuning.MinSpeed)
            {
                return WallRunResult.Resting(wallRun);
            }

            // 落ちている勢いのまま付くと、重力を弱めても壁をすぐ滑り落ちるので、落ちる速さは捨てて付く
            var vertical = Math.Max(velocity.Y, 0f);
            var state = new CharacterWallRunState(wall.Side, tickSeconds, wall.Normal);
            return Run(state, along, vertical, spec.Gravity * tuning.GravityScale, tickSeconds);
        }

        private static WallRunResult Continue(
            CharacterWallRunState wallRun, WallContact wall, Vector3 velocity, Vector2 move, bool jump, MotorSpec spec, WallRunTuning tuning, float tickSeconds)
        {
            if (jump)
            {
                return JumpOff(wallRun.Normal, velocity, spec, tuning, tickSeconds);
            }

            var ended = WallRunResult.Resting(CharacterWallRunState.LeftWall(wallRun.Normal));
            if (wallRun.Elapsed >= tuning.MaxSeconds || !wall.IsFound || wall.Side != wallRun.Side || move.Y < 0f)
            {
                return ended;
            }

            var along = AlongWall(velocity, wall.Normal);
            if (along.Length() < tuning.MinSpeed)
            {
                return ended;
            }

            var state = new CharacterWallRunState(wallRun.Side, wallRun.Elapsed + tickSeconds, wall.Normal);
            return Run(state, along, velocity.Y, spec.Gravity * tuning.GravityScale, tickSeconds);
        }

        // 上への速さを調整値に足さず地上のジャンプの高さから出すのは、サーバーへ送る設定を増やさないため。
        // 壁に沿った勢いを捨てないのは、走った向きへそのまま跳び移れるようにするため
        private static WallRunResult JumpOff(Vector3 normal, Vector3 velocity, MotorSpec spec, WallRunTuning tuning, float tickSeconds)
        {
            var horizontal = AlongWall(velocity, normal) + (normal * tuning.JumpOffSpeed);
            var up = MathF.Sqrt(2f * spec.Gravity * spec.JumpHeight);
            var result = Run(CharacterWallRunState.LeftWall(normal), horizontal, up, spec.Gravity, tickSeconds);
            return new WallRunResult(result.State, WallRunMotion.JumpedOff, result.Velocity, result.Displacement);
        }

        // PlayerMotor と同じく縦は台形則で動かし、tick の長さによらず同じ高さにする
        private static WallRunResult Run(CharacterWallRunState state, Vector3 horizontal, float verticalStart, float gravity, float tickSeconds)
        {
            var verticalEnd = verticalStart - (gravity * tickSeconds);
            var displacement = new Vector3(
                horizontal.X * tickSeconds, (verticalStart + verticalEnd) * 0.5f * tickSeconds, horizontal.Z * tickSeconds);
            return new WallRunResult(state, WallRunMotion.Running, new Vector3(horizontal.X, verticalEnd, horizontal.Z), displacement);
        }
    }
}

using System;
using System.Numerics;

namespace Shooter.Core
{
    // 効くかと壁は tick の始めの状態と体の位置だけから決める。予測・再シミュレーション・サーバーで、同じ状態と入力から同じ傾きにするため。
    public static class LeanStep
    {
        public static CharacterLeanState Step(
            CharacterMotorState state, CharacterMotorInput input, CharacterSwimState swim, CharacterBody body, MotorSpec spec, float tickSeconds)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var held = IsAllowed(state, input, swim) ? input.Lean : 0;
            var position = body.Position;
            var heldRoom = held == 0 ? 0f : Room(body, position, input.Yaw, held, spec);
            var next = Toward(state.Lean.Amount, held * heldRoom, spec, tickSeconds);
            var leaning = Math.Sign(next);

            // 戻る途中でも、近づいた壁へ体を入れたまま戻さない
            if (leaning != 0)
            {
                var room = leaning == held ? heldRoom : Room(body, position, input.Yaw, leaning, spec);
                if (Math.Abs(next) > room)
                {
                    next = leaning * room;
                }
            }

            return new CharacterLeanState(next);
        }

        public static bool IsAllowed(CharacterMotorState state, CharacterMotorInput input, CharacterSwimState swim)
        {
            return !state.Cover.IsInCover
                && !IsDashing(state, input)
                && !state.IsProne
                && !state.Prone.IsActive
                && !state.IsDiving
                && !state.IsClimbing
                && !state.IsVaulting
                && !state.IsParachuting
                && !swim.IsSwimming;
        }

        public static float Toward(float amount, float target, MotorSpec spec, float tickSeconds)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!(tickSeconds >= 0f) || float.IsInfinity(tickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "tickSeconds must be finite and not negative");
            }

            target = Clamp(target, -1f, 1f);
            amount = float.IsNaN(amount) ? 0f : Clamp(amount, -1f, 1f);
            var maxDelta = spec.LeanSpeed * tickSeconds;
            return Math.Abs(target - amount) <= maxDelta ? target : amount + (Math.Sign(target - amount) * maxDelta);
        }

        public static float Room(CharacterBody body, Vector3 position, float yaw, int side, MotorSpec spec)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (side != -1 && side != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(side), side, "side must be -1 or 1");
            }

            var reach = spec.LeanSideOffset;
            if (reach <= 0f)
            {
                return 1f;
            }

            var shape = body.Current;
            var radius = body.Radius;

            // 目が頭の球より上にあっても、体の外 (天井の縁など) で止めない
            var y = Math.Min(shape.EyeY, shape.TopY - radius);
            var center = new Vector3(position.X, position.Y + y, position.Z);
            var skin = body.MoverSpec.SkinWidth;
            if (!body.World.CapsuleCast(center, center, radius, Right(yaw) * side, reach + skin, body.LayerMask, out var hit))
            {
                return 1f;
            }

            // 壁に触れるまで寄せると次の tick に「重なっている」とみなされて壁を素通りするので、CapsuleMover と同じく skin だけ離す
            return Clamp((hit.Distance - skin) / reach, 0f, 1f);
        }

        public static Vector3 EyeOffset(float amount, float yaw, MotorSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (float.IsNaN(amount))
            {
                return Vector3.Zero;
            }

            return Right(yaw) * (Clamp(amount, -1f, 1f) * spec.LeanSideOffset);
        }

        public static Vector3 EyeOffset(CharacterLeanState lean, float yaw, MotorSpec spec)
        {
            return EyeOffset(lean.Amount, yaw, spec);
        }

        // 止まったまま・しゃがみや伏せで押しているだけでは、PlayerMotor でダッシュの速さにならない
        private static bool IsDashing(CharacterMotorState state, CharacterMotorInput input)
        {
            return input.Sprint && !input.Crouch && state.Stance == PlayerStance.Standing && input.Move.LengthSquared() > 1e-4f;
        }

        private static Vector3 Right(float yaw)
        {
            var forward = CharacterMotorSimulation.YawForward(yaw);
            return new Vector3(forward.Z, 0f, -forward.X);
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}

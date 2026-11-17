using System;
using System.Numerics;

namespace Shooter.Core
{
    // 胸と目の高さを泳ぐ間の姿勢である立ちの寸法で測るのは、しゃがんだまま水に入った tick も、入ったかの答えを姿勢で変えないため。
    public static class SwimStep
    {
        public static CharacterSwimState Step(
            CharacterSwimState swim, Vector3 position, bool grounded, StanceGeometry geometry, IWaterVolumes water, SwimTuning tuning, float tickSeconds)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            if (water == null)
            {
                throw new ArgumentNullException(nameof(water));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (!(tickSeconds > 0f) || float.IsInfinity(tickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "tickSeconds must be positive and finite");
            }

            var standing = geometry.Standing;
            var feetY = position.Y + standing.BottomY;
            var surface = water.SurfaceAt(new Vector3(position.X, feetY, position.Z));
            if (!surface.HasValue || float.IsNaN(surface.Value) || float.IsInfinity(surface.Value))
            {
                return CharacterSwimState.None;
            }

            var surfaceY = surface.Value;
            var chestY = position.Y + ChestY(geometry);

            // 浮いている間は水面がちょうど胸に来るので、入るのと同じ境で出すと浮くたびに出入りを繰り返す。底に足が着いたか、体が水から出きったときだけ出す
            var swimming = swim.IsSwimming
                ? !(feetY >= surfaceY || (grounded && surfaceY <= chestY))
                : surfaceY > chestY;
            if (!swimming)
            {
                return CharacterSwimState.None;
            }

            var underwater = position.Y + standing.EyeY < surfaceY;
            var drownings = swim.IsSwimming ? swim.Drownings : 0;
            var breath = 0f;
            if (underwater)
            {
                var used = swim.BreathUsedSeconds >= 0f && !float.IsInfinity(swim.BreathUsedSeconds) ? swim.BreathUsedSeconds : 0f;
                breath = used + tickSeconds;
                // 回数を潜った累計秒数から式で出すのは、tick が間隔よりずっと長い設定でも 1 回で数え終え、DrowningClock と同じ回数にするため
                var hits = DueHits(breath, tuning) - DueHits(used, tuning);
                drownings = (int)Math.Min(int.MaxValue, drownings + (double)Math.Max(0L, hits));
            }

            return new CharacterSwimState(true, surfaceY, underwater, breath, drownings);
        }

        // 胸の高さを値として持たせないのは、体の寸法を変えたゲームでも胸が体に付いて動くようにするため。
        public static float ChestY(StanceGeometry geometry)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            var standing = geometry.Standing;
            return (standing.CenterY + standing.EyeY) * 0.5f;
        }

        private static long DueHits(float usedSeconds, SwimTuning tuning)
        {
            var over = usedSeconds - tuning.BreathSeconds;
            return over < 0f ? 0L : (long)Math.Floor(over / (double)tuning.DrownIntervalSeconds) + 1;
        }

        public static float FloatY(float surfaceY, StanceGeometry geometry)
        {
            return surfaceY - ChestY(geometry);
        }

        // 体を動かさず、速度と変位だけを返す。予測・サーバーの CapsuleMover とオフラインの CharacterController の両方が、同じ泳ぎの計算で動くようにするため。
        // 縦は重力の代わりに浮く力で、水面が胸に来る高さ (gapToFloat = 浮く高さ - 今の高さ) へ寄せる。ascend で泳ぐ速さで上がり、dive で潜る。
        public static Vector3 Paddle(
            bool wasSwimming,
            Vector2 move,
            float yaw,
            bool dive,
            bool ascend,
            float verticalSpeed,
            float gapToFloat,
            float carrySpeedMultiplier,
            SwimTuning tuning,
            float tickSeconds,
            out Vector3 displacement)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (!(tickSeconds > 0f) || float.IsInfinity(tickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "tickSeconds must be positive and finite");
            }

            if (!(carrySpeedMultiplier >= 0f) || float.IsInfinity(carrySpeedMultiplier))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(carrySpeedMultiplier), carrySpeedMultiplier, "carrySpeedMultiplier must be finite and not negative");
            }

            if (!IsFinite(move.X) || !IsFinite(move.Y) || !IsFinite(yaw))
            {
                throw new ArgumentOutOfRangeException(nameof(move), move, "move and yaw must be finite");
            }

            if (!IsFinite(verticalSpeed) || !IsFinite(gapToFloat))
            {
                throw new ArgumentOutOfRangeException(nameof(verticalSpeed), verticalSpeed, "verticalSpeed and gapToFloat must be finite");
            }

            var lengthSquared = move.LengthSquared();
            if (lengthSquared > 1f)
            {
                move /= (float)Math.Sqrt(lengthSquared);
            }

            var horizontal = PlayerMotor.ToWorld(move, yaw) * (tuning.Speed * carrySpeedMultiplier);
            var vertical = verticalSpeed;
            if (!wasSwimming)
            {
                // 入った tick に落ちる速さを泳ぐ速さまで消す。高くから飛び込んでも、浅い底に落下の速さのまま着かないため
                vertical = Math.Max(vertical, -tuning.Speed);
            }

            float rise;
            if (dive)
            {
                vertical = -tuning.Speed;
                rise = vertical * tickSeconds;
            }
            else
            {
                var toward = gapToFloat > 0f ? 1f : (gapToFloat < 0f ? -1f : 0f);
                vertical = ascend && toward > 0f
                    ? tuning.Speed
                    : MoveTowards(vertical, toward * tuning.Speed, tuning.Buoyancy * tickSeconds);
                rise = vertical * tickSeconds;

                // 浮く高さを越えた分を次の tick で戻すと、水面で上下に揺れ続けるので、越える tick はちょうどで止める
                if ((toward > 0f && rise > gapToFloat) || (toward < 0f && rise < gapToFloat))
                {
                    rise = gapToFloat;
                    vertical = 0f;
                }
            }

            displacement = new Vector3(horizontal.X * tickSeconds, rise, horizontal.Y * tickSeconds);
            return new Vector3(horizontal.X, vertical, horizontal.Y);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            return Math.Abs(target - current) <= maxDelta ? target : current + (Math.Sign(target - current) * maxDelta);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool IsInWater(IWaterVolumes water, Vector3 feet)
        {
            if (water == null)
            {
                throw new ArgumentNullException(nameof(water));
            }

            var surface = water.SurfaceAt(feet);
            return surface.HasValue && surface.Value > feet.Y;
        }
    }
}

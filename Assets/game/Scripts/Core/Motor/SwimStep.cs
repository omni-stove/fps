using System;
using System.Numerics;

namespace Shooter.Core
{
    /// <summary>
    /// 泳ぐか・目が水中か・息を、tick の初めの位置から決める。泳ぐときの動きは <see cref="CharacterMotorSimulation"/> が受け持つ。
    /// 胸と目の高さは泳ぐ間の姿勢である立ちの寸法で測る (しゃがんだまま水に入った tick も、入ったかの答えを姿勢で変えないため)。
    /// </summary>
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
                if (breath > tuning.BreathSeconds)
                {
                    // 引き算を繰り返さないのは、tick が息の秒数よりずっと長い設定でも 1 回で数え終えるため
                    var cycles = Math.Floor(breath / (double)tuning.BreathSeconds);
                    breath = Math.Max(0f, (float)(breath - (cycles * tuning.BreathSeconds)));
                    drownings = (int)Math.Min(int.MaxValue, drownings + cycles);
                }
            }

            return new CharacterSwimState(true, surfaceY, underwater, breath, drownings);
        }

        /// <summary>
        /// 胸の Y (本体のローカル)。立ちのカプセルの中心と目の間に置く。
        /// 値として持たせないのは、体の寸法を変えたゲームでも胸が体に付いて動くようにするため。
        /// </summary>
        public static float ChestY(StanceGeometry geometry)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            var standing = geometry.Standing;
            return (standing.CenterY + standing.EyeY) * 0.5f;
        }

        public static float FloatY(float surfaceY, StanceGeometry geometry)
        {
            return surfaceY - ChestY(geometry);
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

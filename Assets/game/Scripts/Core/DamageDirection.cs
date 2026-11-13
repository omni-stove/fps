using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 被弾した方向の表示 (HUD の円周上の弧) に使う計算。
    /// 角度は画面の上 (カメラの正面) を 0 として時計回りの度で、[0, 360) に収める。90 が右、180 が後ろ、270 が左。
    /// 高さの差は無視し、水平面で見た向きだけを使う (上下は円周の上では表せないため)。
    /// yaw は <see cref="LookAngles"/> と同じく Unity の規則 (+Z を 0 として +X へ回る向きが正)。
    /// </summary>
    public static class DamageDirection
    {
        /// <summary>弧を出しておく秒数。この間に薄くなって消える。</summary>
        public const float DefaultSeconds = 1f;

        // これより短い水平の距離は、向きが決まらないとみなす (自分の位置から撃たれた・真上や真下から撃たれた)。
        private const float _minLengthSquared = 1e-8f;

        /// <summary>
        /// viewer から見た source の画面上の角度を求める。
        /// 水平の向きが決まらない (同じ位置・真上・真下) か、値が有限でなければ false。
        /// </summary>
        /// <param name="viewer">見ている位置 (カメラ)。</param>
        /// <param name="viewerYaw">見ている向きの yaw (度)。Unity の eulerAngles.y をそのまま渡してよい。</param>
        /// <param name="source">攻撃元の位置。</param>
        /// <param name="degrees">画面の上から時計回りの角度 (度、[0, 360))。false のときは 0。</param>
        public static bool TryScreenAngle(Vector3 viewer, float viewerYaw, Vector3 source, out float degrees)
        {
            degrees = 0f;
            if (!IsFinite(viewerYaw))
            {
                return false;
            }

            var dx = source.X - viewer.X;
            var dz = source.Z - viewer.Z;
            var lengthSquared = (dx * dx) + (dz * dz);
            if (!IsFinite(lengthSquared) || lengthSquared < _minLengthSquared)
            {
                return false;
            }

            var sourceYaw = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
            degrees = Normalize(sourceYaw - viewerYaw);
            return true;
        }

        /// <summary>
        /// 出してから elapsed 秒たった弧の不透明度。0 秒で 1、duration 秒で 0 になるよう直線で減らし、[0, 1] に収める。
        /// duration が 0 以下か有限でない値は 0 (出さない)。
        /// </summary>
        public static float Opacity(float elapsed, float duration)
        {
            if (!IsFinite(elapsed) || !IsFinite(duration) || duration <= 0f)
            {
                return 0f;
            }

            return Math.Max(0f, Math.Min(1f, 1f - (elapsed / duration)));
        }

        private static float Normalize(float degrees)
        {
            var wrapped = degrees % 360f;
            if (wrapped < 0f)
            {
                wrapped += 360f;
            }

            // -1e-6 % 360 + 360 は float で 360 に丸まることがあるので、範囲の上端を閉じておく。
            return wrapped >= 360f ? 0f : wrapped;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 画面中央 (カメラの正面) で狙った点へ、キャラの目から撃つための計算。一人称と三人称で同じ手順を使う。
    /// <list type="number">
    /// <item>カメラの Ray のうち、カメラと目の間 (<see cref="RayStart"/> より手前) は飛ばす。
    /// 三人称でカメラと体の間にある物を狙ってしまわないため。</item>
    /// <item>残りの Ray で最初に当たった点 (当たらなければ射程の先) を狙う点にする。</item>
    /// <item>目からその点への向き (<see cref="DirectionFromEye"/>) で撃つ。</item>
    /// </list>
    /// 一人称ではカメラと目が重なるので、向きはカメラの正面と同じになる。始点は常に目なので、
    /// 三人称のカメラだけが壁の角から出ている状態で、壁の向こうを撃つことはできない。
    /// </summary>
    public static class AimSolver
    {
        // これより短いベクトルは 0 とみなす。
        private const float _minLengthSquared = 1e-12f;

        /// <summary>
        /// カメラの Ray を飛ばし始める距離。目をカメラの正面の軸へ射影した位置までで、目がカメラより後ろなら 0。
        /// </summary>
        /// <param name="cameraPosition">カメラの位置。</param>
        /// <param name="cameraForward">カメラの正面。長さは問わないが 0 ではないこと。</param>
        /// <param name="eye">目の位置。</param>
        public static float RayStart(Vector3 cameraPosition, Vector3 cameraForward, Vector3 eye)
        {
            var forward = NormalizeOrThrow(cameraForward, nameof(cameraForward));
            return Math.Max(0f, Vector3.Dot(eye - cameraPosition, forward));
        }

        /// <summary>
        /// 目から aimPoint への単位ベクトル。aimPoint が目に重なるか有限でなければ fallback (正規化して返す)。
        /// </summary>
        /// <param name="eye">目の位置。</param>
        /// <param name="aimPoint">狙う点。</param>
        /// <param name="fallback">向きが決まらないときの向き。普通はカメラの正面。0 ではないこと。</param>
        public static Vector3 DirectionFromEye(Vector3 eye, Vector3 aimPoint, Vector3 fallback)
        {
            var normalizedFallback = NormalizeOrThrow(fallback, nameof(fallback));
            var toAim = aimPoint - eye;
            var lengthSquared = toAim.LengthSquared();
            if (float.IsNaN(lengthSquared) || float.IsInfinity(lengthSquared) || lengthSquared < _minLengthSquared)
            {
                return normalizedFallback;
            }

            return toAim / (float)Math.Sqrt(lengthSquared);
        }

        private static Vector3 NormalizeOrThrow(Vector3 value, string name)
        {
            var lengthSquared = value.LengthSquared();
            if (float.IsNaN(lengthSquared) || float.IsInfinity(lengthSquared) || lengthSquared < _minLengthSquared)
            {
                throw new ArgumentException(name + " must be a finite non-zero vector", name);
            }

            return value / (float)Math.Sqrt(lengthSquared);
        }
    }
}

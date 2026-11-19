using System;
using System.Numerics;

namespace Shooter.Core
{
    // Unity 抜きのサーバーは ShrinkingZone を読めないので、Editor がシーンのエリアから書き出した最初の円を持つ。
    // 段階 (待ち・縮み・半径・ダメージ) はマップでなくルールの調整値なので、ここには持たない
    public sealed class NetMapZoneCircle
    {
        public NetMapZoneCircle(Vector3 center, float radius)
        {
            if (!NetMapLootContainer.IsFinite(center))
            {
                throw new ArgumentOutOfRangeException(nameof(center), center, "center must be finite");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            Center = center;
            Radius = radius;
        }

        // 水平の円でも 3 次元で持つのは、境界の筒を立てる高さもシーンから要るため
        public Vector3 Center { get; }

        public float Radius { get; }

        public Vector2 HorizontalCenter => new Vector2(Center.X, Center.Z);
    }
}

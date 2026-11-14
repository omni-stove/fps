using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 印をカメラから見た画面のどこに出すかを決める。画面の中ならその位置に、外 (後ろも含む) なら画面の中心から印の方へ伸ばした線が
    /// 端の余白の内側の枠に当たる点に寄せ、向きを出す。
    /// </summary>
    public static class PingScreen
    {
        private const float _centerEpsilon = 1e-6f;

        /// <param name="viewport">
        /// カメラから見た印の位置。x, y は画面の左下が (0, 0)、右上が (1, 1)、z はカメラの正面方向の距離 (0 以下ならカメラの後ろ)。
        /// Unity の <c>Camera.WorldToViewportPoint</c> の値。各成分は有限の値。
        /// </param>
        /// <param name="aspect">画面の幅 / 高さ。0 より大きい有限値。端に寄せる向きを画面の縦横比どおりにするのに使う。</param>
        /// <param name="margin">端の余白。画面の高さを 1 とした量で、0 以上 0.5 未満、かつ幅の半分未満。</param>
        public static PingScreenPlacement Place(Vector3 viewport, float aspect, float margin)
        {
            if (!PingAim.IsFinite(viewport))
            {
                throw new ArgumentOutOfRangeException(nameof(viewport), viewport, "viewport must be finite");
            }

            if (!(aspect > 0f) || float.IsInfinity(aspect))
            {
                throw new ArgumentOutOfRangeException(nameof(aspect), aspect, "aspect must be positive and finite");
            }

            if (!(margin >= 0f) || margin >= 0.5f || margin >= 0.5f * aspect)
            {
                throw new ArgumentOutOfRangeException(nameof(margin), margin, "margin must be at least 0 and less than half of the screen");
            }

            // 縦の長さを 1 とした、画面の中心からの位置 (上向きが +y)。縦横比を掛けて、向きを見た目どおりにする。
            var dx = (viewport.X - 0.5f) * aspect;
            var dy = viewport.Y - 0.5f;
            var behind = viewport.Z <= 0f;

            // カメラの後ろの点は、投影すると中心を挟んだ反対側に写るので向きを戻す。
            if (behind)
            {
                dx = -dx;
                dy = -dy;
            }

            var halfWidth = (0.5f * aspect) - margin;
            var halfHeight = 0.5f - margin;
            if (!behind && Math.Abs(dx) <= halfWidth && Math.Abs(dy) <= halfHeight)
            {
                return new PingScreenPlacement(true, viewport.X, 1f - viewport.Y, 0f, PingScreenEdge.None);
            }

            // 真後ろでは向きが決まらないので、振り向く向きが分かるよう下の端に出す。
            if (Math.Abs(dx) < _centerEpsilon && Math.Abs(dy) < _centerEpsilon)
            {
                dx = 0f;
                dy = -1f;
            }

            var scaleX = Math.Abs(dx) > 0f ? halfWidth / Math.Abs(dx) : float.PositiveInfinity;
            var scaleY = Math.Abs(dy) > 0f ? halfHeight / Math.Abs(dy) : float.PositiveInfinity;
            float scale;
            PingScreenEdge edge;
            if (scaleX <= scaleY)
            {
                scale = scaleX;
                edge = dx > 0f ? PingScreenEdge.Right : PingScreenEdge.Left;
            }
            else
            {
                scale = scaleY;
                edge = dy > 0f ? PingScreenEdge.Top : PingScreenEdge.Bottom;
            }

            var x = 0.5f + (dx * scale / aspect);
            var y = 0.5f - (dy * scale);
            var angle = (float)(Math.Atan2(dx, dy) * 180.0 / Math.PI);
            return new PingScreenPlacement(false, x, y, angle, edge);
        }
    }
}

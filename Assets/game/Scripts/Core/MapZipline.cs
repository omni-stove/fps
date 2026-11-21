using System;
using System.Numerics;

namespace Shooter.Core
{
    // Unity 抜きのサーバーがシーンと同じ線で滑らせるよう、シーンの Zipline からではなく NetMapZiplines で書き出したものを読む。
    public sealed class MapZipline
    {
        public MapZipline(Vector3 start, Vector3 end, ZiplineSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));

            // 掴まる瞬間に不正な線で失敗しないよう、滑らせる型の検査を読むときに済ませる (オフラインの Zipline.SetEnds と同じ)
            _ = new ZiplineMotion(start, end, spec);
            Start = start;
            End = end;
        }

        public Vector3 Start { get; }

        public Vector3 End { get; }

        public ZiplineSpec Spec { get; }

        // 掴まるたびに作るのは、滑っている途中の位置と速さを人ごとに持つため
        public ZiplineMotion CreateMotion()
        {
            return new ZiplineMotion(Start, End, Spec);
        }

        public float DistanceTo(Vector3 point)
        {
            var segment = End - Start;
            var lengthSquared = segment.LengthSquared();
            var t = lengthSquared > 0f ? Vector3.Dot(point - Start, segment) / lengthSquared : 0f;
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return Vector3.Distance(point, Start + (segment * t));
        }
    }
}

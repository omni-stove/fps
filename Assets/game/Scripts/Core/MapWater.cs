using System.Numerics;

namespace Shooter.Core
{
    // Unity 抜きのサーバーとクライアントの予測が同じ水で泳ぐよう、シーンからではなく NetMapWater で書き出したものを読む。
    public sealed class MapWater
    {
        public MapWater(WaterBox box)
        {
            Box = box;
        }

        public WaterBox Box { get; }

        public float? SurfaceAt(Vector3 position)
        {
            return Box.Contains(position) ? Box.SurfaceY : (float?)null;
        }
    }
}

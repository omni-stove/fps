using System;
using System.Collections.Generic;
using Shooter.Core;
using Vector3 = System.Numerics.Vector3;

namespace Shooter.Runtime.Traversal
{
    // 重なった水では高い方の水面を返す。低い方を返すと、深い水の中に浅い水を置いた所で、潜っているのに水面より上と判じられる。
    public sealed class WaterVolumes : IWaterVolumes
    {
        // 見た目と音は開いた水をその場で追えばよいが、予測とサーバーに渡す水は同じ時点の写しでないと食い違うので、両方を持つ。
        public static readonly IWaterVolumes Scene = new SceneWater();

        private readonly WaterBox[] _boxes;

        public WaterVolumes(IEnumerable<WaterBox> boxes)
        {
            if (boxes == null)
            {
                throw new ArgumentNullException(nameof(boxes));
            }

            _boxes = new List<WaterBox>(boxes).ToArray();
        }

        public IReadOnlyList<WaterBox> Boxes => _boxes;

        public static WaterVolumes FromScene()
        {
            var boxes = new List<WaterBox>();
            foreach (var water in Water.Enabled)
            {
                if (water != null && water.TryGetBox(out var box))
                {
                    boxes.Add(box);
                }
            }

            return new WaterVolumes(boxes);
        }

        public float? SurfaceAt(Vector3 position)
        {
            float? surface = null;
            foreach (var box in _boxes)
            {
                surface = Higher(surface, box, position);
            }

            return surface;
        }

        private static float? Higher(float? surface, WaterBox box, Vector3 position)
        {
            if (!box.Contains(position))
            {
                return surface;
            }

            return surface.HasValue && surface.Value >= box.SurfaceY ? surface : box.SurfaceY;
        }

        private sealed class SceneWater : IWaterVolumes
        {
            public float? SurfaceAt(Vector3 position)
            {
                float? surface = null;
                var enabled = Water.Enabled;
                for (var i = 0; i < enabled.Count; i++)
                {
                    var water = enabled[i];
                    if (water != null && water.TryGetBox(out var box))
                    {
                        surface = Higher(surface, box, position);
                    }
                }

                return surface;
            }
        }
    }
}

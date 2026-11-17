using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    // 泳ぐかは毎 tick 足元の水面で決まるので、Unity の Editor がシーンの水から書き出したものをサーバーと予測で同じに読む。
    // 重なった水では高い方の水面を返す (Runtime の WaterVolumes と同じ。低い方を返すと、深い水の中に浅い水を置いた所で潜っているのに水面より上と判じられる)。
    public sealed class NetMapWater : IWaterVolumes
    {
        public const string Extension = ".water.json";

        public const string WaterItem = "water";
        public const string MinItem = "min";
        public const string MaxItem = "max";
        public const string SurfaceYItem = "surfaceY";

        private const string _kind = "map water JSON";

        private static readonly string[] _topItems = { WaterItem };

        private static readonly string[] _waterItems = { MinItem, MaxItem, SurfaceYItem };

        private readonly MapWater[] _water;

        public NetMapWater(IReadOnlyList<MapWater> water)
        {
            if (water == null)
            {
                throw new ArgumentNullException(nameof(water));
            }

            var copy = new MapWater[water.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = water[i] ?? throw new ArgumentException("water must not contain null", nameof(water));
            }

            _water = copy;
            Water = Array.AsReadOnly(copy);
        }

        public static NetMapWater None { get; } = new NetMapWater(Array.Empty<MapWater>());

        public IReadOnlyList<MapWater> Water { get; }

        public float? SurfaceAt(Vector3 position)
        {
            float? surface = null;
            for (var i = 0; i < _water.Length; i++)
            {
                var here = _water[i].SurfaceAt(position);
                if (here.HasValue && (!surface.HasValue || here.Value > surface.Value))
                {
                    surface = here;
                }
            }

            return surface;
        }

        public static NetMapWater Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            var root = ServerDataJson.ParseDocument(text, source, _kind);
            var top = new ServerDataJson.Fields(source, _kind, root, string.Empty, _topItems);

            var water = new List<MapWater>();
            var nodes = top.Array(WaterItem);
            for (var i = 0; i < nodes.Count; i++)
            {
                var fields = top.Element(nodes[i], WaterItem, i, _waterItems);
                WaterBox box;
                try
                {
                    box = new WaterBox(
                        fields.ReadVector3(MinItem),
                        fields.ReadVector3(MaxItem),
                        fields.ToFloat(fields.ItemPath(SurfaceYItem), fields.Require(SurfaceYItem)));
                }
                catch (ArgumentException e)
                {
                    // WaterBox の引数名は項目名と同じ
                    throw fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
                }

                water.Add(new MapWater(box));
            }

            return new NetMapWater(water);
        }

        public static string Write(NetMapWater water)
        {
            if (water == null)
            {
                throw new ArgumentNullException(nameof(water));
            }

            var writer = new ServerDataJson.Writer();
            writer.BeginArray(WaterItem);
            foreach (var volume in water.Water)
            {
                var box = volume.Box;
                writer.BeginElement();
                writer.Vector3(MinItem, box.Min);
                writer.Vector3(MaxItem, box.Max);
                writer.Float(SurfaceYItem, box.SurfaceY);
                writer.EndObject();
            }

            writer.EndArray();
            return writer.Finish();
        }
    }
}

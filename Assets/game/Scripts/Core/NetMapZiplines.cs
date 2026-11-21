using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    // 線の両端と滑り方は NavMesh や当たりから作れないので、Unity の Editor がシーンのジップラインから書き出したものを読む。
    // 並びはシーンの階層順で、同じ近さの線が並んだときは先の線を掴む。
    public sealed class NetMapZiplines
    {
        public const string Extension = ".ziplines.json";

        public const string ZiplinesItem = "ziplines";
        public const string StartItem = "start";
        public const string EndItem = "end";
        public const string MinSpeedItem = "minSpeed";
        public const string MaxSpeedItem = "maxSpeed";
        public const string GravityItem = "gravity";
        public const string EndMarginItem = "endMargin";

        private const string _kind = "map ziplines JSON";

        private static readonly string[] _topItems = { ZiplinesItem };

        private static readonly string[] _ziplineItems = { StartItem, EndItem, MinSpeedItem, MaxSpeedItem, GravityItem, EndMarginItem };

        public NetMapZiplines(IReadOnlyList<MapZipline> ziplines)
        {
            if (ziplines == null)
            {
                throw new ArgumentNullException(nameof(ziplines));
            }

            var copy = new List<MapZipline>(ziplines.Count);
            foreach (var zipline in ziplines)
            {
                copy.Add(zipline ?? throw new ArgumentException("ziplines must not contain null", nameof(ziplines)));
            }

            Ziplines = copy.AsReadOnly();
        }

        public static NetMapZiplines None { get; } = new NetMapZiplines(Array.Empty<MapZipline>());

        public IReadOnlyList<MapZipline> Ziplines { get; }

        // 掴める線が無ければ -1
        public int FindNearest(Vector3 point, float maxDistance)
        {
            var best = -1;
            var bestDistance = maxDistance;
            for (var i = 0; i < Ziplines.Count; i++)
            {
                var distance = Ziplines[i].DistanceTo(point);
                if (distance <= bestDistance && (best < 0 || distance < bestDistance))
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public static NetMapZiplines Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            var root = ServerDataJson.ParseDocument(text, source, _kind);
            var top = new ServerDataJson.Fields(source, _kind, root, string.Empty, _topItems);

            var ziplines = new List<MapZipline>();
            var nodes = top.Array(ZiplinesItem);
            for (var i = 0; i < nodes.Count; i++)
            {
                var fields = top.Element(nodes[i], ZiplinesItem, i, _ziplineItems);
                var start = fields.ReadVector3(StartItem);
                var end = fields.ReadVector3(EndItem);
                try
                {
                    var spec = new ZiplineSpec(
                        fields.ReadFloat(MinSpeedItem, ZiplineSpec.DefaultMinSpeed),
                        fields.ReadFloat(MaxSpeedItem, ZiplineSpec.DefaultMaxSpeed),
                        fields.ReadFloat(GravityItem, ZiplineSpec.DefaultGravity),
                        fields.ReadFloat(EndMarginItem, ZiplineSpec.DefaultEndMargin));
                    ziplines.Add(new MapZipline(start, end, spec));
                }
                catch (ArgumentException e)
                {
                    // ZiplineSpec と ZiplineMotion の引数名は項目名と同じ
                    throw fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
                }
            }

            return new NetMapZiplines(ziplines);
        }

        public static string Write(NetMapZiplines ziplines)
        {
            if (ziplines == null)
            {
                throw new ArgumentNullException(nameof(ziplines));
            }

            var writer = new JsonWriter();
            writer.BeginArray(ZiplinesItem);
            foreach (var zipline in ziplines.Ziplines)
            {
                var spec = zipline.Spec;
                writer.BeginElement();
                writer.Vector3(StartItem, zipline.Start);
                writer.Vector3(EndItem, zipline.End);
                writer.Float(MinSpeedItem, spec.MinSpeed);
                writer.Float(MaxSpeedItem, spec.MaxSpeed);
                writer.Float(GravityItem, spec.Gravity);
                writer.Float(EndMarginItem, spec.EndMargin);
                writer.EndObject();
            }

            writer.EndArray();
            return writer.Finish();
        }
    }
}

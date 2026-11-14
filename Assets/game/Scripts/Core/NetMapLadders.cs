using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 1 つの対戦マップのはしご (<c>server-data/maps/&lt;マップ&gt;.ladders.json</c>)。NavMesh のリンクの両端からは幅・向き・調整値を作れないので、
    /// Unity の Editor がシーンのはしごから書き出す (<see cref="Write"/>)。並びはシーンの階層順で、スナップショットははしごをこの並びの番号で指す。
    /// </summary>
    public sealed class NetMapLadders
    {
        public const string Extension = ".ladders.json";

        public const string LaddersItem = "ladders";
        public const string BottomItem = "bottom";
        public const string FacingItem = "facing";
        public const string HeightItem = "height";
        public const string WidthItem = "width";
        public const string ClimbSpeedItem = "climbSpeed";
        public const string GrabDistanceItem = "grabDistance";
        public const string JumpOffSpeedItem = "jumpOffSpeed";
        public const string RegrabSecondsItem = "regrabSeconds";

        private const string _kind = "map ladders JSON";

        private static readonly string[] _topItems = { LaddersItem };

        private static readonly string[] _ladderItems =
        {
            BottomItem, FacingItem, HeightItem, WidthItem, ClimbSpeedItem, GrabDistanceItem, JumpOffSpeedItem, RegrabSecondsItem,
        };

        public NetMapLadders(IReadOnlyList<MapLadder> ladders)
        {
            if (ladders == null)
            {
                throw new ArgumentNullException(nameof(ladders));
            }

            var copy = new List<MapLadder>(ladders.Count);
            foreach (var ladder in ladders)
            {
                copy.Add(ladder ?? throw new ArgumentException("ladders must not contain null", nameof(ladders)));
            }

            Ladders = copy.AsReadOnly();
        }

        public static NetMapLadders None { get; } = new NetMapLadders(Array.Empty<MapLadder>());

        public IReadOnlyList<MapLadder> Ladders { get; }

        public static NetMapLadders Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            var root = ServerDataJson.ParseDocument(text, source, _kind);
            var top = new ServerDataJson.Fields(source, _kind, root, string.Empty, _topItems);

            var ladders = new List<MapLadder>();
            var nodes = top.Array(LaddersItem);
            for (var i = 0; i < nodes.Count; i++)
            {
                var fields = top.Element(nodes[i], LaddersItem, i, _ladderItems);
                LadderSpan span;
                try
                {
                    span = new LadderSpan(
                        fields.ReadVector3(BottomItem),
                        fields.ReadVector3(FacingItem),
                        fields.ToFloat(fields.ItemPath(HeightItem), fields.Require(HeightItem)),
                        fields.ToFloat(fields.ItemPath(WidthItem), fields.Require(WidthItem)));
                }
                catch (ArgumentException e)
                {
                    // LadderSpan の引数名は項目名と同じ
                    throw fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
                }

                LadderSpec spec;
                try
                {
                    spec = new LadderSpec(
                        fields.ReadFloat(ClimbSpeedItem, LadderSpec.DefaultClimbSpeed),
                        fields.ReadFloat(GrabDistanceItem, LadderSpec.DefaultGrabDistance),
                        fields.ReadFloat(JumpOffSpeedItem, LadderSpec.DefaultJumpOffSpeed),
                        fields.ReadFloat(RegrabSecondsItem, LadderSpec.DefaultRegrabSeconds));
                }
                catch (ArgumentException e)
                {
                    // LadderSpec の引数名は項目名と同じ
                    throw fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
                }

                ladders.Add(new MapLadder(span, spec));
            }

            return new NetMapLadders(ladders);
        }

        public static string Write(NetMapLadders ladders)
        {
            if (ladders == null)
            {
                throw new ArgumentNullException(nameof(ladders));
            }

            var writer = new ServerDataJson.Writer();
            writer.BeginArray(LaddersItem);
            foreach (var ladder in ladders.Ladders)
            {
                var span = ladder.Span;
                var spec = ladder.Spec;
                writer.BeginElement();
                writer.Vector3(BottomItem, span.Bottom);
                writer.Vector3(FacingItem, span.Facing);
                writer.Float(HeightItem, span.Height);
                writer.Float(WidthItem, span.Width);
                writer.Float(ClimbSpeedItem, spec.ClimbSpeed);
                writer.Float(GrabDistanceItem, spec.GrabDistance);
                writer.Float(JumpOffSpeedItem, spec.JumpOffSpeed);
                writer.Float(RegrabSecondsItem, spec.RegrabSeconds);
                writer.EndObject();
            }

            writer.EndArray();
            return writer.Finish();
        }
    }
}

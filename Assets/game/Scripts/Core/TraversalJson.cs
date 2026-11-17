using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class TraversalJson
    {
        public const string ResourcePath = "Tuning/traversal";

        public const string SlideItem = "slide";
        public const string ParachuteItem = "parachute";
        public const string ZiplineItem = "zipline";

        public const string StartSpeedItem = "startSpeed";
        public const string DecelerationItem = "deceleration";
        public const string EndSpeedItem = "endSpeed";
        public const string MinSecondsItem = "minSeconds";
        public const string MaxSecondsItem = "maxSeconds";
        public const string SlopeAccelerationItem = "slopeAcceleration";

        public const string MinOpenHeightItem = "minOpenHeight";
        public const string DescentSpeedItem = "descentSpeed";
        public const string HorizontalSpeedItem = "horizontalSpeed";
        public const string SteerAccelerationItem = "steerAcceleration";

        public const string GrabRadiusItem = "grabRadius";
        public const string HangOffsetItem = "hangOffset";
        public const string RegrabSecondsItem = "regrabSeconds";
        public const string SpreadMoveFractionItem = "spreadMoveFraction";

        private const string _kind = "traversal JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { SlideItem, ParachuteItem, ZiplineItem };

        // 項目名はコンストラクターの引数名にそろえ、値の検査で断られた項目を ParamName から示せるようにする。
        public static IReadOnlyList<string> SlideItemNames { get; } = new[]
        {
            StartSpeedItem,
            DecelerationItem,
            EndSpeedItem,
            MinSecondsItem,
            MaxSecondsItem,
            SlopeAccelerationItem,
        };

        public static IReadOnlyList<string> ParachuteItemNames { get; } = new[]
        {
            MinOpenHeightItem,
            DescentSpeedItem,
            HorizontalSpeedItem,
            SteerAccelerationItem,
        };

        public static IReadOnlyList<string> ZiplineItemNames { get; } = new[]
        {
            GrabRadiusItem,
            HangOffsetItem,
            RegrabSecondsItem,
            SpreadMoveFractionItem,
        };

        public static TraversalSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                var slide = fields.Object(SlideItem, SlideItemNames);
                var parachute = fields.Object(ParachuteItem, ParachuteItemNames);
                var zipline = fields.Object(ZiplineItem, ZiplineItemNames);
                return new TraversalSpec(
                    slide == null ? null : ReadSlide(slide),
                    parachute == null ? null : ReadParachute(parachute),
                    zipline == null ? null : ReadZipline(zipline));
            }
            catch (ServerDataJsonException e)
            {
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static SlideTuning ReadSlide(ServerDataJson.Fields fields)
        {
            var defaults = SlideTuning.Default;
            var startSpeed = fields.ReadFloat(StartSpeedItem, defaults.StartSpeed);
            var deceleration = fields.ReadFloat(DecelerationItem, defaults.Deceleration);
            var endSpeed = fields.ReadFloat(EndSpeedItem, defaults.EndSpeed);
            var minSeconds = fields.ReadFloat(MinSecondsItem, defaults.MinSeconds);
            var maxSeconds = fields.ReadFloat(MaxSecondsItem, defaults.MaxSeconds);
            var slopeAcceleration = fields.ReadFloat(SlopeAccelerationItem, defaults.SlopeAcceleration);
            try
            {
                return new SlideTuning(startSpeed, deceleration, endSpeed, minSeconds, maxSeconds, slopeAcceleration);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, SlideItemNames, e);
            }
        }

        private static ParachuteTuning ReadParachute(ServerDataJson.Fields fields)
        {
            var defaults = ParachuteTuning.Default;
            var minOpenHeight = fields.ReadFloat(MinOpenHeightItem, defaults.MinOpenHeight);
            var descentSpeed = fields.ReadFloat(DescentSpeedItem, defaults.DescentSpeed);
            var horizontalSpeed = fields.ReadFloat(HorizontalSpeedItem, defaults.HorizontalSpeed);
            var steerAcceleration = fields.ReadFloat(SteerAccelerationItem, defaults.SteerAcceleration);
            try
            {
                return new ParachuteTuning(minOpenHeight, descentSpeed, horizontalSpeed, steerAcceleration);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, ParachuteItemNames, e);
            }
        }

        private static ZiplineTuning ReadZipline(ServerDataJson.Fields fields)
        {
            var defaults = ZiplineTuning.Default;
            var grabRadius = fields.ReadFloat(GrabRadiusItem, defaults.GrabRadius);
            var hangOffset = fields.ReadFloat(HangOffsetItem, defaults.HangOffset);
            var regrabSeconds = fields.ReadFloat(RegrabSecondsItem, defaults.RegrabSeconds);
            var spreadMoveFraction = fields.ReadFloat(SpreadMoveFractionItem, defaults.SpreadMoveFraction);
            try
            {
                return new ZiplineTuning(grabRadius, hangOffset, regrabSeconds, spreadMoveFraction);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, ZiplineItemNames, e);
            }
        }

        private static ServerDataJsonException Rejected(ServerDataJson.Fields fields, IReadOnlyList<string> known, ArgumentException e)
        {
            foreach (var name in known)
            {
                if (string.Equals(name, e.ParamName, StringComparison.Ordinal))
                {
                    return fields.Rejected(fields.ItemPath(name), e.Message, e);
                }
            }

            return fields.Rejected(fields.Path, e.Message, e);
        }
    }
}

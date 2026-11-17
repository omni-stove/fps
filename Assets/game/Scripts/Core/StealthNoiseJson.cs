using System;
using System.Collections.Generic;

namespace Fps.Core
{
    public static class StealthNoiseJson
    {
        public const string FootstepNoiseRadiusItem = "footstepNoiseRadius";
        public const string WalkStrideItem = "walkStride";
        public const string SprintStrideItem = "sprintStride";
        public const string CrouchStrideItem = "crouchStride";
        public const string WalkVolumeItem = "walkVolume";
        public const string SprintVolumeItem = "sprintVolume";
        public const string CrouchVolumeItem = "crouchVolume";
        public const string MinLandingFallSpeedItem = "minLandingFallSpeed";
        public const string HardLandingFallSpeedItem = "hardLandingFallSpeed";
        public const string MinLandingVolumeItem = "minLandingVolume";

        private const string _kind = "stealth JSON";

        // 項目名は StealthNoiseSpec のコンストラクターの引数名にそろえ、値の検査で断られた項目を ParamName からそのまま示せるようにする
        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            FootstepNoiseRadiusItem,
            WalkStrideItem,
            SprintStrideItem,
            CrouchStrideItem,
            WalkVolumeItem,
            SprintVolumeItem,
            CrouchVolumeItem,
            MinLandingFallSpeedItem,
            HardLandingFallSpeedItem,
            MinLandingVolumeItem,
        };

        public static StealthNoiseSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);

            // KitTuning は TuningJsonException だけを受けて既定値で続けるので、ServerDataJson の例外をここで包み直す
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                var defaults = StealthNoiseSpec.Default;
                var radius = fields.ReadFloat(FootstepNoiseRadiusItem, defaults.FootstepNoiseRadius);
                var walkStride = fields.ReadFloat(WalkStrideItem, defaults.WalkStride);
                var sprintStride = fields.ReadFloat(SprintStrideItem, defaults.SprintStride);
                var crouchStride = fields.ReadFloat(CrouchStrideItem, defaults.CrouchStride);
                var walkVolume = fields.ReadFloat(WalkVolumeItem, defaults.WalkVolume);
                var sprintVolume = fields.ReadFloat(SprintVolumeItem, defaults.SprintVolume);
                var crouchVolume = fields.ReadFloat(CrouchVolumeItem, defaults.CrouchVolume);
                var minLandingFallSpeed = fields.ReadFloat(MinLandingFallSpeedItem, defaults.MinLandingFallSpeed);
                var hardLandingFallSpeed = fields.ReadFloat(HardLandingFallSpeedItem, defaults.HardLandingFallSpeed);
                var minLandingVolume = fields.ReadFloat(MinLandingVolumeItem, defaults.MinLandingVolume);
                try
                {
                    return new StealthNoiseSpec(
                        radius,
                        walkStride,
                        sprintStride,
                        crouchStride,
                        walkVolume,
                        sprintVolume,
                        crouchVolume,
                        minLandingFallSpeed,
                        hardLandingFallSpeed,
                        minLandingVolume);
                }
                catch (ArgumentException e)
                {
                    var item = e.ParamName != null && Contains(ItemNames, e.ParamName) ? e.ParamName : null;
                    throw fields.Rejected(item, e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static bool Contains(IReadOnlyList<string> names, string name)
        {
            foreach (var candidate in names)
            {
                if (string.Equals(candidate, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 武器の aim.sight とアタッチメントの attachment.sight で同じ形を読み書きするので、ここにまとめる。
    internal static class SightSpecJson
    {
        public const string SightItem = "sight";
        public const string ReticleIdItem = "reticleId";
        public const string ScopeOverlayItem = "scopeOverlay";
        public const string EyeViewInThirdPersonItem = "eyeViewInThirdPerson";
        public const string AimedInProgressItem = "aimedInProgress";
        public const string VisionModeItem = "visionMode";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            ReticleIdItem, ScopeOverlayItem, EyeViewInThirdPersonItem, AimedInProgressItem, VisionModeItem,
        };

        public static SightSpec Read(ServerDataJson.Fields fields)
        {
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            try
            {
                return new SightSpec(
                    fields.ReadString(ReticleIdItem, null),
                    fields.ReadBool(ScopeOverlayItem, false),
                    fields.ReadBool(EyeViewInThirdPersonItem, false),
                    fields.ReadFloat(AimedInProgressItem, SightSpec.DefaultAimedInProgress),
                    fields.ReadEnum(VisionModeItem, VisionMode.None));
            }
            catch (ArgumentException e)
            {
                var item = Known(e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path;
                throw fields.Rejected(item, e.Message, e);
            }
        }

        public static void Write(JsonWriter writer, SightSpec sight)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (sight == null || sight.IsDefault)
            {
                return;
            }

            writer.BeginObject(SightItem);
            if (sight.HasReticle)
            {
                writer.String(ReticleIdItem, sight.ReticleId);
            }

            writer.Bool(ScopeOverlayItem, sight.ScopeOverlay);
            writer.Bool(EyeViewInThirdPersonItem, sight.EyeViewInThirdPerson);
            writer.Float(AimedInProgressItem, sight.AimedInProgress);

            // 暗視の無い照準器は今まで通りの JSON のまま書き出すため、None のときは項目を出さない。
            if (sight.VisionMode != VisionMode.None)
            {
                writer.Enum(VisionModeItem, sight.VisionMode);
            }

            writer.EndObject();
        }

        private static bool Known(string item)
        {
            foreach (var name in ItemNames)
            {
                if (string.Equals(name, item, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

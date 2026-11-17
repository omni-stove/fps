using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>
    /// 照準器 (<see cref="SightSpec"/>) の JSON の項目。武器の <c>aim.sight</c> (<see cref="WeaponSpecJson"/>) と
    /// アタッチメントの <c>attachment.sight</c> (<see cref="InventoryJson"/>) で同じ形を読み書きする。
    /// </summary>
    internal static class SightSpecJson
    {
        public const string SightItem = "sight";
        public const string ReticleIdItem = "reticleId";
        public const string ScopeOverlayItem = "scopeOverlay";
        public const string EyeViewInThirdPersonItem = "eyeViewInThirdPerson";
        public const string AimedInProgressItem = "aimedInProgress";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            ReticleIdItem, ScopeOverlayItem, EyeViewInThirdPersonItem, AimedInProgressItem,
        };

        /// <exception cref="ServerDataJsonException">値が不正。項目の道筋を持つ。</exception>
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
                    fields.ReadFloat(AimedInProgressItem, SightSpec.DefaultAimedInProgress));
            }
            catch (ArgumentException e)
            {
                var item = Known(e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path;
                throw fields.Rejected(item, e.Message, e);
            }
        }

        /// <summary>sight を <see cref="SightItem"/> のオブジェクトとして書く。既定 (<see cref="SightSpec.IsDefault"/>) なら何も書かない。</summary>
        public static void Write(ServerDataJson.Writer writer, SightSpec sight)
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
